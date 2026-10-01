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

    public static string NotBoughtHeader(int count)
        => $"Could not buy {count} item(s); the run goes on and makes what it can without them:";

    public static string CouldNotBuy(string why)
        => $"[GatherBuddy] Could not buy from vendors: {why.TrimEnd('.')}. The run goes on without them (fork).";

    public const string BagsFull = "[GatherBuddy] Run paused: your bags are full, so it could not buy everything. Make room, then press Resume "
      + "in the Craft Status window; it buys only what is still missing (fork).";

    public const string StoppedWhileBuying = "[GatherBuddy] Run stopped while buying from vendors (fork).";
}
