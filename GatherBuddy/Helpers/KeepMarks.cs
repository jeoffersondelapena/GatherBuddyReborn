using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: green is saved per character (keep-marks-<key>.json, the key other per-character files use), so two windows never share marks
public static unsafe class KeepMarks
{
    private static Dictionary<uint, KeepRules.Mark> _needed = new();
    private static Dictionary<uint, KeepRules.Mark> _made   = new();

    private static readonly Dictionary<KeepRules.Run, Dictionary<uint, int>> Open = new();
    private static int      _nextRun;
    private static DateTime _nextPrune;
    private static string?  _loadedFor;
    private static string?  _lastSaved;

    public static int Count
        => _needed.Keys.Union(_made.Keys).Count();

    public static int NeededCount
        => _needed.Count;

    public static int MadeCount
        => _made.Count;

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

    public static void ClearNeeded()
    {
        ForkTrace.Info($"keep marks: {_needed.Count} orange cleared by the player");
        _needed = new Dictionary<uint, KeepRules.Mark>();
    }

    // a run still going counts from the clear on, so what it made before stays cleared
    public static void ClearMade()
    {
        ForkTrace.Info($"keep marks: {_made.Count} green cleared by the player");
        _made = new Dictionary<uint, KeepRules.Mark>();
        foreach (var run in Open.Keys.ToList())
            Open[run] = Open[run].Keys.ToDictionary(id => id, Held);
        Save();
    }

    public static void Update()
    {
        var key = ForkLog.CharacterKey() is var k && k != ForkLog.NoCharacter ? k : null;
        if (key != _loadedFor)
            SwitchTo(key);

        if (DateTime.Now < _nextPrune || Count == 0 || !InventoryReady())
            return;

        _nextPrune = DateTime.Now.AddSeconds(2);
        var held = _needed.Keys.Union(_made.Keys).ToDictionary(id => id, Held);
        _needed = KeepRules.Shrink(_needed, held);
        var made = KeepRules.Shrink(_made, held);
        if (made.Count == _made.Count && made.All(kv => ReferenceEquals(kv.Value, _made.GetValueOrDefault(kv.Key))))
            return;

        _made = made;
        Save();
    }

    private static void SwitchTo(string? key)
    {
        Open.Clear();
        _needed    = new Dictionary<uint, KeepRules.Mark>();
        _made      = new Dictionary<uint, KeepRules.Mark>();
        _nextRun   = 0;
        _loadedFor = key;
        _lastSaved = null;
        _nextPrune = DateTime.Now.AddSeconds(10);
        if (key == null)
            return;

        var path = PathFor(key);
        try
        {
            if (File.Exists(path))
            {
                _lastSaved        = SafeFile.Read(path, attempts: 1);
                (_made, _nextRun) = KeepRules.Deserialize(_lastSaved);
            }
        }
        catch (Exception e)
        {
            _lastSaved = null;
            GatherBuddy.Log.Warning($"[KeepMarks] {path} unreadable, starting without green marks: {e.Message}");
        }

        ForkTrace.Info($"keep marks: {_made.Count} green mark(s) loaded for this character");
    }

    private static void Save()
    {
        if (_loadedFor == null)
            return;

        var text = KeepRules.Serialize(_made, _nextRun);
        if (text == _lastSaved)
            return;

        try
        {
            SafeFile.Write(PathFor(_loadedFor), text);
            _lastSaved = text;
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[KeepMarks] green marks not saved: {e.Message}");
        }
    }

    private static string PathFor(string key)
        => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, $"keep-marks-{key}.json");

    // right after login the containers can still read empty, which would shrink every saved mark away
    private static bool InventoryReady()
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null)
            return false;

        foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.ArmoryMainHand })
        {
            var container = inventory->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded)
                return false;
        }

        return true;
    }

    private static int NoteMade(KeepRules.Run run)
    {
        var before = Open[run];
        var made   = KeepRules.Made(before, before.Keys.ToDictionary(id => id, Held));
        _made = KeepRules.Merge(_made, made.Select(kv => (kv.Key, kv.Value)).ToList(), run);
        Save();
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
