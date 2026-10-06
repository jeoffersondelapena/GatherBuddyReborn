using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Crafting;

// fork: a craft the character cannot start is bought, waited for or left out before the run sources anything
public readonly record struct PlanSourced(uint ItemId, int Amount, string? Why);

public readonly record struct PlanGated(uint RecipeId, string Name, string Why, bool Final);

public sealed class CraftingListPlan
{
    public List<CraftingListItem> OriginalRecipes { get; } = new();
    public List<CraftingListItem> Recipes { get; } = new();
    public Dictionary<uint, int> Materials { get; } = new();
    public Dictionary<uint, int> Precrafts { get; } = new();
    public Dictionary<uint, IngredientQualityDemand> IngredientDemands { get; } = new();
    public Dictionary<uint, int> RetainerConsumedCraftables { get; } = new();
    public List<PlanSourced> BoughtInstead { get; } = new();
    public List<PlanGated> OutOfReach { get; } = new();
    public List<PlanGated> Deferred { get; } = new();
    internal List<PlanGated> Blocked { get; } = new();
}

public readonly record struct CraftingListPlannerOptions(
    bool UseRetainerCraftableAvailability = false,
    bool ConsumeIntermediateAvailability = true,
    bool ConsumeFinalAvailability = true,
    IReadOnlyCollection<uint>? LeaveOut = null);

public static class CraftingListPlanner
{
    // a final whose precraft turns out blocked is planned again without it, so none of its other materials are sourced
    public static CraftingListPlan Build(CraftingListDefinition list, CraftingListPlannerOptions options = default)
    {
        var leaveOut = new HashSet<uint>(options.LeaveOut ?? []);
        var blocked  = new List<PlanGated>();
        for (var pass = 0; ; pass++)
        {
            var plan = new Planner(list, options with { LeaveOut = leaveOut }).Build();
            if (plan.Blocked.Count == 0 || pass == 3)
            {
                plan.OutOfReach.AddRange(blocked);
                return plan;
            }

            blocked.AddRange(plan.Blocked);
            leaveOut.UnionWith(plan.Blocked.Select(b => b.RecipeId));
        }
    }

    private sealed class Planner
    {
        private readonly CraftingListDefinition _list;
        private readonly CraftingListPlan _plan = new();
        private readonly AvailabilityLedger _availability;
        private readonly bool _useRetainers;
        private readonly bool _consumeIntermediateAvailability;
        private readonly bool _consumeFinalAvailability;
        private readonly IReadOnlyCollection<uint> _leaveOut;
        private readonly Dictionary<uint, CraftingListItem> _originalRecipeLookup;
        private readonly Dictionary<uint, Gate?> _gates = new();
        private readonly Dictionary<uint, int> _bought = new();
        private CraftingListItem? _original;

        public Planner(CraftingListDefinition list, CraftingListPlannerOptions options)
        {
            _list = list;
            _useRetainers = options.UseRetainerCraftableAvailability;
            _consumeIntermediateAvailability = options.ConsumeIntermediateAvailability;
            _consumeFinalAvailability = options.ConsumeFinalAvailability;
            _leaveOut = options.LeaveOut ?? [];
            _availability = new AvailabilityLedger(_useRetainers);
            _originalRecipeLookup = list.Recipes
                .GroupBy(item => item.RecipeId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        public CraftingListPlan Build()
        {
            var skippedCrafted = 0;
            var remembered     = new List<string>();
            foreach (var item in GetOriginalRecipesInDependencyOrder())
            {
                if (item.Options.Skipping || item.Quantity <= 0 || _leaveOut.Contains(item.RecipeId))
                    continue;
                if (_list.SkipCraftedRecipes && QuestManager.IsRecipeComplete(item.RecipeId))
                {
                    skippedCrafted++;
                    continue;
                }

                var recipe = RecipeManager.GetRecipe(item.RecipeId);
                if (!recipe.HasValue)
                    continue;
                if (SkippedRecipes.IsRemembered(item.RecipeId))
                {
                    remembered.Add(recipe.Value.ItemResult.Value.Name.ExtractText());
                    continue;
                }

                _original = item;
                PlanOriginalRecipe(item, recipe.Value);
            }
            // the buy run subtracts the bags again, so a bought craft's target carries what the plan already spent from them
            foreach (var (itemId, amount) in _bought)
                AddCount(_plan.Materials, itemId, PurchaseRules.BoughtTarget(amount, _availability.InventoryTaken(itemId)));
            if (skippedCrafted > 0)
                GatherBuddy.Log.Information($"[CraftingListPlanner] Skip Logged Recipes left out {skippedCrafted} recipe(s) already in the crafting log for list '{_list.Name}'");
            SkippedRecipes.LeftOutThisRun.Clear();
            if (remembered.Count > 0)
            {
                SkippedRecipes.LeftOutThisRun.AddRange(remembered);
                ForkChat.List($"Left out {remembered.Count} remembered recipe(s) that would not start before:", remembered, 10,
                    tone: Communicator.Tone.Info);
                GatherBuddy.Log.Warning($"[CraftingListPlanner] Remembered recipes left out of list '{_list.Name}': {string.Join(", ", remembered)}");
            }

            return _plan;
        }

        private List<CraftingListItem> GetOriginalRecipesInDependencyOrder()
        {
            var orderedRecipes = new List<CraftingListItem>();
            var processedRecipeIds = new HashSet<uint>();
            var visitingRecipeIds = new HashSet<uint>();

            foreach (var item in _list.Recipes)
                VisitOriginalRecipe(item, processedRecipeIds, visitingRecipeIds, orderedRecipes);

            return orderedRecipes;
        }

        private void VisitOriginalRecipe(
            CraftingListItem item,
            HashSet<uint> processedRecipeIds,
            HashSet<uint> visitingRecipeIds,
            List<CraftingListItem> orderedRecipes)
        {
            if (processedRecipeIds.Contains(item.RecipeId))
                return;

            if (!visitingRecipeIds.Add(item.RecipeId))
                return;

            var recipe = RecipeManager.GetRecipe(item.RecipeId);
            if (recipe.HasValue)
            {
                foreach (var (itemId, _) in RecipeManager.GetIngredients(recipe.Value))
                {
                    var dependencyRecipe = RecipeManager.GetRecipeForItem(itemId);
                    if (!dependencyRecipe.HasValue)
                        continue;

                    var dependencyItem = _list.Recipes.FirstOrDefault(candidate => candidate.RecipeId == dependencyRecipe.Value.RowId);
                    if (dependencyItem != null)
                        VisitOriginalRecipe(dependencyItem, processedRecipeIds, visitingRecipeIds, orderedRecipes);
                }
            }

            visitingRecipeIds.Remove(item.RecipeId);
            processedRecipeIds.Add(item.RecipeId);
            orderedRecipes.Add(item);
        }

        private void PlanOriginalRecipe(CraftingListItem item, Recipe recipe)
        {
            var resultItemId = recipe.ItemResult.RowId;
            var requestedItemCount = item.Quantity * (int)recipe.AmountResult;
            var remainingItemCount = requestedItemCount;

            remainingItemCount -= _availability.ConsumePlanned(resultItemId, remainingItemCount);

            // fork: upstream also asked for SkipIfEnough here, which tied held final crafts to the precraft rule for no reason of its own
            if (_list.SkipFinalIfEnough && _consumeFinalAvailability)
            {
                var onlyHq = _list.OnlyHqCountsFor(item.RecipeId);
                var consumedInventory = _availability.ConsumeInventory(resultItemId, remainingItemCount, onlyHq);
                remainingItemCount -= consumedInventory;
                var consumedRetainers = 0;
                if (_useRetainers)
                {
                    consumedRetainers = _availability.ConsumeRetainers(resultItemId, remainingItemCount, onlyHq);
                    remainingItemCount -= consumedRetainers;
                }


                if (consumedRetainers > 0)
                    AddCount(_plan.RetainerConsumedCraftables, resultItemId, consumedRetainers);
            }

            if (remainingItemCount <= 0)
                return;

            var gate = Gate(recipe);
            switch (GateRules.Decide(gate, _list.EffectiveBuyFinals, MaterialSourceClassifier.IsSoldForGil(resultItemId)))
            {
                case Gated.Buy:
                    AddCount(_bought, resultItemId, remainingItemCount);
                    _plan.BoughtInstead.Add(new PlanSourced(resultItemId, remainingItemCount, gate?.Need));
                    return;
                case Gated.Block:
                    _plan.OutOfReach.Add(new PlanGated(item.RecipeId, Name(recipe), gate!.Value.Need, true));
                    return;
                case Gated.Defer:
                    _plan.Deferred.Add(new PlanGated(item.RecipeId, Name(recipe), gate!.Value.Need, true));
                    break;
            }

            var craftCount = DivideRoundUp(remainingItemCount, (int)recipe.AmountResult);
            AddRecipe(_plan.OriginalRecipes, item.RecipeId, craftCount, true);
            AddRecipe(_plan.Recipes, item.RecipeId, craftCount, true);

            var surplus = craftCount * (int)recipe.AmountResult - remainingItemCount;
            _availability.AddPlanned(resultItemId, surplus);

            PlanIngredients(recipe, craftCount, true);
        }

        private void PlanIngredients(Recipe recipe, int craftCount, bool isOriginalRecipe)
        {
            var qualityPolicy = ResolveQualityPolicy(recipe, isOriginalRecipe);
            foreach (var (itemId, _) in RecipeManager.GetIngredients(recipe))
            {
                var itemDemand = qualityPolicy.GetDemand(itemId).Scale(craftCount);
                AddDemand(_plan.IngredientDemands, itemId, itemDemand);
                var subRecipe = ResolveSubRecipe(itemId, recipe);
                if (!subRecipe.HasValue)
                {
                    AddCount(_plan.Materials, itemId, itemDemand.Total);
                    continue;
                }

                AddCount(_plan.Precrafts, itemId, itemDemand.Total);
                PlanPrecraftDemand(subRecipe.Value, itemDemand);
            }
        }

        private static string Name(Recipe recipe)
            => recipe.ItemResult.Value.Name.ExtractText();

        private Gate? Gate(Recipe recipe)
        {
            if (_gates.TryGetValue(recipe.RowId, out var gate))
                return gate;

            try
            {
                gate = CraftingGameInterop.GateFor(recipe);
            }
            catch (Exception e)
            {
                GatherBuddy.Log.Warning($"[CraftingListPlanner] the gates of recipe {recipe.RowId} could not be read: {e.Message}");
                gate = null;
            }

            return _gates[recipe.RowId] = gate;
        }

        private void PlanPrecraftDemand(Recipe recipe, IngredientQualityDemand itemDemand)
        {
            var resultItemId = recipe.ItemResult.RowId;
            var remainingDemand = _availability.ConsumePlanned(resultItemId, itemDemand);

            if (_list.SkipIfEnough && _consumeIntermediateAvailability)
            {
                remainingDemand = _availability.ConsumeInventory(resultItemId, remainingDemand);

                if (_useRetainers)
                {
                    var remainingAfterRetainers = _availability.ConsumeRetainers(resultItemId, remainingDemand);
                    var fromRetainers = remainingDemand.Total - remainingAfterRetainers.Total;
                    remainingDemand = remainingAfterRetainers;
                    AddCount(_plan.RetainerConsumedCraftables, resultItemId, fromRetainers);
                }
            }

            if (remainingDemand.Total <= 0)
                return;

            var gate = Gate(recipe);
            switch (GateRules.Decide(gate, _list.EffectiveBuyPrecrafts, MaterialSourceClassifier.IsSoldForGil(resultItemId)))
            {
                case Gated.Buy:
                    _plan.Precrafts[resultItemId] = Math.Max(0, _plan.Precrafts.GetValueOrDefault(resultItemId) - remainingDemand.Total);
                    AddCount(_bought, resultItemId, remainingDemand.Total);
                    _plan.BoughtInstead.Add(new PlanSourced(resultItemId, remainingDemand.Total, gate?.Need));
                    return;
                case Gated.Block:
                    if (_original is { } original)
                        _plan.Blocked.Add(new PlanGated(original.RecipeId, RecipeManager.GetRecipe(original.RecipeId) is { } final ? Name(final) : $"recipe {original.RecipeId}",
                            GateRules.Blocked(Name(recipe), gate!.Value.Need), true));
                    return;
                case Gated.Defer:
                    _plan.Deferred.Add(new PlanGated(recipe.RowId, Name(recipe), gate!.Value.Need, false));
                    break;
            }

            var craftCount = DivideRoundUp(remainingDemand.Total, (int)recipe.AmountResult);
            AddRecipe(_plan.Recipes, recipe.RowId, craftCount, false);

            var surplus = craftCount * (int)recipe.AmountResult - remainingDemand.Total;
            var qualityPolicy = ResolveQualityPolicy(recipe, false);
            var outputQuality = CraftingQualityPolicyResolver.ResolvePlannedOutputQuality(recipe, qualityPolicy, remainingDemand);
            _availability.AddPlanned(resultItemId, surplus, outputQuality);

            PlanIngredients(recipe, craftCount, false);
        }

        private Recipe? ResolveSubRecipe(uint itemId, Recipe parent)
        {
            if (_list.PrecraftRecipeOverrides.TryGetValue(itemId, out var overrideRecipeId))
            {
                var overrideRecipe = RecipeManager.GetRecipe(overrideRecipeId);
                if (overrideRecipe.HasValue)
                    return overrideRecipe;
            }
            return RecipeManager.GetRecipeForItem(itemId, parent);
        }

        private CraftingQualityPolicy ResolveQualityPolicy(Recipe recipe, bool isOriginalRecipe)
        {
            var settings = isOriginalRecipe
                ? _originalRecipeLookup.GetValueOrDefault(recipe.RowId)?.CraftSettings
                : _list.PrecraftCraftSettings.GetValueOrDefault(recipe.RowId);
            var overrideMode = _list.GetQualityOverrideMode(recipe, isOriginalRecipe);
            var effectiveSettings = CraftingQualityPolicyResolver.BuildEffectiveSettings(recipe, settings, _list.UseAllHQ);
            return CraftingQualityPolicyResolver.Resolve(recipe, effectiveSettings, overrideMode);
        }

        private static void AddRecipe(List<CraftingListItem> target, uint recipeId, int craftCount, bool isOriginalRecipe)
        {
            if (craftCount <= 0)
                return;

            var existing = target.FirstOrDefault(item => item.RecipeId == recipeId && item.IsOriginalRecipe == isOriginalRecipe);
            if (existing != null)
            {
                existing.Quantity += craftCount;
                return;
            }

            target.Add(new CraftingListItem(recipeId, craftCount)
            {
                IsOriginalRecipe = isOriginalRecipe,
            });
        }

        private static void AddCount(Dictionary<uint, int> target, uint itemId, int amount)
        {
            if (amount <= 0)
                return;

            target[itemId] = target.GetValueOrDefault(itemId) + amount;
        }

        private static void AddDemand(Dictionary<uint, IngredientQualityDemand> target, uint itemId, IngredientQualityDemand demand)
        {
            if (demand.Total <= 0)
                return;

            target[itemId] = target.TryGetValue(itemId, out var existing)
                ? existing.Add(demand)
                : demand;
        }

        private static int DivideRoundUp(int value, int divisor)
            => (int)Math.Ceiling((double)value / divisor);
    }

    private sealed class AvailabilityLedger
    {
        private readonly bool _useRetainers;
        private readonly Dictionary<uint, PlannedAvailability> _plannedAvailable = new();
        private readonly Dictionary<uint, (int NQ, int HQ)> _inventoryAvailable = new();
        private readonly Dictionary<uint, (int NQ, int HQ)> _retainerAvailable = new();
        private readonly Dictionary<uint, int> _inventoryTaken = new();

        public int InventoryTaken(uint itemId)
            => _inventoryTaken.GetValueOrDefault(itemId);

        public AvailabilityLedger(bool useRetainers)
        {
            _useRetainers = useRetainers;
        }

        public int ConsumePlanned(uint itemId, int requested)
        {
            if (requested <= 0)
                return 0;

            var available = _plannedAvailable.GetValueOrDefault(itemId);
            var consumed = Math.Min(requested, available.Total);
            if (consumed <= 0)
                return 0;

            var remainingToConsume = consumed;
            var consumeUnknown = Math.Min(remainingToConsume, available.Unknown);
            remainingToConsume -= consumeUnknown;
            var consumeNQ = Math.Min(remainingToConsume, available.NQ);
            remainingToConsume -= consumeNQ;
            var consumeHQ = Math.Min(remainingToConsume, available.HQ);
            _plannedAvailable[itemId] = new PlannedAvailability(
                available.Unknown - consumeUnknown,
                available.NQ - consumeNQ,
                available.HQ - consumeHQ);
            return consumed;
        }

        public int ConsumeInventory(uint itemId, int requested, bool onlyHq = false)
        {
            var taken = ConsumeTotal(_inventoryAvailable, itemId, requested, GetInventorySplitCounts, onlyHq);
            _inventoryTaken[itemId] = InventoryTaken(itemId) + taken;
            return taken;
        }

        public IngredientQualityDemand ConsumePlanned(uint itemId, IngredientQualityDemand demand)
        {
            if (demand.Total <= 0)
                return demand;

            var available = _plannedAvailable.GetValueOrDefault(itemId);
            if (available.Total <= 0)
                return demand;

            var remaining = demand.ConsumeSplit(available.NQ, available.HQ, out var consumedNQ, out var consumedHQ);
            remaining = remaining.ConsumeUnknownQuality(available.Unknown, out var consumedUnknown);
            _plannedAvailable[itemId] = new PlannedAvailability(
                available.Unknown - consumedUnknown,
                available.NQ - consumedNQ,
                available.HQ - consumedHQ);
            return remaining;
        }

        public IngredientQualityDemand ConsumeInventory(uint itemId, IngredientQualityDemand demand)
        {
            var left = ConsumeSplit(_inventoryAvailable, itemId, demand, GetInventorySplitCounts);
            _inventoryTaken[itemId] = InventoryTaken(itemId) + demand.Total - left.Total;
            return left;
        }

        public IngredientQualityDemand ConsumeRetainers(uint itemId, IngredientQualityDemand demand)
            => _useRetainers
                ? ConsumeSplit(_retainerAvailable, itemId, demand, GetRetainerSplitCounts)
                : demand;

        public int ConsumeRetainers(uint itemId, int requested, bool onlyHq = false)
            => _useRetainers
                ? ConsumeTotal(_retainerAvailable, itemId, requested, GetRetainerSplitCounts, onlyHq)
                : 0;

        public void AddPlanned(uint itemId, int amount, PlannedOutputQuality outputQuality = PlannedOutputQuality.Unknown)
        {
            if (amount <= 0)
                return;

            var available = _plannedAvailable.GetValueOrDefault(itemId);
            _plannedAvailable[itemId] = outputQuality switch
            {
                PlannedOutputQuality.NQ => available with { NQ = available.NQ + amount },
                PlannedOutputQuality.HQ => available with { HQ = available.HQ + amount },
                _ => available with { Unknown = available.Unknown + amount },
            };
        }

        private readonly record struct PlannedAvailability(int Unknown, int NQ, int HQ)
        {
            public int Total => Unknown + NQ + HQ;
        }

        private static int ConsumeTotal(
            Dictionary<uint, (int NQ, int HQ)> ledger,
            uint itemId,
            int requested,
            Func<uint, (int NQ, int HQ)> valueFactory,
            bool onlyHq = false)
        {
            if (requested <= 0)
                return 0;

            if (!ledger.TryGetValue(itemId, out var available))
            {
                available = valueFactory(itemId);
                ledger[itemId] = available;
            }

            var (consumed, remainingNQ, remainingHQ) = ForkLogic.QueueRules.TakeHeld(available.NQ, available.HQ, requested, onlyHq);
            ledger[itemId] = (remainingNQ, remainingHQ);
            return consumed;
        }

        private static IngredientQualityDemand ConsumeSplit(
            Dictionary<uint, (int NQ, int HQ)> ledger,
            uint itemId,
            IngredientQualityDemand demand,
            Func<uint, (int NQ, int HQ)> valueFactory)
        {
            if (demand.Total <= 0)
                return demand;

            if (!ledger.TryGetValue(itemId, out var available))
            {
                available = valueFactory(itemId);
                ledger[itemId] = available;
            }

            if (available.NQ <= 0 && available.HQ <= 0)
                return demand;

            var remaining = demand.ConsumeSplit(available.NQ, available.HQ, out var consumedNQ, out var consumedHQ);
            ledger[itemId] = (Math.Max(0, available.NQ - consumedNQ), Math.Max(0, available.HQ - consumedHQ));
            return remaining;
        }

        private static unsafe (int NQ, int HQ) GetInventorySplitCounts(uint itemId)
        {
            try
            {
                var inventory = InventoryManager.Instance();
                if (inventory == null)
                    return (0, 0);

                return (
                    (int)inventory->GetInventoryItemCount(itemId, false, false, false),
                    (int)inventory->GetInventoryItemCount(itemId, true, false, false));
            }
            catch
            {
                return (0, 0);
            }
        }

        private static (int NQ, int HQ) GetRetainerSplitCounts(uint itemId)
        {
            try
            {
                var snapshot = RetainerItemQuery.CreateSnapshot(new[] { itemId });
                return (snapshot.GetCountNQ(itemId), snapshot.GetCountHQ(itemId));
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}
