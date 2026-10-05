#nullable enable

namespace GatherBuddy.ForkLogic;

// with Count Items Already Held off, a list's amount is gathered on top of what was held when the run started
public static class GatherRules
{
    public static bool StillNeeded(int held, int? heldAtStart, uint quantity)
        => Missing(held, heldAtStart, quantity) > 0;

    public static int Missing(int held, int? heldAtStart, uint quantity)
        => (int)System.Math.Max(0, quantity - (long)(heldAtStart is { } start ? held - start : held));

    // a bought item is never logged, so a log list's share is left for gathering
    public static int ToBuy(int missing, uint keptForGathering)
        => (int)System.Math.Max(0, missing - (long)keptForGathering);

    public static bool Waits(int? nodeLocation, int? fishLocation, bool diademRaw)
        => (nodeLocation != null || fishLocation != null) && !PurchaseRules.GatheredWithoutWaiting(nodeLocation, fishLocation, diademRaw);

    // Buy Instead of Gathering goes further than Buy Instead of Waiting, so it only counts while that one is on
    public static bool Buys(bool insteadOfWaiting, bool insteadOfGathering, bool waits)
        => insteadOfWaiting && (insteadOfGathering || waits);

    public static bool BuyInstead(bool soldForGil, int toBuy)
        => soldForGil && toBuy > 0;
}
