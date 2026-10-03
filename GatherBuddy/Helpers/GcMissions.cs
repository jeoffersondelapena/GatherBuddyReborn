using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.Automation;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Interfaces;
using Lumina.Excel.Sheets;
using Mission = GatherBuddy.ForkLogic.MissionRules.Mission;

namespace GatherBuddy.Helpers;

// fork: the day's Grand Company supply and provisioning missions as one crafting list and one gathering list, refilled on every press
internal static unsafe class GcMissions
{
    public const string Window = "ContentsInfoDetail";

    private static readonly Lazy<Dictionary<string, Mission>> Known = new(() =>
    {
        var known = new Dictionary<string, Mission>(StringComparer.Ordinal);
        foreach (var row in Dalamud.GameData.GetExcelSheet<GCSupplyDuty>())
            foreach (var data in row.SupplyData)
                for (var i = 0; i < data.Item.Count; i++)
                    if (data.Item[i].RowId != 0 && data.Item[i].ValueNullable is { } item)
                        known.TryAdd(item.Name.ExtractText(), new Mission(item.RowId, Math.Max(1, (int)data.ItemCount[i])));
        return known;
    });

    public static bool Busy
        => CraftingGatherBridge.IsQueueMode || GatherBuddy.AutoGather.Enabled;

    private static DateTime _looked = DateTime.MinValue;
    private static bool     _showing;

    // the detail window serves every timer; cached because the overlay asks every frame
    public static bool WindowShowsMissions(AtkUnitBase* addon)
    {
        if (DateTime.UtcNow - _looked < TimeSpan.FromMilliseconds(500))
            return _showing;

        _looked  = DateTime.UtcNow;
        _showing = MissionRules.FromNames(Texts(addon), Known.Value).Count > 0;
        return _showing;
    }

    public static void MakeLists()
    {
        var delivery = FromDeliveryData();
        var window   = FromWindow(delivery);
        ForkTrace.Info($"gc missions: Timers window {Describe(window)}; delivery data {Describe(delivery)}");
        var missions = window is { Count: > 0 } ? window : delivery;
        if (missions is not { Count: > 0 })
        {
            Dalamud.Chat.PrintError("[GatherBuddy] Could not read today's Grand Company missions. Open Timers, then Supply & Provisioning "
              + "Missions, and press the button again (fork).");
            return;
        }

        var crafted  = new List<(Recipe Recipe, int Crafts)>();
        var gathered = new List<(IGatherable Item, uint Amount)>();
        var waiting  = new List<string>();
        var neither  = new List<string>();
        foreach (var mission in missions)
        {
            var name   = ForkTrace.ItemName(mission.ItemId);
            var amount = Math.Max(1, mission.Requested);
            if (RecipeManager.GetRecipeForItem(mission.ItemId) is { } recipe)
            {
                var crafts = MissionRules.Crafts(amount, recipe.AmountResult);
                var slow   = Held(mission.ItemId) >= amount
                    ? []
                    : RecipeManager.GetResolvedIngredients(recipe).Where(m => Waits(m.Key, m.Value * crafts, MaterialSourceClassifier.IsSoldForGil(m.Key)))
                        .Select(m => ForkTrace.ItemName(m.Key)).ToList();
                if (slow.Count > 0)
                    waiting.Add($"{name} (needs {string.Join(", ", slow)})");
                else
                    crafted.Add((recipe, crafts));
            }
            else if (Waits(mission.ItemId, amount, false))
                waiting.Add(name);
            else if (GatherBuddy.GameData.Gatherables.TryGetValue(mission.ItemId, out var gatherable))
                gathered.Add((gatherable, (uint)amount));
            else if (GatherBuddy.GameData.Fishes.TryGetValue(mission.ItemId, out var fish))
                gathered.Add((fish, (uint)amount));
            else
                neither.Add(name);
        }

        FillCraftingList(crafted);
        var gatheringListed = FillGatheringList(gathered);
        Dalamud.Chat.Print($"[GatherBuddy] Grand Company mission lists refilled: {crafted.Count} item(s) in the crafting list "
          + $"'{MissionRules.SupplyList}'"
          + (gatheringListed ? $", {gathered.Count} in the gathering list '{MissionRules.ProvisioningList}'" : "") + " (fork).");
        ForkTrace.Info($"gc missions: left out for waiting: {(waiting.Count == 0 ? "none" : string.Join(", ", waiting))}");
        if (waiting.Count > 0)
            Dalamud.Chat.PrintError($"[GatherBuddy] Left out, since each would mean waiting for a time or weather window: {string.Join(", ", waiting)} (fork).");
        if (neither.Count > 0)
            Dalamud.Chat.PrintError($"[GatherBuddy] Neither craftable nor gatherable, so on no list: {string.Join(", ", neither)} (fork).");
    }

    private static int Held(uint itemId)
        => Vulcan.Vendors.VendorBuyListManager.GetCurrentInventoryAndArmoryCount(itemId);

    // a gathering run never buys, so a vendor only spares the wait for a crafting run's materials
    private static bool Waits(uint itemId, int needed, bool soldForGil)
        => MissionRules.Waits(
            GatherBuddy.GameData.Gatherables.TryGetValue(itemId, out var node) ? node.InternalLocationId : null,
            GatherBuddy.GameData.Fishes.TryGetValue(itemId, out var fish) ? fish.InternalLocationId : null,
            AutoGather.Helpers.Diadem.ApprovedToRawItemIds.ContainsKey(itemId), soldForGil, Held(itemId), needed);

    // a refill resets only the switches that decide whether a held or logged item is made again
    private static void FillCraftingList(List<(Recipe Recipe, int Crafts)> crafted)
    {
        var manager = GatherBuddy.CraftingListManager;
        var list    = manager.GetListByName(MissionRules.SupplyList);
        if (list == null)
        {
            list                            = manager.CreateNewList(MissionRules.SupplyList);
            list.QuickSynthAll              = true;
            list.QuickSynthAllPreferNQ      = true;
            list.QuickSynthAllPrecraftsOnly = true;
        }

        list.SkipIfEnough       = true;
        list.SkipFinalIfEnough  = true;
        list.SkipCraftedRecipes = false;
        list.Recipes.Clear();
        foreach (var (recipe, crafts) in crafted)
            list.AddRecipe(recipe.RowId, crafts);
        list.Description = MissionRules.Description(DateTime.Now, crafted.Count);
        manager.SaveList(list);
    }

    private static bool FillGatheringList(List<(IGatherable Item, uint Amount)> gathered)
    {
        if (CraftingGatherBridge.ListsManager is not { } lists)
            return false;

        var list = lists.Lists.FirstOrDefault(l => l.Name == MissionRules.ProvisioningList);
        if (list == null)
        {
            list = new AutoGatherList { Name = MissionRules.ProvisioningList };
            lists.AddList(list);
        }

        list.CountHeld       = true;
        list.SkipLoggedItems = false;
        while (list.Items.Count > 0)
            list.RemoveAt(0);
        foreach (var (item, amount) in gathered)
            list.Add(item, amount);
        list.Description = MissionRules.Description(DateTime.Now, gathered.Count);
        lists.Save();
        lists.SetActiveItems();
        return true;
    }

    private static List<Mission>? FromDeliveryData()
    {
        var agent = AgentGrandCompanySupply.Instance();
        if (agent == null || agent->SupplyProvisioningData == null)
            return null;

        // the pointer may outlive the delivery window, so only items the mission table holds are trusted
        var valid    = Known.Value.Values.Select(m => m.ItemId).ToHashSet();
        var missions = new List<Mission>();
        foreach (ref var item in agent->SupplyProvisioningData->SupplyData)
            if (valid.Contains(item.ItemId))
                missions.Add(new Mission(item.ItemId, item.NumRequested));
        foreach (ref var item in agent->SupplyProvisioningData->ProvisioningData)
            if (valid.Contains(item.ItemId))
                missions.Add(new Mission(item.ItemId, item.NumRequested));
        return missions;
    }

    private static List<Mission>? FromWindow(List<Mission>? exact)
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(Window, out var addon) || !addon->IsVisible)
            return null;

        var texts = Texts(addon);
        ForkTrace.Info($"gc missions: the Timers window holds {addon->AtkValuesCount} value(s), {texts.Count} of them text: {string.Join(" | ", texts.Take(60))}");
        return MissionRules.FromNames(texts, Known.Value, exact);
    }

    private static List<string> Texts(AtkUnitBase* addon)
    {
        var texts = new List<string>();
        for (var i = 0; i < addon->AtkValuesCount; i++)
        {
            var value = addon->AtkValues[i];
            if (((int)value.Type & 0x0F) is 8 or 10 && (byte*)value.String != null)
                texts.Add(SeString.Parse((byte*)value.String).TextValue);
        }

        return texts;
    }

    private static string Describe(List<Mission>? missions)
        => missions == null ? "not available"
            : missions.Count == 0 ? "empty"
            : string.Join(", ", missions.Select(m => $"{ForkTrace.ItemName(m.ItemId)} x{m.Requested}"));
}
