#nullable enable

namespace GatherBuddy.ForkLogic;

// with Skip If Already Have Enough off, a list's amount is gathered on top of what was held when the run started
public static class GatherRules
{
    public static bool StillNeeded(int held, int? heldAtStart, uint quantity)
        => Missing(held, heldAtStart, quantity) > 0;

    public static int Missing(int held, int? heldAtStart, uint quantity)
        => (int)System.Math.Max(0, quantity - (long)(heldAtStart is { } start ? held - start : held));
}
