#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

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

    // what you hold counts toward a list's target whether the run got it or you already had it
    public static Dictionary<uint, int> Counted(IReadOnlyDictionary<uint, int> targets, IReadOnlyDictionary<uint, int> held)
        => targets.Select(kv => (kv.Key, Count: System.Math.Min(kv.Value, held.GetValueOrDefault(kv.Key))))
            .Where(x => x.Count > 0)
            .ToDictionary(x => x.Key, x => x.Count);

    public static string NeededLine(Mark mark)
        => $"[Keep] {string.Join(", ", ByLabel(mark).Select(g => $"{g.Count} still needed by {g.Label}"))} (fork)";

    public static string MadeLine(Mark mark)
        => $"[Keep] {string.Join(", ", ByLabel(mark).Select(g => $"{g.Count} for {TextRules.KindLabel(TextRules.KindOfVerb(g.Verb), g.Label)}"))} (fork)";

    private static IEnumerable<(string Label, string Verb, int Count)> ByLabel(Mark mark)
        => mark.ByRun.GroupBy(kv => (Label: TextRules.ShownLabel(kv.Key.Label), kv.Key.Verb))
            .Select(g => (g.Key.Label, g.Key.Verb, g.Sum(kv => kv.Value)));

    public sealed record SavedRow(uint Item, int Run, string Label, string Verb, int Count);

    public sealed record Saved(int NextRun, List<SavedRow> Made);

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // only green is kept on disk: orange belongs to a paused run, which a restart ends
    public static string Serialize(IReadOnlyDictionary<uint, Mark> made, int nextRun)
        => JsonSerializer.Serialize(new Saved(nextRun, made.Values
            .SelectMany(m => m.ByRun.Select(kv => new SavedRow(m.ItemId, kv.Key.Id, kv.Key.Label, kv.Key.Verb, kv.Value)))
            .OrderBy(r => r.Item).ThenBy(r => r.Run)
            .ToList()), Indented);

    public static (Dictionary<uint, Mark> Made, int NextRun) Deserialize(string text)
    {
        var saved = JsonSerializer.Deserialize<Saved>(text);
        var rows  = (saved?.Made ?? new List<SavedRow>()).Where(r => r.Count > 0 && r.Item != 0 && BaseItemId(r.Item) == r.Item).ToList();
        var made  = rows.GroupBy(r => r.Item).ToDictionary(g => g.Key,
            g => new Mark(g.Key, g.GroupBy(r => new Run(r.Run, r.Label, r.Verb)).ToDictionary(x => x.Key, x => x.Sum(r => r.Count))));
        return (made, System.Math.Max(saved?.NextRun ?? 0, rows.Select(r => r.Run).DefaultIfEmpty(0).Max()));
    }

    public static string PauseSummary(int needed, int made)
        => $"[GatherBuddy] Marked in your bags: {needed} item(s) this run still needs, in orange until the run ends"
          + (made > 0 ? $"; {made} item(s) its list counts toward its targets, in green" : "") + ". Hover one to see how many (fork).";

    public static string EndSummary(int made)
        => $"[GatherBuddy] Marked in green in your bags: {made} item(s) this list counts toward its targets. Clear Green Marks (fork) removes them (fork).";
}
