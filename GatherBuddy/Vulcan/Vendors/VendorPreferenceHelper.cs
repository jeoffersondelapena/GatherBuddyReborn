using System.Collections.Generic;
using System.Linq;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;

namespace GatherBuddy.Vulcan.Vendors;

public static class VendorPreferenceHelper
{
    public static string GetKey(uint itemId, uint currencyId, uint cost)
        => $"{itemId}_{currencyId}_{cost}";
    public static string GetKey(VendorShopType shopType, uint itemId, uint currencyId, uint cost)
        => $"{(int)shopType}_{itemId}_{currencyId}_{cost}";

    public static string GetKey(VendorShopEntry entry)
        => GetKey(entry.ShopType, entry.ItemId, entry.CurrencyItemId, entry.Cost);

    public static string GetKey(VendorBuyListEntry entry)
        => GetKey(entry.ShopType, entry.ItemId, entry.CurrencyItemId, entry.Cost);

    public static string GetRouteKey(VendorNpc npc)
        => $"{npc.NpcId}_{(int)npc.MenuShopType}_{npc.ShopId}_{npc.InclusionPageIndex}_{npc.InclusionSubPageIndex}_{npc.SourceShopId}_{npc.ShopItemIndex}_{npc.GcRankIndex}_{npc.GcCategoryIndex}";

    public static uint GetPreferredNpcId(uint itemId, uint currencyId, uint cost)
    {
        GatherBuddy.Config.VendorNpcPreferences.TryGetValue(GetKey(itemId, currencyId, cost), out var npcId);
        return npcId;
    }

    public static bool MatchesVendor(VendorNpc? left, VendorNpc? right)
        => left != null
        && right != null
        && left.NpcId == right.NpcId
        && left.MenuShopType == right.MenuShopType
        && left.ShopId == right.ShopId
        && left.InclusionPageIndex == right.InclusionPageIndex
        && left.InclusionSubPageIndex == right.InclusionSubPageIndex
        && left.SourceShopId == right.SourceShopId
        && left.ShopItemIndex == right.ShopItemIndex
        && left.GcRankIndex == right.GcRankIndex
        && left.GcCategoryIndex == right.GcCategoryIndex;

    public static void SetPreferredNpc(VendorShopEntry entry, VendorNpc npc)
    {
        GatherBuddy.Config.VendorRoutePreferences[GetKey(entry)] = GetRouteKey(npc);
        GatherBuddy.Config.VendorNpcPreferences[GetKey(entry.ItemId, entry.CurrencyItemId, entry.Cost)] = npc.NpcId;
        GatherBuddy.Config.Save();
    }

    public static VendorNpc? ResolvePreferredNpc(VendorShopEntry entry)
        => ResolvePreferredNpc(entry.Npcs, entry.ShopType, entry.ItemId, entry.CurrencyItemId, entry.Cost);

    public static VendorNpc? ResolvePreferredNpc(VendorShopEntry entry, IReadOnlyList<VendorNpc> npcs)
        => ResolvePreferredNpc(npcs, entry.ShopType, entry.ItemId, entry.CurrencyItemId, entry.Cost);

    public static VendorNpc? ResolvePreferredNpc(VendorBuyListEntry entry, IReadOnlyList<VendorNpc> npcs)
        => ResolvePreferredNpc(npcs, entry.ShopType, entry.ItemId, entry.CurrencyItemId, entry.Cost);

    // fork: with no saved choice, a vendor in the player's zone, else the cheapest teleport, rather than the game data's first
    private static VendorNpc? CheapestFirst(IReadOnlyList<VendorNpc> npcs)
    {
        if (!Dalamud.Framework.IsInFrameworkUpdateThread || Dalamud.Objects.LocalPlayer is not { } player)
            return null;

        var places = npcs.Select(npc => LiveNpcs.TryGet(npc.NpcId, out var here)
                ? new VendorRules.Place(Dalamud.ClientState.TerritoryType, here, null)
                : VendorNpcLocationCache.TryGetFirstLocation(npc.NpcId) is { } at
                    ? new VendorRules.Place(at.TerritoryId, at.Position, TeleportCosts.Trip(at.TerritoryId, () => TeleportCosts.ToZone(at.TerritoryId)))
                    : (VendorRules.Place?)null)
            .ToList();
        var pick = VendorRules.Pick(places, Dalamud.ClientState.TerritoryType, player.Position);
        return pick >= 0 ? npcs[pick] : null;
    }

    private static VendorNpc? ResolvePreferredNpc(IReadOnlyList<VendorNpc> npcs, VendorShopType shopType, uint itemId, uint currencyId, uint cost)
    {
        var selectableNpcs = VendorDevExclusions.GetSelectableNpcs(npcs, "resolving preferred vendors");
        if (selectableNpcs.Count == 0)
            return null;

        GatherBuddy.Config.VendorRoutePreferences ??= new();
        if (GatherBuddy.Config.VendorRoutePreferences.TryGetValue(GetKey(shopType, itemId, currencyId, cost), out var preferredRouteKey))
        {
            var preferredRoute = selectableNpcs.FirstOrDefault(npc => GetRouteKey(npc) == preferredRouteKey);
            if (preferredRoute != null)
                return preferredRoute;
        }
        var preferredNpcId = GetPreferredNpcId(itemId, currencyId, cost);
        var preferredNpc   = selectableNpcs.FirstOrDefault(npc => npc.NpcId == preferredNpcId);
        if (preferredNpc != null)
            return preferredNpc;
        if (CheapestFirst(selectableNpcs) is { } nearest)
            return nearest;
        foreach (var npc in selectableNpcs)
            if (VendorNpcLocationCache.TryGetFirstLocation(npc.NpcId) != null)
                return npc;
        return selectableNpcs[0];
    }
}
