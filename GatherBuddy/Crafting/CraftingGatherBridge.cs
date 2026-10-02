using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Automation;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.AutoGather.Collectables;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using Lumina.Excel.Sheets;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;

namespace GatherBuddy.Crafting;

public static class CraftingGatherBridge
{
    private static AutoGatherList? _gatherList;
    private static global::GatherBuddy.GatherBuddy? _plugin;
    private static uint _recipeIdToCraft = 0;
    private static bool _waitingForGatherComplete = false;
    private static bool _homeBeforeCrafting;
    private static DateTime? _homeBeforeCraftingAt;
    private static DateTime _homeBeforeCraftingUntil;
    private static DateTime _jobSwitchTime = DateTime.MinValue;
    private static bool _waitingForJobSwitch = false;
    private static CraftingQueueProcessor? _queueProcessor = null;
    private static CraftingExecutionPlan? _activeExecutionPlan = null;
    private static bool _isQueueMode = false;
    private static KeepRules.Run? _keepRun;
    private static List<AutoGatherList> _disabledGatherLists = new();
    private static int? _ephemeralListId = null;
    private static bool _waitingForCollectables = false;
    private static bool _collectablesStartPending = false;
    private static DateTime _nextCollectablesRetry = DateTime.MinValue;
    private static DateTime _lastCollectablesWaitLog = DateTime.MinValue;
    private static DateTime _lastCollectablesExitAttempt = DateTime.MinValue;
    private static DateTime _lastCollectablesHardFailLog = DateTime.MinValue;
    private static bool _waitingForCollectablesHomeReturn = false;
    private static bool _collectablesHomeReturnStarted = false;
    private static Dictionary<uint, int>? _afterBuying;
    private static bool _buyStarted;
    private static bool _buyingPaused;
    private static DateTime _buyStartBy;
    private static readonly List<string> _noVendor = new();

    public static bool PreserveListOnDisable { get; set; } = false;

    public static void Initialize(global::GatherBuddy.GatherBuddy plugin)
    {
        _plugin = plugin;
    }

    // fork: a run's plan and marks belong to the character that started it, so logging out ends it
    internal static void HookLogout()
        => Dalamud.ClientState.Logout += OnLogout;

    internal static void UnhookLogout()
        => Dalamud.ClientState.Logout -= OnLogout;

    private static void OnLogout(int type, int code)
    {
        if (!_isQueueMode)
            return;

        ForkTrace.Info("crafting run stopped by the logout");
        StopQueue();
    }

    private static int RoundUpToBatchSize(int quantity, int batchSize)
        => batchSize <= 1
            ? quantity
            : (int)Math.Ceiling((double)quantity / batchSize) * batchSize;

    public static void BindCollectableManager(CollectableManager manager)
    {
        manager.OnFinishCollecting -= OnCollectablesFinished;
        manager.OnError -= OnCollectablesError;
        manager.OnFinishCollecting += OnCollectablesFinished;
        manager.OnError += OnCollectablesError;
    }
    
    public static uint RecipeToCraft => _recipeIdToCraft;
    public static bool WaitingForGatherComplete => _waitingForGatherComplete;
    public static bool IsQueueMode => _isQueueMode;
    public static string? RunningListName => _isQueueMode ? _activeExecutionPlan?.ListName : null;
    internal static KeepRules.Run? KeepRun => _keepRun;
    internal static AutoGatherListsManager? ListsManager => _plugin?.AutoGatherListsManager;

    // fork: a gathering part that stops short pauses the run there, so Resume gathers the rest instead of crafting short
    public static bool PauseRunForGatheringStop(string? reason)
    {
        if (!_isQueueMode || _queueProcessor is not { CurrentState: CraftingQueueProcessor.QueueState.WaitingForGather } || _gatherList == null)
            return false;

        var missing = _gatherList.Items
            .Select(item => (Item: item, Need: (int)(_gatherList.Quantities.TryGetValue(item, out var q) ? q : 0), Have: GetInventoryCount(item.ItemId)))
            .Where(m => m.Have < m.Need)
            .Select(m => $"{m.Item.Name[GatherBuddy.Language]} x{m.Need - m.Have}")
            .ToList();
        _queueProcessor.PauseGatheringStoppedShort(reason, missing);
        return true;
    }
    
    public static AutoGatherList? GetTemporaryGatherList() => _gatherList;
    public static CraftingExecutionPlan? GetActiveExecutionPlan()
        => _activeExecutionPlan;

    public static CraftingExecutionPlan? GetActiveExecutionPlan(int listId)
        => _activeExecutionPlan != null && _activeExecutionPlan.MatchesList(listId)
            ? _activeExecutionPlan
            : null;
    
    public static void DeleteTemporaryGatherList()
    {
        if (_gatherList != null && _plugin != null)
        {
            try
            {
                _plugin.AutoGatherListsManager.DeleteList(_gatherList);
                GatherBuddy.Log.Debug($"[CraftingGatherBridge] Deleted temporary gather list: {_gatherList.Name}");
                _gatherList = null;
            }
            catch (Exception ex)
            {
                GatherBuddy.Log.Warning($"[CraftingGatherBridge] Failed to delete temporary gather list: {ex.Message}");
            }
        }
    }
    
    public static void CreatePersistentGatherList(string listName, Dictionary<uint, int> materials)
    {
        if (_plugin == null)
        {
            GatherBuddy.Log.Warning("[CraftingGatherBridge] Cannot create gather list: plugin not initialized");
            return;
        }

        try
        {
            var gatherList = new AutoGatherList()
            {
                Name = listName,
                Enabled = false
            };

            foreach (var (itemId, quantity) in materials)
            {
                var gatherQuantity = GetCraftingGatherTargetQuantity(itemId, quantity, out var gatherItemId);
                if (gatherQuantity <= 0)
                    continue;

                if (GatherBuddy.GameData.Gatherables.TryGetValue(gatherItemId, out var gatherable))
                    gatherList.Add(gatherable, (uint)gatherQuantity);
                else if (GatherBuddy.GameData.Fishes.TryGetValue(gatherItemId, out var fish))
                    gatherList.Add(fish, (uint)gatherQuantity);
                else
                    GatherBuddy.Log.Debug($"[CraftingGatherBridge] Item {gatherItemId} not found in gatherables or fish, skipping");
            }

            if (gatherList.Items.Count > 0)
            {
                _plugin.AutoGatherListsManager.AddList(gatherList);
                _plugin.AutoGatherListsManager.SetActiveItems();
                GatherBuddy.Log.Information($"[CraftingGatherBridge] Created gather list '{listName}' with {gatherList.Items.Count} items.");
            }
            else
            {
                GatherBuddy.Log.Warning($"[CraftingGatherBridge] No gatherable items found for list '{listName}'.");
            }
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Error($"[CraftingGatherBridge] Failed to create gather list '{listName}': {ex.Message}");
        }
    }

    private static int GetCraftingGatherTargetQuantity(uint itemId, int quantity, out uint gatherItemId)
    {
        gatherItemId = itemId;
        if (quantity <= 0)
            return 0;

        if (!AutoGather.Helpers.Diadem.ApprovedToRawItemIds.TryGetValue(itemId, out var rawItemId))
            return quantity;
        var approvedDeficit = Math.Max(0, quantity - GetInventoryCount(itemId));
        if (approvedDeficit <= 0)
            return 0;

        gatherItemId = rawItemId;
        var batchSize = AutoGather.Helpers.Diadem.ApprovedInspectionBatchSizes.TryGetValue(itemId, out var configuredBatchSize) && configuredBatchSize > 0
            ? (int)configuredBatchSize
            : 1;
        return RoundUpToBatchSize(approvedDeficit, batchSize);
    }
    
    public static void Update()
    {
        if (_keepRun == null && KeepMarks.NeededCount > 0)
            KeepMarks.DropNeeded();

        if (_isQueueMode && _queueProcessor != null)
        {
            UpdateCollectablesHomeReturnBeforeResume();
            TryStartCollectablesInterruption();
            if (_homeBeforeCrafting)
                UpdateHomeBeforeCrafting();
            UpdateBuying();
            if (_queueProcessor == null)
                return;
            _queueProcessor.Update();
            
            if (_queueProcessor.CurrentState == CraftingQueueProcessor.QueueState.Complete && !_queueProcessor.HasPendingTasks())
            {
                GatherBuddy.Log.Information("[CraftingGatherBridge] All completion tasks done, cleaning up");
                RestoreDisabledGatherLists();
                GatherBuddy.CraftingStatusWindow?.SetQueueProcessor(null);
                _queueProcessor = null;
                _activeExecutionPlan = null;
                _isQueueMode = false;
                _keepRun = null;
                ClearBuying();

                if (_ephemeralListId.HasValue)
                {
                    GatherBuddy.Log.Information($"[CraftingGatherBridge] Deleting ephemeral crafting list {_ephemeralListId.Value}");
                    GatherBuddy.CraftingListManager.DeleteList(_ephemeralListId.Value);
                    _ephemeralListId = null;
                }
            }
        }
        
        if (!_waitingForJobSwitch)
            return;
        
        var timeSinceSwitch = (DateTime.Now - _jobSwitchTime).TotalSeconds;
        if (timeSinceSwitch >= 2)
        {
            GatherBuddy.Log.Debug($"[CraftingGatherBridge] Job switch wait complete, retrying gather-to-craft");
            _waitingForJobSwitch = false;
            _jobSwitchTime = DateTime.MinValue;
            OnGatherComplete();
        }
    }
    
    public static void OnCraftFinished(Recipe? recipe, bool cancelled)
    {
        if (_isQueueMode && _queueProcessor != null)
        {
            _queueProcessor.OnCraftFinished(recipe, cancelled);
        }
    }

    public static void StartGatherAndCraft(uint recipeId, Dictionary<uint, int> missing)
    {
        _isQueueMode = false;
        _recipeIdToCraft = recipeId;
        _waitingForGatherComplete = true;
        CreateGatherListForMissingIngredients(missing);
    }
    
    public static void StartQueueCraftAndGather(CraftingExecutionPlan executionPlan, CraftingListConsumableSettings? listConsumables = null, int? ephemeralListId = null)
    {
        RunChat.Begin(TextRules.KindLabel(TextRules.Crafting, executionPlan.ListName));
        _isQueueMode = true;
        _ephemeralListId = ephemeralListId;
        _gatheredInstead.Clear();
        _activeExecutionPlan = executionPlan;
        _keepRun = KeepMarks.BeginRun(executionPlan.ListName, "crafted", CraftingTargets(executionPlan));
        ResetCollectablesInterruptionState();
        _lastCollectablesHardFailLog = DateTime.MinValue;
        _queueProcessor = new CraftingQueueProcessor();
        _queueProcessor.QueueCompleted += OnQueueCompleted;
        _waitingForGatherComplete = true;
        GatherBuddy.Log.Information($"[CraftingGatherBridge] Starting queue automation with {executionPlan.QueueView.Count} recipes, retainerRestock={executionPlan.RetainerRestock}");
        ForkTrace.Info(DescribePlan(executionPlan));
        _queueProcessor.StartQueue(executionPlan, listConsumables, GatherBuddy.RaphaelSolveCoordinator);
        var hasRetainerWork = executionPlan.RetainerRestock && AllaganTools.Enabled
            && (executionPlan.Materials.Count > 0 || executionPlan.RetainerConsumedCraftables.Count > 0);
        if (!hasRetainerWork)
            BeginGathering(executionPlan.Materials);

        GatherBuddy.CraftingStatusWindow?.SetQueueProcessor(_queueProcessor);
    }
    
    // the list's own recipes, even ones the run skips because enough is already held, less any left out for being in the crafting log
    private static IEnumerable<(uint ItemId, int Target)> CraftingTargets(CraftingExecutionPlan plan)
    {
        var list  = GatherBuddy.CraftingListManager.GetListByID(plan.ListId);
        var items = list?.Recipes.Where(i => !i.Options.Skipping && !(list.SkipCraftedRecipes && QuestManager.IsRecipeComplete(i.RecipeId))).ToList()
         ?? plan.OriginalRecipesView.ToList();
        foreach (var item in items)
            if (RecipeManager.GetRecipe(item.RecipeId) is { } recipe)
                yield return (recipe.ItemResult.RowId, item.Quantity * (int)recipe.AmountResult);
    }

    private static string DescribePlan(CraftingExecutionPlan plan)
    {
        static string Line(CraftingListItem item)
        {
            var recipe = RecipeManager.GetRecipe(item.RecipeId);
            var name   = recipe?.ItemResult.Value.Name.ExtractText() ?? $"recipe {item.RecipeId}";
            var job    = recipe == null ? "?" : Dalamud.GameData.GetExcelSheet<ClassJob>()?.GetRowOrDefault(8u + recipe.Value.CraftType.RowId)?.Abbreviation.ExtractText() ?? "?";
            return $"{name} x{item.Quantity} {job}{(item.IsOriginalRecipe ? "" : " (precraft)")}{(item.Options.Skipping ? " (skipped)" : "")}";
        }

        static string Items(IReadOnlyDictionary<uint, int> items)
            => items.Count == 0 ? "none" : string.Join(", ", items.Select(kv => $"{ForkTrace.Named(kv.Key)} x{kv.Value}"));

        return $"plan for '{plan.ListName}': queue [{string.Join("; ", plan.QueueView.Select(Line))}]"
          + $" | to source {Items(plan.MaterialsView)} | precrafts {Items(plan.PrecraftsView)}"
          + $" | from retainers {Items(plan.RetainerConsumedCraftablesView)}"
          + $" | skipIfEnough={plan.SkipIfEnough} skipFinalIfEnough={plan.SkipFinalIfEnough} retainerRestock={plan.RetainerRestock}";
    }

    public static bool IsBuying
        => _afterBuying != null;

    internal static bool BuyingHeld
        => _afterBuying != null && _buyingPaused;

    // fork: a crafting run first buys what it can neither gather nor fish but a gil vendor sells, then gathers the rest
    public static void BeginGathering(Dictionary<uint, int> materials)
    {
        if (!_isQueueMode || !GatherBuddy.Config.VulcanBuyBeforeGathering || GatherBuddy.VendorBuyListManager == null)
        {
            CreateGatherListForMissingIngredients(materials);
            return;
        }

        _afterBuying  = materials;
        _buyingPaused = false;
        StartBuying();
    }

    private static void StartBuying()
    {
        _buyStarted = false;
        _buyStartBy = DateTime.Now.AddMinutes(1);
        _noVendor.Clear();
        TryStartBuying();
    }

    private static bool InsteadOfGathering
        => _activeExecutionPlan?.BuyInsteadOfGathering == true;

    private static readonly List<string> _gatheredInstead = new();

    internal static IReadOnlyList<string> GatheredInstead
        => _gatheredInstead;

    private static List<(uint ItemId, uint Target, int Missing)> ToBuy(bool insteadOfGathering)
        => PurchaseRules.BeforeGathering(_afterBuying!,
            PurchaseRules.Gathered(insteadOfGathering, id => PurchaseRules.GatheredWithoutWaiting(
                GatherBuddy.GameData.Gatherables.TryGetValue(id, out var node) ? node.InternalLocationId : null,
                GatherBuddy.GameData.Fishes.TryGetValue(id, out var fish) ? fish.InternalLocationId : null,
                AutoGather.Helpers.Diadem.ApprovedToRawItemIds.ContainsKey(id))),
            MaterialSourceClassifier.IsSoldForGil, VendorBuyListManager.GetCurrentInventoryAndArmoryCount);

    private static List<string> Named(IEnumerable<(uint ItemId, uint Target, int Missing)> targets)
        => targets.Select(t => $"{ForkTrace.ItemName(t.ItemId)} x{t.Missing}").ToList();

    private static void TryStartBuying()
    {
        var targets = ToBuy(InsteadOfGathering);
        if (targets.Count == 0)
        {
            FinishBuying();
            return;
        }

        if (!VendorAutomationRequirements.IsAvailable)
        {
            StopBuyingShort(VendorAutomationRequirements.UnavailableStatusText, Named(targets));
            return;
        }


        var requests = targets.Select(t => new VendorBuyListManager.VendorTargetRequest(t.ItemId, t.Target)).ToList();
        var result   = GatherBuddy.VendorBuyListManager!.StartForRun(_activeExecutionPlan?.ListName ?? "crafting run", requests, out var noVendor);
        if (result is VendorBuyListManager.StartResult.Started or VendorBuyListManager.StartResult.WaitingForPreviousInteraction)
        {
            _buyStarted = true;
            _noVendor.AddRange(noVendor.Select(id => $"{ForkTrace.ItemName(id)} (no vendor GatherBuddy can walk to)"));
            var buying = Named(targets.Where(t => !noVendor.Contains(t.ItemId)));
            ForkTrace.Info($"buy before gathering: {string.Join(", ", buying)}{(noVendor.Count == 0 ? "" : $"; no vendor for {string.Join(", ", noVendor.Select(ForkTrace.Named))}")}");
            ForkChat.List(PurchaseRules.BuyingHeader(InsteadOfGathering), buying, tone: Communicator.Tone.Info);
            return;
        }

        if (result is VendorBuyListManager.StartResult.NoPendingEntries)
        {
            FinishBuying();
            return;
        }

        var waiting = result is VendorBuyListManager.StartResult.AlreadyRunning or VendorBuyListManager.StartResult.VendorDataLoading
            or VendorBuyListManager.StartResult.LocationDataLoading;
        if (waiting && DateTime.Now < _buyStartBy)
            return;

        if (result is VendorBuyListManager.StartResult.Empty && PurchaseRules.GatherInstead(InsteadOfGathering, ToBuy(false).Count))
        {
            ForkTrace.Info($"buy before gathering: no vendor GatherBuddy can walk to sells {string.Join(", ", Named(targets))}; gathering them instead");
            Communicator.PrintRun(PurchaseRules.GatheringInstead(targets.Count), tone: Communicator.Tone.Info);
            _gatheredInstead.AddRange(targets.Select(t => $"{ForkTrace.ItemName(t.ItemId)} (no vendor GatherBuddy can walk to)"));
            FinishBuying();
            return;
        }

        StopBuyingShort(result switch
        {
            VendorBuyListManager.StartResult.Empty          => "no vendor GatherBuddy can walk to sells what is missing",
            VendorBuyListManager.StartResult.AlreadyRunning => "another vendor run was still going after a minute",
            _                                               => "the vendor data did not load within a minute",
        }, Named(targets));
    }

    private static void UpdateBuying()
    {
        if (_afterBuying == null || _buyingPaused || _queueProcessor is not { Paused: false } || GatherBuddy.VendorBuyListManager is not { } manager)
            return;

        if (!_buyStarted)
        {
            TryStartBuying();
            return;
        }

        if (manager.RunPurchase == VendorBuyListManager.RunPurchaseOutcome.Running || manager.IsBusy)
            return;

        var (outcome, detail, notBought) = manager.TakeRunPurchase();
        _queueProcessor?.NoteSetOut(manager.SetOut);
        notBought.AddRange(_noVendor);
        ForkTrace.Info($"buy before gathering: {outcome}{(detail.Length > 0 ? $" ({detail})" : "")}; not bought: {(notBought.Count == 0 ? "none" : string.Join(", ", notBought))}");
        switch (outcome)
        {
            case VendorBuyListManager.RunPurchaseOutcome.Stopped:
                Communicator.PrintRun(PurchaseRules.StoppedWhileBuying);
                StopQueue();
                return;
            case VendorBuyListManager.RunPurchaseOutcome.Finished when notBought.Count == 0:
                Communicator.PrintRun(PurchaseRules.AllBought, tone: Communicator.Tone.Info);
                FinishBuying();
                return;
            case VendorBuyListManager.RunPurchaseOutcome.Finished when PurchaseRules.GatherInstead(InsteadOfGathering, ToBuy(false).Count):
                Communicator.PrintRun(PurchaseRules.GatheringInstead(notBought.Count), tone: Communicator.Tone.Info);
                ForkChat.List("Gathering instead:", notBought, 12, tone: Communicator.Tone.Info);
                _gatheredInstead.AddRange(notBought);
                FinishBuying();
                return;
            case VendorBuyListManager.RunPurchaseOutcome.Finished:
                StopBuyingShort(PurchaseRules.NotBought(notBought.Count), notBought);
                return;
            case VendorBuyListManager.RunPurchaseOutcome.BagsFull:
                StopBuyingShort(PurchaseRules.BagsFull, Named(ToBuy(InsteadOfGathering)));
                return;
            default:
                StopBuyingShort(detail.Length > 0 ? detail : "the vendor run ended early", Named(ToBuy(InsteadOfGathering)));
                return;
        }
    }

    // fork: like the gathering part, a purchase that falls short pauses the run instead of crafting short
    private static void StopBuyingShort(string why, IReadOnlyList<string> missing)
    {
        _buyingPaused = true;
        _buyStarted   = false;
        _noVendor.Clear();
        _queueProcessor?.PauseStoppedShort(TextRules.BuyList, why, missing);
    }

    private static void FinishBuying()
    {
        var materials = _afterBuying;
        ClearBuying();
        if (materials != null)
            CreateGatherListForMissingIngredients(materials);
    }

    private static void ClearBuying()
    {
        _afterBuying  = null;
        _buyStarted   = false;
        _buyingPaused = false;
        _noVendor.Clear();
    }

    internal static void PauseBuying()
    {
        if (_afterBuying == null || _buyingPaused)
            return;

        _buyingPaused = true;
        GatherBuddy.VendorBuyListManager?.CancelRunPurchase();
    }

    internal static bool ResumeBuying()
    {
        if (!BuyingHeld)
            return false;

        _buyingPaused = false;
        StartBuying();
        return true;
    }

    internal static bool SkipBuying()
    {
        if (!BuyingHeld)
            return false;

        ForkTrace.Info("buy before gathering: skipped by the player, gathering next");
        FinishBuying();
        return true;
    }

    public static void CreateGatherListForMissingIngredients(Dictionary<uint, int> missing)
    {
        try
        {
            if (_plugin != null)
            {
                var enabledLists = _plugin.AutoGatherListsManager.Lists.Where(l => l.Enabled && !l.Fallback).ToList();
                if (enabledLists.Count > 0)
                {
                    _disabledGatherLists.Clear();
                    foreach (var existingList in enabledLists)
                    {
                        existingList.Enabled = false;
                        _disabledGatherLists.Add(existingList);
                        _plugin.AutoGatherListsManager.PausedByRun.Add(existingList);
                        GatherBuddy.Log.Debug($"[CraftingGatherBridge] Disabled gather list '{existingList.Name}' before starting craft gather");
                    }
                    _plugin.AutoGatherListsManager.Save();
                }
            }

            _gatherList = new AutoGatherList()
            {
                Name = AutoGatherListsManager.TemporaryListName,
                Enabled = true,
                UsesRetainerInventory = false
            };

            foreach (var (itemId, quantity) in missing)
            {
                var gatherQuantity = GetCraftingGatherTargetQuantity(itemId, quantity, out var gatherItemId);
                if (gatherQuantity <= 0)
                    continue;
                
                if (GatherBuddy.GameData.Gatherables.TryGetValue(gatherItemId, out var gatherable))
                    _gatherList.Add(gatherable, (uint)gatherQuantity);
                else if (GatherBuddy.GameData.Fishes.TryGetValue(gatherItemId, out var fish))
                    _gatherList.Add(fish, (uint)gatherQuantity);
                else
                    GatherBuddy.Log.Debug($"[CraftingGatherBridge] Item {gatherItemId} not found in gatherables or fish, skipping");
            }

            ForkTrace.Info($"gather bridge list: {(_gatherList.Items.Count == 0 ? "empty" : string.Join(", ", _gatherList.Items.Select(i => $"{i.Name[GatherBuddy.Language]} x{_gatherList.Quantities[i]}")))}"
              + $"; paused lists: {(_disabledGatherLists.Count == 0 ? "none" : string.Join(", ", _disabledGatherLists.Select(l => l.Name)))}");
            if (_gatherList.Items.Count > 0 && _plugin != null)
            {
                _plugin.AutoGatherListsManager.AddList(_gatherList);
                _plugin.AutoGatherListsManager.SetActiveItems();

                if (IsGatheringComplete())
                {
                    GatherBuddy.Log.Debug($"[CraftingGatherBridge] Gather list created but all items already in inventory, proceeding directly to crafting");
                    CraftAfterGoingHome();
                }
                else
                {
                    _waitingForGatherComplete = true;
                    GatherBuddy.AutoGather.Enabled = true;
                    GatherBuddy.Log.Information($"Created crafting gather list with {_gatherList.Items.Count} items. Starting auto-gather.");
                }
            }
            else
            {
                GatherBuddy.Log.Debug($"[CraftingGatherBridge] No gatherable items needed, proceeding directly to crafting");
                CraftAfterGoingHome();
            }
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Error($"Failed to create gather list: {ex.Message}");
        }
    }
    
    // fork: the gathering part's end is what takes a run home before crafting; with nothing to gather, crafting would start wherever the
    // retainer or buy part left the character
    internal static void NoteSetOut()
        => _queueProcessor?.NoteSetOut(true);

    private static void CraftAfterGoingHome()
    {
        if (_queueProcessor?.SetOut != true || !GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone || PauseHome.AtHome())
        {
            OnGatherComplete();
            return;
        }

        ForkTrace.Info("crafting run: nothing to gather, so it goes home before crafting");
        _waitingForGatherComplete = false;
        _homeBeforeCrafting       = true;
        _homeBeforeCraftingAt     = null;
        _homeBeforeCraftingUntil  = DateTime.Now.AddMinutes(3);
    }

    private static void UpdateHomeBeforeCrafting()
    {
        if (DateTime.Now > _homeBeforeCraftingUntil)
        {
            ForkTrace.Info("go home (before crafting): not home within three minutes, crafting here");
            CraftAfterHomeTrip();
            return;
        }

        if (_homeBeforeCraftingAt == null)
        {
            if (PauseHome.Blocker() != null)
                return;

            if (!HomeNavigationHelper.TryStartReturnHome(out var error, "before crafting"))
            {
                if (error == null)
                    return;

                ForkTrace.Info($"go home (before crafting): {error}");
                CraftAfterHomeTrip();
                return;
            }

            _homeBeforeCraftingAt = DateTime.Now.AddSeconds(2);
            return;
        }

        if (DateTime.Now < _homeBeforeCraftingAt || !HomeNavigationHelper.IsReturnComplete())
            return;

        ForkTrace.Info($"go home (before crafting): Lifestream finished, now in territory {Dalamud.ClientState.TerritoryType}");
        CraftAfterHomeTrip();
    }

    private static void CraftAfterHomeTrip()
    {
        _homeBeforeCrafting = false;
        OnGatherComplete();
    }

    public static void OnGatherComplete()
    {
        if (_isQueueMode && _queueProcessor != null)
        {
            _waitingForGatherComplete = false;
            GatherBuddy.Log.Debug($"[CraftingGatherBridge] Gather complete for queue mode");
            _queueProcessor.OnGatherComplete();
            return;
        }
        
        if (_recipeIdToCraft == 0)
            return;
        
        var recipeSheet = Dalamud.GameData.GetExcelSheet<Recipe>();
        if (recipeSheet == null || !recipeSheet.TryGetRow(_recipeIdToCraft, out var recipe))
        {
            GatherBuddy.Log.Error($"Could not find recipe {_recipeIdToCraft}");
            _recipeIdToCraft = 0;
            _waitingForGatherComplete = false;
            return;
        }
        
        var requiredCraftJob = (uint)(recipe.CraftType.RowId + 8);
        var currentJob = Dalamud.Objects.LocalPlayer?.ClassJob.RowId ?? 0;
        
        if (currentJob != requiredCraftJob)
        {
            if (!_waitingForJobSwitch)
            {
                GatherBuddy.Log.Information($"Switching from job {currentJob} to job {requiredCraftJob} for crafting");
                SwitchJob(requiredCraftJob);
                _jobSwitchTime = DateTime.Now;
                _waitingForJobSwitch = true;
            }
            return;
        }
        
        _waitingForGatherComplete = false;
        _waitingForJobSwitch = false;
        GatherBuddy.Log.Information($"Gathering complete. Starting craft for recipe {_recipeIdToCraft}");
        
        DeleteTemporaryGatherList();
        
        CraftingGameInterop.StartCraft(recipe, 1);
        _recipeIdToCraft = 0;
    }
    
    private static unsafe void SwitchJob(uint jobId)
    {
        try
        {
            var gearsetModule = FFXIVClientStructs.FFXIV.Client.UI.Misc.RaptureGearsetModule.Instance();
            if (gearsetModule == null)
            {
                GatherBuddy.Log.Error("Failed to get gearset module");
                return;
            }
            
            if (GearsetStatsReader.TryResolveExistingGearsetIndex(gearsetModule, jobId, out var gearsetIndex))
            {
                gearsetModule->EquipGearset(gearsetIndex);
                GatherBuddy.Log.Information($"Equipped gearset {gearsetIndex} for job {jobId}");
                return;
            }
            
            GatherBuddy.Log.Warning($"No gearset found for job {jobId}");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Error($"Failed to switch job: {ex.Message}");
        }
    }

    public static bool IsGatheringComplete()
    {
        // fork: the buy step runs before any gathering list exists, so without this the run read "no list" as "gathered" and crafted mid-trip
        if (IsBuying)
            return false;
        if (_gatherList == null)
            return _waitingForGatherComplete;

        var allComplete = true;
        foreach (var item in _gatherList.Items)
        {
            var have = GetInventoryCount(item.ItemId);
            var needed = _gatherList.Quantities.TryGetValue(item, out var qty) ? qty : 0;
            if (have < needed)
            {
                allComplete = false;
                break;
            }
        }

        return allComplete;
    }

    private static unsafe int GetInventoryCount(uint itemId)
    {
        try
        {
            var inventory = InventoryManager.Instance();
            if (inventory == null)
                return 0;
            return inventory->GetInventoryItemCount(itemId, false, false, false);
        }
        catch
        {
            return 0;
        }
    }
    
    public static void TestRepairSystem()
    {
        if (_queueProcessor != null && _isQueueMode)
        {
            GatherBuddy.Log.Warning("[CraftingGatherBridge] Cannot test repair - queue is already running");
            return;
        }
        
        GatherBuddy.Log.Information("[CraftingGatherBridge] Starting repair system test");
        _isQueueMode = true;
        _queueProcessor = new CraftingQueueProcessor();
        _queueProcessor.TestRepair();
        
        GatherBuddy.CraftingStatusWindow?.SetQueueProcessor(_queueProcessor);
    }
    
    private static void RestoreDisabledGatherLists()
    {
        if (_disabledGatherLists.Count == 0 || _plugin == null)
            return;

        foreach (var list in _disabledGatherLists)
        {
            list.Enabled = true;
            _plugin.AutoGatherListsManager.PausedByRun.Remove(list);
            GatherBuddy.Log.Debug($"[CraftingGatherBridge] Re-enabled gather list '{list.Name}'");
        }
        _plugin.AutoGatherListsManager.SetActiveItems();
        _plugin.AutoGatherListsManager.Save();
        _disabledGatherLists.Clear();
    }

    private static void OnQueueCompleted()
    {
        GatherBuddy.Log.Information("[CraftingGatherBridge] Queue completed, will clean up after tasks finish");
    }

    private static void TryStartCollectablesInterruption()
    {
        if (_queueProcessor == null
         || _queueProcessor.CurrentState is CraftingQueueProcessor.QueueState.Idle or CraftingQueueProcessor.QueueState.Complete
         || GatherBuddy.CollectableManager == null
         || GatherBuddy.CollectableManager.IsRunning
         || _waitingForCollectablesHomeReturn
         || DateTime.UtcNow < _nextCollectablesRetry)
            return;

        if (_waitingForCollectables)
            return;

        var collectableConfig = GatherBuddy.Config.CollectableConfig;
        if (!collectableConfig.AutoTurnInCollectables)
        {
            if (_collectablesStartPending)
            {
                GatherBuddy.Log.Information("[CraftingGatherBridge] Collectables interruption was pending when auto turn-ins were forced off, resuming the queue without starting collectables");
                ResetCollectablesInterruptionState();
                _queueProcessor.Resume();
            }
            LogCollectablesHardFailState(collectableConfig.AutoTurnInHardFailReason);
            return;
        }

        if (!CollectableTurnInRequirements.IsAvailable)
        {
            if (_collectablesStartPending)
            {
                GatherBuddy.Log.Debug("[CraftingGatherBridge] Collectables interruption was pending when neither AllaganTools nor AllaganItemSearch was loaded, resuming the queue without starting collectables");
                ResetCollectablesInterruptionState();
                _queueProcessor.Resume();
            }
            return;
        }

        if (!_collectablesStartPending)
        {
            if (_queueProcessor.Paused)
                return;

            var thresholdState = CollectableInventoryHelper.GetThresholdState(GatherBuddy.Config.CollectableConfig);
            if (!thresholdState.ThresholdReached)
                return;
            _queueProcessor.Pause(forPlayer: false);
            _collectablesStartPending = true;
            _lastCollectablesWaitLog = DateTime.MinValue;
            _lastCollectablesExitAttempt = DateTime.MinValue;
        }

        TryExitCraftingUiForCollectables();
        if (!IsReadyToStartCollectables(out var waitReason))
        {
            LogCollectablesWaitReason(waitReason);
            return;
        }

        if (IsWaitingForCollectablesRouteData(out waitReason))
        {
            LogCollectablesWaitReason(waitReason);
            return;
        }

        if (GatherBuddy.CollectableManager.Start(CollectableRunSource.VulcanQueue, returnHomeAfterCompletion: true))
        {
            _collectablesStartPending = false;
            _waitingForCollectables = true;
            _lastCollectablesWaitLog = DateTime.MinValue;
            _lastCollectablesExitAttempt = DateTime.MinValue;
            return;
        }

        if (IsWaitingForCollectablesRouteData(out waitReason))
        {
            LogCollectablesWaitReason(waitReason);
            return;
        }

        GatherBuddy.Log.Warning($"[CraftingGatherBridge] Failed to start collectables interruption: {GatherBuddy.CollectableManager.StatusText}");
        ResetCollectablesInterruptionState();
        _nextCollectablesRetry = DateTime.UtcNow.AddSeconds(5);
        _queueProcessor.Resume();
    }

    private static void TryExitCraftingUiForCollectables()
    {
        if (CraftingGameInterop.CurrentState != CraftingGameInterop.CraftState.IdleBetween)
            return;

        if (_lastCollectablesExitAttempt != DateTime.MinValue
         && (DateTime.UtcNow - _lastCollectablesExitAttempt) < TimeSpan.FromMilliseconds(500))
            return;

        _lastCollectablesExitAttempt = DateTime.UtcNow;
        CraftingTasks.TaskExitCraft();
    }

    private static bool IsReadyToStartCollectables(out string waitReason)
    {
        if (Dalamud.Conditions[ConditionFlag.BetweenAreas] || Dalamud.Conditions[ConditionFlag.BetweenAreas51])
        {
            waitReason = "area transition is still active";
            return false;
        }

        if (Lifestream.Enabled && Lifestream.IsBusy())
        {
            waitReason = "Lifestream is still busy";
            return false;
        }

        if (!GenericHelpers.IsScreenReady())
        {
            waitReason = "the screen is not ready";
            return false;
        }

        if (Dalamud.Conditions[ConditionFlag.ExecutingCraftingAction])
        {
            waitReason = "a crafting action is still executing";
            return false;
        }

        if (Dalamud.Conditions[ConditionFlag.PreparingToCraft])
        {
            waitReason = "craft preparation is still active";
            return false;
        }

        if (Dalamud.Conditions[ConditionFlag.Crafting])
        {
            waitReason = $"crafting state is still {CraftingGameInterop.CurrentState}";
            return false;
        }

        if (CraftingGameInterop.CurrentState != CraftingGameInterop.CraftState.IdleNormal)
        {
            waitReason = $"crafting has not returned to IdleNormal yet ({CraftingGameInterop.CurrentState})";
            return false;
        }

        if (IsCraftingAddonVisible("RecipeNote") || IsCraftingAddonVisible("Synthesis") || IsCraftingAddonVisible("SynthesisSimple") || IsCraftingAddonVisible("WKSRecipeNotebook"))
        {
            waitReason = "crafting windows are still visible";
            return false;
        }

        waitReason = string.Empty;
        return true;
    }

    private static unsafe bool IsCraftingAddonVisible(string addonName)
    {
        var addon = (AtkUnitBase*)(nint)Dalamud.GameGui.GetAddonByName(addonName);
        return addon != null && addon->IsVisible;
    }

    private static bool IsWaitingForCollectablesRouteData(out string waitReason)
    {
        if (!CollectableTurnInRouteResolver.HasLookupData)
        {
            waitReason = string.Empty;
            return false;
        }

        var collectableNpcIds = CollectableTurnInRouteResolver.GetCollectableNpcIds();
        if (collectableNpcIds.Count == 0)
        {
            waitReason = string.Empty;
            return false;
        }

        VendorNpcLocationCache.InitializeAsync(collectableNpcIds);
        if (VendorNpcLocationCache.IsInitialized)
        {
            waitReason = string.Empty;
            return false;
        }

        waitReason = VendorNpcLocationCache.IsInitializing
            ? $"collectables route locations are still loading ({VendorNpcLocationCache.ResolvedNpcCount}/{VendorNpcLocationCache.RequestedNpcCount} NPCs resolved)"
            : "collectables route locations are still loading";
        return true;
    }

    private static void LogCollectablesWaitReason(string waitReason)
    {
        if (_lastCollectablesWaitLog != DateTime.MinValue && (DateTime.UtcNow - _lastCollectablesWaitLog) < TimeSpan.FromSeconds(10))
            return;

        GatherBuddy.Log.Debug($"[CraftingGatherBridge] Waiting to start collectables interruption: {waitReason}");
        _lastCollectablesWaitLog = DateTime.UtcNow;
    }

    private static void OnCollectablesFinished()
    {
        if (!_waitingForCollectables && !_collectablesStartPending)
            return;
        ResetCollectablesInterruptionState();
        _queueProcessor?.Resume();
    }

    private static void OnCollectablesError(string error)
    {
        if (!_waitingForCollectables && !_collectablesStartPending)
            return;

        GatherBuddy.Log.Error($"[CraftingGatherBridge] Collectables interruption failed: {error}");
        var hardFailReason = GatherBuddy.Config.CollectableConfig.AutoTurnInHardFailReason;
        if (!GatherBuddy.Config.CollectableConfig.AutoTurnInCollectables && !string.IsNullOrWhiteSpace(hardFailReason))
        {
            LogCollectablesHardFailState(hardFailReason);
            StartCollectablesHomeReturnBeforeResume(hardFailReason);
            return;
        }

        ResetCollectablesInterruptionState();
        _nextCollectablesRetry = DateTime.UtcNow.AddSeconds(5);
        _lastCollectablesWaitLog = DateTime.MinValue;
        _lastCollectablesExitAttempt = DateTime.MinValue;
        _lastCollectablesHardFailLog = DateTime.MinValue;
        _queueProcessor?.Resume();
    }

    private static void StartCollectablesHomeReturnBeforeResume(string hardFailReason)
    {
        _collectablesStartPending = false;
        _waitingForCollectables = false;
        _waitingForCollectablesHomeReturn = true;
        _collectablesHomeReturnStarted = false;
        _nextCollectablesRetry = DateTime.MinValue;
        _lastCollectablesWaitLog = DateTime.MinValue;
        _lastCollectablesExitAttempt = DateTime.MinValue;
        GatherBuddy.Log.Warning("[CraftingGatherBridge] Returning home before resuming the queue after collectables hard fail");
    }

    private static void UpdateCollectablesHomeReturnBeforeResume()
    {
        if (!_waitingForCollectablesHomeReturn)
            return;

        if (!_collectablesHomeReturnStarted)
        {
            if (Lifestream.Enabled && Lifestream.IsBusy())
                return;

            if (!HomeNavigationHelper.TryStartReturnHome(out var error))
            {
                if (string.IsNullOrWhiteSpace(error))
                    return;

                GatherBuddy.Log.Warning($"[CraftingGatherBridge] {error}");
                GatherBuddy.Log.Warning("[CraftingGatherBridge] Resuming the queue without a home return after collectables hard fail");
                ResetCollectablesInterruptionState();
                _queueProcessor?.Resume();
                return;
            }

            _collectablesHomeReturnStarted = true;
            return;
        }

        if (!HomeNavigationHelper.IsReturnComplete())
            return;

        GatherBuddy.Log.Information("[CraftingGatherBridge] Home return complete, resuming the queue after collectables hard fail");
        ResetCollectablesInterruptionState();
        _queueProcessor?.Resume();
    }
    
    public static void StopQueue()
    {
        if (_queueProcessor != null)
        {
            GatherBuddy.Log.Information("[CraftingGatherBridge] Stopping queue processor");
            ResetCollectablesInterruptionState();
            _lastCollectablesHardFailLog = DateTime.MinValue;
            _ephemeralListId = null;
            GatherBuddy.AutoGather.Enabled = false;
            if (_afterBuying != null)
                GatherBuddy.VendorBuyListManager?.CancelRunPurchase();
            ClearBuying();
            _homeBeforeCrafting = false;
            CraftingGameInterop.CancelCurrentCraft();
            DeleteTemporaryGatherList();
            _queueProcessor.Reset();
            _queueProcessor = null;
            _activeExecutionPlan = null;
            _isQueueMode = false;
            KeepMarks.EndRun(_keepRun);
            _keepRun = null;
            RunChat.End();
            RestoreDisabledGatherLists();
            GatherBuddy.CraftingStatusWindow?.SetQueueProcessor(null);
        }
        else
        {
            GatherBuddy.Log.Information("[CraftingGatherBridge] No queue processor running");
        }
    }

    public static void PauseQueue(string reason)
    {
        if (_isQueueMode && _queueProcessor is { Paused: false })
            _queueProcessor.Pause(reason);
    }

    private static void LogCollectablesHardFailState(string hardFailReason)
    {
        if (string.IsNullOrWhiteSpace(hardFailReason))
            return;

        if (_lastCollectablesHardFailLog != DateTime.MinValue && (DateTime.UtcNow - _lastCollectablesHardFailLog) < TimeSpan.FromSeconds(30))
            return;

        GatherBuddy.Log.Warning($"[CraftingGatherBridge] Skipping collectables interruption because auto turn-ins were forced off: {hardFailReason}");
        _lastCollectablesHardFailLog = DateTime.UtcNow;
    }

    private static void ResetCollectablesInterruptionState()
    {
        _collectablesStartPending = false;
        _waitingForCollectables = false;
        _waitingForCollectablesHomeReturn = false;
        _collectablesHomeReturnStarted = false;
        _nextCollectablesRetry = DateTime.MinValue;
        _lastCollectablesWaitLog = DateTime.MinValue;
        _lastCollectablesExitAttempt = DateTime.MinValue;
    }
}
