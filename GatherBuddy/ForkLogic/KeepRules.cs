#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

// marks are keyed by the plain item id: the game adds 1,000,000 for HQ and 500,000 for collectables, and 2,000,000+ are event items
public static class KeepRules
{
    public readonly record struct Run(int Id, string Label, string Verb);

    public sealed record Mark(uint ItemId, IReadOnlyDictionary<Run, int> ByRun)
    {
        public int Count
            => ByRun.Values.Sum();
    }

    public static uint BaseItemId(ulong id)
        => id >= 2_000_000 ? 0 : (uint)(id % 500_000);

    public static Dictionary<uint, Mark> Merge(IReadOnlyDictionary<uint, Mark> current, IEnumerable<(uint ItemId, int Count)> items, Run run)
    {
        var merged = WithoutRun(current, run);
        foreach (var (raw, count) in items)
        {
            var id = BaseItemId(raw);
            if (id == 0 || count <= 0)
                continue;

            var byRun = merged.TryGetValue(id, out var old) ? new Dictionary<Run, int>(old.ByRun) : new Dictionary<Run, int>();
            byRun[run] = byRun.GetValueOrDefault(run) + count;
            merged[id] = new Mark(id, byRun);
        }

        return merged;
    }

    public static Dictionary<uint, Mark> WithoutRun(IReadOnlyDictionary<uint, Mark> current, Run run)
    {
        var left = new Dictionary<uint, Mark>();
        foreach (var (id, mark) in current)
        {
            if (!mark.ByRun.ContainsKey(run))
                left[id] = mark;
            else if (mark.ByRun.Count > 1)
                left[id] = new Mark(id, mark.ByRun.Where(kv => kv.Key != run).ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        return left;
    }

    // a mark never claims more than is held, so it cannot land on items that arrive later; what left comes off the oldest runs first
    public static Dictionary<uint, Mark> Shrink(IReadOnlyDictionary<uint, Mark> current, IReadOnlyDictionary<uint, int> held)
    {
        var left = new Dictionary<uint, Mark>();
        foreach (var (id, mark) in current)
        {
            var excess = mark.Count - held.GetValueOrDefault(id);
            if (excess <= 0)
            {
                left[id] = mark;
                continue;
            }

            var byRun = new Dictionary<Run, int>();
            foreach (var (run, count) in mark.ByRun.OrderBy(kv => kv.Key.Id))
            {
                var take = System.Math.Min(excess, count);
                excess -= take;
                if (count - take > 0)
                    byRun[run] = count - take;
            }

            if (byRun.Count > 0)
                left[id] = new Mark(id, byRun);
        }

        return left;
    }

    public static Dictionary<uint, int> Made(IReadOnlyDictionary<uint, int> before, IReadOnlyDictionary<uint, int> now)
        => now.Where(kv => kv.Value > before.GetValueOrDefault(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value - before.GetValueOrDefault(kv.Key));

    public static string NeededLine(Mark mark)
        => $"[Keep] {string.Join(", ", ByLabel(mark).Select(g => $"{g.Count} still needed by {g.Label}"))} (fork)";

    public static string MadeLine(Mark mark)
        => $"[Keep] {string.Join(", ", ByLabel(mark).Select(g => $"{g.Count} {g.Verb} by {g.Label}"))} (fork)";

    private static IEnumerable<(string Label, string Verb, int Count)> ByLabel(Mark mark)
        => mark.ByRun.GroupBy(kv => (Label: TextRules.ShownLabel(kv.Key.Label), kv.Key.Verb))
            .Select(g => (g.Key.Label, g.Key.Verb, g.Sum(kv => kv.Value)));

    public static string PauseSummary(int needed, int made)
        => $"[GatherBuddy] Marked in your bags: {needed} item(s) this run still needs, in orange, cleared when the run goes on"
          + (made > 0 ? $"; {made} item(s) it has made so far, in green" : "") + ". Hover one to see how many (fork).";

    public static string EndSummary(int made, string verb)
        => $"[GatherBuddy] Marked in green in your bags: {made} item(s) this run {verb}. Clear Keep Marks (fork) removes the marks (fork).";
}
