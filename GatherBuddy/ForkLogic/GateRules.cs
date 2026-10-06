#nullable enable
using System.Collections.Generic;

namespace GatherBuddy.ForkLogic;

public enum GateKind
{
    Level,
    Class,
    Book,
    Quest,
    Gearset,
    Stats,
    Specialist,
    Item,
    Status,
}

public readonly record struct Gate(GateKind Kind, string Need);

public enum Gated
{
    Craft,
    Buy,
    Defer,
    Block,
}

// what a run does with a craft the character cannot start: only a level can change during the run, so only that one waits
public static class GateRules
{
    public static bool Defers(GateKind kind)
        => kind == GateKind.Level;

    public static Gated Decide(Gate? gate, BuyCrafts buying, bool soldForGil)
    {
        if (buying == BuyCrafts.InsteadOfCrafting && soldForGil)
            return Gated.Buy;
        if (gate == null)
            return Gated.Craft;
        if (buying == BuyCrafts.WhenOutOfReach && soldForGil)
            return Gated.Buy;
        return Defers(gate.Value.Kind) ? Gated.Defer : Gated.Block;
    }

    public static bool Short(int need, int have)
        => need > 0 && have < need;

    public static string Need(string what, int need, int have)
        => $"{what} {need} (yours is {have})";

    public static string Bought(string item, int amount, string? why)
        => why == null ? $"{item} x{amount} (a vendor sells it)" : $"{item} x{amount} ({why})";

    public static string Blocked(string precraft, string why)
        => $"needs {precraft}: {why}";

    // the same craft is reported once, under the first reason met
    public static List<string> Distinct(IEnumerable<string> lines)
    {
        var seen = new HashSet<string>();
        var kept = new List<string>();
        foreach (var line in lines)
            if (seen.Add(line))
                kept.Add(line);
        return kept;
    }
}
