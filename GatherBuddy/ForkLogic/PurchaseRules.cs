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

    public const string BuyingHeader = "Buying first what only vendors sell:";

    public const string AllBought = "[GatherBuddy] Bought everything this run needs from vendors (fork).";

    public static string NotBought(int count)
        => $"{count} item(s) could not be bought";

    public const string StoppedWhileBuying = "[GatherBuddy] Run stopped while buying from vendors (fork).";

    public const string BagsFull = "your bags are full";

    // a purchase first tops up the matching stacks already in the bags; only the rest takes free slots
    public static int SlotsShort(IEnumerable<(int Missing, int StackSize, int Room)> buys, int freeSlots)
    {
        var needed = 0;
        foreach (var (missing, stackSize, room) in buys)
        {
            var stack = Math.Max(stackSize, 1);
            if (missing > room)
                needed += (missing - room + stack - 1) / stack;
        }

        return Math.Max(0, needed - freeSlots);
    }

    public static string NoRoom(int slots)
        => $"your bags need {slots} more free slot(s) for what it buys";
}
