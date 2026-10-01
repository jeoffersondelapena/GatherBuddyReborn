#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

// the game shows and opens a node only for a gatherer at most 4 levels below it (consolegameswiki "Gathering"); ActiveItemList filters with Reach
public static class GatherLevelRules
{
    public readonly record struct OutOfReach(string Item, string Job, int JobLevel, int NodeLevel);

    public static int Reach(int level)
        => level + 4;

    public static int LevelNeeded(int nodeLevel)
        => Math.Max(1, nodeLevel - 4);

    public static string Reason(IReadOnlyList<OutOfReach> items, int shown = 3)
    {
        var parts = items.Take(shown)
            .Select(i => $"{i.Item} needs {i.Job} {LevelNeeded(i.NodeLevel)} (yours is {i.JobLevel}) for its level {i.NodeLevel} node")
            .ToList();
        if (items.Count > shown)
            parts.Add($"{items.Count - shown} more");
        var joined = parts.Count == 1 ? parts[0] : $"{string.Join(", ", parts.Take(parts.Count - 1))} and {parts[^1]}";
        return $"{joined}, so GatherBuddy left {(items.Count == 1 ? "it" : "them")} out";
    }

    public static string Short(IReadOnlyList<OutOfReach> items)
        => items.Count == 1
            ? $"{items[0].Item} needs {items[0].Job} {LevelNeeded(items[0].NodeLevel)} (yours is {items[0].JobLevel})"
            : $"{items.Count} items need a higher level";
}
