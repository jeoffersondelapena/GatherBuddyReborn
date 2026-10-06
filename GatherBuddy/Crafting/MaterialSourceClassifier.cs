using System;
using System.Collections.Generic;
using System.Linq;
using GatherBuddy.AutoGather.Collectables;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Crafting;

public enum MaterialSource
{
    Gatherable,
    Fish,
    Scrip,
    GilVendor,
    SpecialCurrency,
    Craftable,
    Drop,
    Other
}

public static class MaterialSourceClassifier
{
    private static Dictionary<uint, List<uint>>? _gilShops;
    private static HashSet<uint>? _scripItems;
    private static HashSet<uint>? _specialCurrencyItems;
    private static HashSet<uint>? _craftableItems;
    private static HashSet<uint>? _fallbackDropItems;
    private static bool _initialized;

    public static void Reset()
    {
        _gilShops             = null;
        _scripItems           = null;
        _specialCurrencyItems = null;
        _craftableItems       = null;
        _fallbackDropItems    = null;
        _initialized          = false;
    }

    public static MaterialSource Classify(uint itemId, bool preferVendors = false)
    {
        EnsureInitialized();

        if (preferVendors && SoldOpenly(itemId))
            return MaterialSource.GilVendor;

        if (GatherBuddy.GameData.Gatherables.ContainsKey(itemId))
            return MaterialSource.Gatherable;

        if (GatherBuddy.GameData.Fishes.ContainsKey(itemId))
            return MaterialSource.Fish;

        // fork: the order a run gets an item in (gather, craft, buy for gil), then what is left to the player
        if (_craftableItems?.Contains(itemId) == true)
            return MaterialSource.Craftable;

        if (SoldOpenly(itemId))
            return MaterialSource.GilVendor;

        if (_scripItems?.Contains(itemId) == true)
            return MaterialSource.Scrip;
        if (MobDropInfoCache.IsKnownDropItem(itemId))
            return MaterialSource.Drop;

        if (!MobDropInfoCache.IsInitialized && _fallbackDropItems?.Contains(itemId) == true)
            return MaterialSource.Drop;

        if (_specialCurrencyItems?.Contains(itemId) == true)
            return MaterialSource.SpecialCurrency;

        return MaterialSource.Other;
    }

    public static bool IsSoldForGil(uint itemId)
    {
        EnsureInitialized();
        return SoldOpenly(itemId);
    }

    // fork: a shop still shut behind a quest does not make an item sold
    private static bool SoldOpenly(uint itemId)
        => _gilShops != null && _gilShops.TryGetValue(itemId, out var shops) && shops.Any(shop => !Vulcan.Vendors.ShopGates.Locked(shop, itemId, out _));

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        BuildGilVendorSet();
        BuildScripSet();
        BuildSpecialCurrencySet();
        BuildCraftableSet();
        BuildDropSet();
    }

    private static void BuildGilVendorSet()
    {
        _gilShops = new Dictionary<uint, List<uint>>();
        try
        {
            var sheet = Dalamud.GameData.GetSubrowExcelSheet<GilShopItem>();
            if (sheet == null) return;
            foreach (var subrow in sheet.SelectMany(s => s))
            {
                if (subrow.Item.RowId == 0)
                    continue;
                if (!_gilShops.TryGetValue(subrow.Item.RowId, out var shops))
                    _gilShops[subrow.Item.RowId] = shops = new List<uint>();
                if (!shops.Contains(subrow.RowId))
                    shops.Add(subrow.RowId);
            }
            GatherBuddy.Log.Debug($"[MaterialSourceClassifier] Gil vendor set: {_gilShops.Count} items");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[MaterialSourceClassifier] Gil vendor set failed: {ex.Message}");
        }
    }

    private static void BuildScripSet()
    {
        _scripItems = new HashSet<uint>();
        try
        {
            VendorShopResolver.InitializeAsync();
            foreach (var entry in VendorShopResolver.SpecialShopEntries.Where(entry => entry.Group == VendorCurrencyGroup.Scrips))
                if (entry.ItemId > 0)
                    _scripItems.Add(entry.ItemId);
            GatherBuddy.Log.Debug($"[MaterialSourceClassifier] Scrip set: {_scripItems.Count} items");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[MaterialSourceClassifier] Scrip set failed: {ex.Message}");
        }
    }

    private static void BuildCraftableSet()
    {
        _craftableItems = new HashSet<uint>();
        try
        {
            var sheet = Dalamud.GameData.GetExcelSheet<Recipe>();
            if (sheet == null) return;
            foreach (var recipe in sheet)
                if (recipe.ItemResult.RowId > 0)
                    _craftableItems.Add(recipe.ItemResult.RowId);
            GatherBuddy.Log.Debug($"[MaterialSourceClassifier] Craftable set: {_craftableItems.Count} items");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[MaterialSourceClassifier] Craftable set failed: {ex.Message}");
        }
    }

    private static void BuildDropSet()
    {
        _fallbackDropItems = new HashSet<uint>();
        try
        {
            var sheet = Dalamud.GameData.GetExcelSheet<RetainerTaskNormal>();
            if (sheet == null) return;
            foreach (var row in sheet)
                if (row.Item.RowId > 0 && row.GatheringLog.RowId == 0 && row.FishingLog.RowId == 0)
                    _fallbackDropItems.Add(row.Item.RowId);
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[MaterialSourceClassifier] Drop set failed: {ex.Message}");
        }
    }

    private static void BuildSpecialCurrencySet()
    {
        _specialCurrencyItems = new HashSet<uint>();
        try
        {
            var sheet = Dalamud.GameData.GetExcelSheet<SpecialShop>();
            if (sheet == null) return;
            foreach (var shop in sheet)
            {
                foreach (var entry in shop.Item)
                {
                    foreach (var received in entry.ReceiveItems)
                    {
                        var id = received.Item.RowId;
                        if (id > 0)
                            _specialCurrencyItems.Add(id);
                    }
                }
            }
            GatherBuddy.Log.Debug($"[MaterialSourceClassifier] Special currency set: {_specialCurrencyItems.Count} items");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[MaterialSourceClassifier] Special currency set failed: {ex.Message}");
        }
    }
}
