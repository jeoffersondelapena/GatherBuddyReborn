#nullable enable

namespace GatherBuddy.ForkLogic;

// with Skip If Already Have Enough off, a list's amount is gathered on top of what was held when the run started
public static class GatherRules
{
    public static bool StillNeeded(int held, int? heldAtStart, uint quantity)
        => (heldAtStart is { } start ? held - start : held) < quantity;
}
