using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: in memory only, so a reload or a restart clears them too
public static class KeepMarks
{
    private static Dictionary<uint, KeepRules.Mark> _marks = new();

    public static int Count
        => _marks.Count;

    public static bool TryGet(ulong itemId, [MaybeNullWhen(false)] out KeepRules.Mark mark)
        => _marks.TryGetValue(KeepRules.BaseItemId(itemId), out mark);

    public static void Add(IEnumerable<(uint ItemId, int Count)> items, string? label)
    {
        var list = items.ToList();
        var name = string.IsNullOrWhiteSpace(label) ? "this run" : label;
        _marks = KeepRules.Merge(_marks, list, name);
        ForkTrace.Info($"keep marks for {name}: {string.Join(", ", list.Where(i => i.Count > 0).Select(i => $"{ForkTrace.Named(i.ItemId)} x{i.Count}"))}; "
          + $"{_marks.Count} item(s) marked in all");
        if (_marks.Count > 0)
            Communicator.PrintError(KeepRules.Summary(_marks.Count));
    }

    public static void ClearRun(string? label)
    {
        if (string.IsNullOrWhiteSpace(label) || _marks.Count == 0)
            return;

        var had = _marks.Values.Count(m => m.ByRun.ContainsKey(label));
        if (had == 0)
            return;

        _marks = KeepRules.WithoutRun(_marks, label);
        ForkTrace.Info($"keep marks: {label} is done, its marks on {had} item(s) cleared; {_marks.Count} item(s) still marked");
    }

    public static void Clear()
    {
        ForkTrace.Info($"keep marks: {_marks.Count} cleared by the player");
        _marks = new Dictionary<uint, KeepRules.Mark>();
    }
}
