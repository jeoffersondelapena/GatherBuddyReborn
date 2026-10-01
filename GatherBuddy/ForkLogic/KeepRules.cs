#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

// marks are keyed by the plain item id: the game adds 1,000,000 for HQ and 500,000 for collectables, and 2,000,000+ are event items
public static class KeepRules
{
    public sealed record Mark(uint ItemId, IReadOnlyDictionary<string, int> ByRun)
    {
        public int Count
            => ByRun.Values.Sum();
    }

    public static uint BaseItemId(ulong id)
        => id >= 2_000_000 ? 0 : (uint)(id % 500_000);

    public static Dictionary<uint, Mark> Merge(IReadOnlyDictionary<uint, Mark> current, IEnumerable<(uint ItemId, int Count)> items, string label)
    {
        var merged = WithoutRun(current, label);
        foreach (var (raw, count) in items)
        {
            var id = BaseItemId(raw);
            if (id == 0 || count <= 0)
                continue;

            var byRun = merged.TryGetValue(id, out var old) ? new Dictionary<string, int>(old.ByRun) : new Dictionary<string, int>();
            byRun[label]  = byRun.GetValueOrDefault(label) + count;
            merged[id]    = new Mark(id, byRun);
        }

        return merged;
    }

    public static Dictionary<uint, Mark> WithoutRun(IReadOnlyDictionary<uint, Mark> current, string label)
    {
        var left = new Dictionary<uint, Mark>();
        foreach (var (id, mark) in current)
        {
            var byRun = mark.ByRun.Where(kv => kv.Key != label).ToDictionary(kv => kv.Key, kv => kv.Value);
            if (byRun.Count == mark.ByRun.Count)
                left[id] = mark;
            else if (byRun.Count > 0)
                left[id] = new Mark(id, byRun);
        }

        return left;
    }

    public static string TooltipLine(Mark mark)
        => $"[Keep] {string.Join(", ", mark.ByRun.Select(kv => $"{kv.Value} for {kv.Key}"))} (fork)";

    public static string Summary(int marked)
        => $"[GatherBuddy] Marked Keep in your bags: {marked} item(s) still needed. Hover one to see how many, or look for its green frame; "
          + "Clear Keep Marks (fork) removes the marks.";
}
