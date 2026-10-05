#nullable enable
using System;
using System.Collections.Generic;

namespace GatherBuddy.ForkLogic;

// targets are the plan's full amounts, since the buy run itself subtracts what the bags and armoury hold
public static class PurchaseRules
{
    public static List<(uint ItemId, uint Target, int Missing)> BeforeGathering(IEnumerable<KeyValuePair<uint, int>> materials,
        Func<uint, bool> gathered, Func<uint, bool> soldForGil, Func<uint, int> held)
    {
        var buy = new List<(uint, uint, int)>();
        foreach (var (itemId, need) in materials)
        {
            if (need <= 0 || gathered(itemId) || !soldForGil(itemId))
                continue;

            var missing = need - Math.Max(0, held(itemId));
            if (missing > 0)
                buy.Add((itemId, (uint)need, missing));
        }

        return buy;
    }

    // no class gathers it, and either no gil vendor sells it or the run does not buy: a run cannot get it by itself
    public static List<(uint ItemId, int Missing, string Why)> BeyondReach(IEnumerable<KeyValuePair<uint, int>> materials,
        Func<uint, bool> gatherable, Func<uint, bool> soldForGil, Func<uint, int> held, bool buying)
    {
        var beyond = new List<(uint, int, string)>();
        foreach (var (itemId, need) in materials)
        {
            var missing = need - Math.Max(0, held(itemId));
            if (missing <= 0 || gatherable(itemId))
                continue;

            var sold = soldForGil(itemId);
            if (!sold || !buying)
                beyond.Add((itemId, missing, sold ? NotBuying : NeitherGatheredNorSold));
        }

        return beyond;
    }

    public const string NeitherGatheredNorSold = "no class gathers it and no gil vendor sells it";
    public const string NotBuying = "no class gathers it; a gil vendor sells it, but Buy From Vendors Before Gathering (fork) is off";

    public static string NoWayToGet(int count)
        => $"{count} material(s) the run cannot get itself";

    public enum NoVendorCause { ShopKind, LeftOut }

    public static string NoVendor(IReadOnlyList<string> sellers, NoVendorCause cause)
    {
        if (sellers.Count == 0)
            return "no vendor is known for the shop that sells it";

        var named = sellers.Count <= 2 ? string.Join(" and ", sellers) : $"{sellers[0]}, {sellers[1]} and {sellers.Count - 2} more";
        return cause == NoVendorCause.ShopKind
            ? $"sold by {named}, but through a kind of shop window GatherBuddy cannot work"
            : $"sold by {named}, which GatherBuddy's own list of vendors leaves out";
    }

    public static string PlaceUnknown(string vendor)
        => $"GatherBuddy does not know where {vendor} is, so it cannot walk there.";

    // GatherBuddy numbers timed nodes and windowed fish from 1 up; waiting for one is what a vendor saves, so those count as not gathered
    public static bool GatheredWithoutWaiting(int? nodeLocation, int? fishLocation, bool diademRaw)
        => nodeLocation is <= 0 || fishLocation is <= 0 || diademRaw;

    // with Buy Instead of Waiting off, what a class can gather is gathered even when that means waiting for its window
    public static Func<uint, bool> Gathered(bool insteadOfWaiting, bool insteadOfGathering, Func<uint, bool> gatheredWithoutWaiting,
        Func<uint, bool> gatherable)
        => id => insteadOfWaiting ? !insteadOfGathering && gatheredWithoutWaiting(id) : gatherable(id);

    public static bool GatherInstead(bool insteadOfGathering, int mustBuyMissing)
        => insteadOfGathering && mustBuyMissing == 0;

    public static string BuyingHeader(bool insteadOfGathering)
        => insteadOfGathering ? "Buying first what vendors sell:" : "Buying first what only vendors sell:";

    public static string GatheringInstead(int count)
        => $"[GatherBuddy] {count} item(s) could not be bought; the run gathers them instead (fork).";

    public const string AllBought = "[GatherBuddy] Bought everything this run needs from vendors (fork).";

    public static string NotBought(int count)
        => $"{count} item(s) could not be bought";

    public const string StoppedWhileBuying = "[GatherBuddy] Run stopped while buying from vendors (fork).";

    public const string BagsFull = "your bags are full";
}
