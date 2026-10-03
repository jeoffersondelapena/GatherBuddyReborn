#nullable enable

namespace GatherBuddy.ForkLogic;

// with Count Items Already Held off, a list's amount is gathered on top of what was held when the run started
public static class GatherRules
{
    public static bool StillNeeded(int held, int? heldAtStart, uint quantity)
        => Missing(held, heldAtStart, quantity) > 0;

    public static int Missing(int held, int? heldAtStart, uint quantity)
        => (int)System.Math.Max(0, quantity - (long)(heldAtStart is { } start ? held - start : held));

    // a log list keeps this off: an item bought is an item never logged
    public static bool BuyInstead(bool listsAllow, int? nodeLocation, int? fishLocation, bool diademRaw, bool soldForGil, int missing)
        => listsAllow && soldForGil && missing > 0
         && (nodeLocation != null || fishLocation != null)
         && !PurchaseRules.GatheredWithoutWaiting(nodeLocation, fishLocation, diademRaw);
}
