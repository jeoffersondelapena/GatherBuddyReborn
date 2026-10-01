using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: in memory only, so a reload or a restart clears them too
public static unsafe class KeepMarks
{
    private static Dictionary<uint, KeepRules.Mark> _needed = new();
    private static Dictionary<uint, KeepRules.Mark> _made   = new();

    private static readonly Dictionary<KeepRules.Run, Dictionary<uint, int>> Open = new();
    private static int      _nextRun;
    private static DateTime _nextPrune;

    public static int Count
        => _needed.Keys.Union(_made.Keys).Count();

    public static bool TryGet(ulong itemId, out KeepRules.Mark? needed, out KeepRules.Mark? made)
    {
        var id = KeepRules.BaseItemId(itemId);
        needed = _needed.GetValueOrDefault(id);
        made   = _made.GetValueOrDefault(id);
        return needed != null || made != null;
    }

    public static KeepRules.Run BeginRun(string? label, string verb, IEnumerable<uint> targets)
    {
        var run = new KeepRules.Run(++_nextRun, string.IsNullOrWhiteSpace(label) ? "this run" : label, verb);
        Open[run] = targets.Select(t => KeepRules.BaseItemId(t)).Where(t => t != 0).Distinct().ToDictionary(t => t, Held);
        ForkTrace.Info($"keep marks: run {run.Id} ({run.Label}, {verb}) watches {Open[run].Count} item(s)");
        return run;
    }

    public static void MarkPause(KeepRules.Run? run, IReadOnlyList<(uint ItemId, int Count)> needed)
    {
        if (run is not { } r)
            return;

        _needed = KeepRules.Merge(_needed, needed, r);
        var made   = NoteMade(r);
        var marked = _needed.Values.Count(m => m.ByRun.ContainsKey(r));
        ForkTrace.Info($"keep marks: run {r.Id} paused; still needed {string.Join(", ", needed.Where(i => i.Count > 0).Select(i => $"{ForkTrace.Named(i.ItemId)} x{i.Count}"))}; made so far {made}");
        if (marked > 0 || made > 0)
            Communicator.PrintRun(KeepRules.PauseSummary(marked, made), r.Label);
    }

    public static void ResumeRun(KeepRules.Run? run)
    {
        if (run is not { } r || !_needed.Values.Any(m => m.ByRun.ContainsKey(r)))
            return;

        _needed = KeepRules.WithoutRun(_needed, r);
        ForkTrace.Info($"keep marks: run {r.Id} went on, its orange marks cleared");
    }

    public static void EndRun(KeepRules.Run? run)
    {
        if (run is not { } r || !Open.ContainsKey(r))
            return;

        _needed = KeepRules.WithoutRun(_needed, r);
        var made = NoteMade(r);
        Open.Remove(r);
        ForkTrace.Info($"keep marks: run {r.Id} ended, {made} item(s) {r.Verb}");
        if (made > 0)
            Communicator.PrintRun(KeepRules.EndSummary(made, r.Verb), r.Label);
    }

    public static void Clear()
    {
        ForkTrace.Info($"keep marks: {Count} cleared by the player");
        _needed = new Dictionary<uint, KeepRules.Mark>();
        _made   = new Dictionary<uint, KeepRules.Mark>();
    }

    public static void Prune()
    {
        if (DateTime.Now < _nextPrune || Count == 0)
            return;

        _nextPrune = DateTime.Now.AddSeconds(2);
        var held = _needed.Keys.Union(_made.Keys).ToDictionary(id => id, Held);
        _needed = KeepRules.Shrink(_needed, held);
        _made   = KeepRules.Shrink(_made, held);
    }

    private static int NoteMade(KeepRules.Run run)
    {
        var before = Open[run];
        var made   = KeepRules.Made(before, before.Keys.ToDictionary(id => id, Held));
        _made = KeepRules.Merge(_made, made.Select(kv => (kv.Key, kv.Value)).ToList(), run);
        return made.Count;
    }

    private static int Held(uint itemId)
    {
        var inventory = InventoryManager.Instance();
        return inventory == null
            ? 0
            : inventory->GetInventoryItemCount(itemId, false, false, true) + inventory->GetInventoryItemCount(itemId, true, false, true);
    }
}
