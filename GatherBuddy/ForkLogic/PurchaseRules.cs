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

    // a drop or a currency item: no class gathers it and no gil vendor sells it, so a run cannot get it by itself
    public static List<(uint ItemId, int Missing)> BeyondReach(IEnumerable<KeyValuePair<uint, int>> materials, Func<uint, bool> gatherable,
        Func<uint, bool> soldForGil, Func<uint, int> held)
    {
        var beyond = new List<(uint, int)>();
        foreach (var (itemId, need) in materials)
        {
            var missing = need - Math.Max(0, held(itemId));
            if (missing > 0 && !gatherable(itemId) && !soldForGil(itemId))
                beyond.Add((itemId, missing));
        }

        return beyond;
    }

    public static string NoWayToGet(int count)
        => $"{count} material(s) can be neither gathered nor bought for gil, so the run cannot get them itself";

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
