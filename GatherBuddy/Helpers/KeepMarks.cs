using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
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

    private static readonly InventoryType[] Holding =
    [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody,
        InventoryType.ArmoryHands, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets, InventoryType.ArmoryEar,
        InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings, InventoryType.EquippedItems,
    ];
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

    public static void Start()
        => Dalamud.GameInventory.InventoryChanged += OnInventoryChanged;

    public static void Stop()
        => Dalamud.GameInventory.InventoryChanged -= OnInventoryChanged;

    // green follows the bags as items arrive, so a crash or a reload mid-run keeps what the run had made
    private static void OnInventoryChanged(IReadOnlyCollection<InventoryEventArgs> events)
    {
        if (Open.Count == 0)
            return;

        var changed = events.Select(e => e.Item.BaseItemId).Where(id => id != 0).ToHashSet();
        foreach (var run in Open.Where(kv => kv.Value.Keys.Any(changed.Contains)).Select(kv => kv.Key).ToList())
            NoteMade(run);
    }

    public static KeepRules.Run BeginRun(string? label, string verb, IEnumerable<(uint ItemId, int Target)> targets)
    {
        var run = new KeepRules.Run(++_nextRun, string.IsNullOrWhiteSpace(label) ? "this run" : label, verb);
        Open[run] = targets.Select(t => (Id: KeepRules.BaseItemId(t.ItemId), t.Target))
            .Where(t => t.Id != 0 && t.Target > 0)
            .GroupBy(t => t.Id)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Target));
        var counted = NoteMade(run);
        ForkTrace.Info($"keep marks: run {run.Id} ({run.Label}, {verb}) counts {Open[run].Count} item(s) toward their targets, {counted.Count} already held");
        return run;
    }

    public static void MarkPause(KeepRules.Run? run, IReadOnlyList<(uint ItemId, int Count)> needed)
    {
        if (run is not { } r)
            return;

        _needed = KeepRules.Merge(_needed, needed, r);
        var made   = NoteMade(r).Count;
        var marked = _needed.Values.Count(m => m.ByRun.ContainsKey(r));
        ForkTrace.Info($"keep marks: run {r.Id} paused; still needed {string.Join(", ", needed.Where(i => i.Count > 0).Select(i => $"{ForkTrace.Named(i.ItemId)} x{i.Count}"))}; made so far {made}");
        if (marked > 0 || made > 0)
            Communicator.PrintRun(KeepRules.PauseSummary(marked, made), TextRules.KindLabel(TextRules.KindOfVerb(r.Verb), r.Label), Communicator.Tone.Needed);
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
        if (run is not { } r || !Open.Remove(r, out var before))
            return;

        _needed = KeepRules.WithoutRun(_needed, r);
        var held    = Held(before.Keys);
        var counted = Merge(r, KeepRules.Counted(before, held));
        var none    = before.Keys.Where(id => !counted.ContainsKey(id)).ToList();
        ForkTrace.Info($"keep marks: run {r.Id} ended, {counted.Count} of {before.Count} item(s) held toward their targets"
          + (none.Count == 0 ? "" : $"; none held: {string.Join(", ", none.Select(id => $"{ForkTrace.Named(id)} (target {before[id]})"))}"));
        if (counted.Count > 0)
            Communicator.PrintRun(KeepRules.EndSummary(counted.Count), TextRules.KindLabel(TextRules.KindOfVerb(r.Verb), r.Label), Communicator.Tone.Good);
    }

    public static void ClearNeeded()
    {
        ForkTrace.Info($"keep marks: {_needed.Count} orange cleared by the player");
        _needed = new Dictionary<uint, KeepRules.Mark>();
    }

    public static void ClearMade()
    {
        ForkTrace.Info($"keep marks: {_made.Count} green cleared by the player");
        _made = new Dictionary<uint, KeepRules.Mark>();
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
        var held = Held(_needed.Keys.Union(_made.Keys));
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

    private static Dictionary<uint, int> NoteMade(KeepRules.Run run)
        => Merge(run, KeepRules.Counted(Open[run], Held(Open[run].Keys)));

    private static Dictionary<uint, int> Merge(KeepRules.Run run, Dictionary<uint, int> made)
    {
        _made = KeepRules.Merge(_made, made.Select(kv => (kv.Key, kv.Value)).ToList(), run);
        Save();
        return made;
    }

    // what is worn counts too: a run's gearset change moves gear between the armoury and the character, which must not read as a loss
    private static Dictionary<uint, int> Held(IEnumerable<uint> itemIds)
    {
        var held      = itemIds.Distinct().ToDictionary(id => id, _ => 0);
        var inventory = InventoryManager.Instance();
        if (inventory == null || held.Count == 0)
            return held;

        foreach (var type in Holding)
        {
            var container = inventory->GetInventoryContainer(type);
            if (container == null)
                continue;

            for (var i = 0; i < container->Size; i++)
            {
                var slot = container->GetInventorySlot(i);
                if (slot != null && slot->ItemId != 0 && held.ContainsKey(slot->GetBaseItemId()))
                    held[slot->GetBaseItemId()] += slot->Quantity;
            }
        }

        return held;
    }
}
