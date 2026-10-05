#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GatherBuddy.ForkLogic;

public static class MissionRules
{
    public const string SupplyList       = "GC Supply Missions";
    public const string ProvisioningList = "GC Provisioning Missions";
    public const string Tag              = "[gc-missions]";

    public readonly record struct Mission(uint ItemId, int Requested);

    // the window's other text (headings, counts) names no mission item, so only names from the game's mission table count
    public static List<Mission> FromNames(IEnumerable<string> texts, IReadOnlyDictionary<string, Mission> known, IReadOnlyList<Mission>? exact = null)
    {
        var found = new List<Mission>();
        foreach (var text in texts)
        {
            if (!known.TryGetValue(text.Trim(), out var mission) || found.Any(m => m.ItemId == mission.ItemId))
                continue;

            var counted = exact?.FirstOrDefault(m => m.ItemId == mission.ItemId) ?? default;
            found.Add(counted.ItemId != 0 && counted.Requested > 0 ? counted : mission);
        }

        return found;
    }

    // waiting for a window is the only way left when no always-up node, no gil vendor and not the bags supply the item
    public static bool Waits(int? nodeLocation, int? fishLocation, bool diademRaw, bool soldForGil, int held, int needed)
        => (nodeLocation != null || fishLocation != null)
         && !PurchaseRules.GatheredWithoutWaiting(nodeLocation, fishLocation, diademRaw)
         && !soldForGil
         && held < needed;

    // Grand Company missions turn over at 20:00 UTC, so a day here is one such period, named by the date it began on
    public static string Day(DateTime utc)
        => utc.AddHours(-20).ToString("yyyy-MM-dd");

    public const string NoneRead = Tag + " none read on this character since the daily reset";

    private sealed record Saved(string On, List<Mission> Missions, List<uint>? Tried = null, List<Mission>? All = null);

    public static string Serialize(string day, IEnumerable<Mission> missions, IEnumerable<uint>? tried = null, IEnumerable<Mission>? all = null)
        => JsonSerializer.Serialize(new Saved(day, missions.ToList(), tried?.Distinct().ToList(), all?.ToList()));

    // the missions change at the reset, so a read from another day holds nothing for this one
    public static (List<Mission> Missions, List<uint> Tried, List<Mission> All) SavedFor(string? text, string today)
    {
        try
        {
            var saved = string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<Saved>(text);
            if (saved is not { Missions: not null } || saved.On != today)
                return ([], [], []);

            var open = saved.Missions.Where(m => m.ItemId != 0).ToList();
            return (open, saved.Tried ?? [], Merge(saved.All ?? [], open));
        }
        catch (JsonException)
        {
            return ([], [], []);
        }
    }

    // a window lists only the missions still open, so the day's whole set is what was seen at any point
    public static List<Mission> Merge(IEnumerable<Mission> all, IEnumerable<Mission> open)
    {
        var merged = all.Where(m => m.ItemId != 0).ToList();
        foreach (var mission in open)
            if (merged.All(m => m.ItemId != mission.ItemId))
                merged.Add(mission);
        return merged;
    }

    public static string Tally(string kind, int missions, int listed, IReadOnlyList<string> delivered, IReadOnlyList<string> leftOut)
    {
        var parts = new List<string> { $"{listed} on this list" };
        if (delivered.Count > 0)
            parts.Add($"{delivered.Count} delivered ({string.Join(", ", delivered)})");
        if (leftOut.Count > 0)
            parts.Add($"{leftOut.Count} left out ({string.Join(", ", leftOut)})");
        return $"{missions} {kind} mission(s) today: {string.Join(", ", parts)} (fork).";
    }

    // the bags are asked too, since the officer's list is also empty while it loads
    public sealed class DeliveryWatch
    {
        private int                            _tab = -1;
        private readonly Dictionary<uint, int> _held = new();
        private readonly Dictionary<uint, int> _gone = new();

        // the window often closes right after a delivery, before its list is read again, so there the bags alone decide
        public List<uint> Closed(Func<uint, int> held)
        {
            var delivered = _held.Where(kv => held(kv.Key) < kv.Value).Select(kv => kv.Key).ToList();
            _tab = -1;
            _held.Clear();
            _gone.Clear();
            return delivered;
        }

        public List<uint> Read(int tab, IReadOnlyCollection<uint> listed, Func<uint, int> held, int reads = 2)
        {
            var delivered = tab != _tab ? Closed(held) : [];
            _tab = tab;
            foreach (var id in _held.Keys.Where(id => !listed.Contains(id)).ToList())
            {
                _gone[id] = _gone.GetValueOrDefault(id) + 1;
                if (_gone[id] < reads || held(id) >= _held[id])
                    continue;

                _held.Remove(id);
                _gone.Remove(id);
                delivered.Add(id);
            }

            foreach (var id in listed)
            {
                _held[id] = held(id);
                _gone.Remove(id);
            }

            return delivered;
        }
    }

    public static string Aligned(string character, string day)
        => $"{character} {day}";

    // lists are shared by every character and missions are not, so the two lists are brought in line once per character and mission day
    public static bool Realign(string? character, string day, string? alignedFor, bool busy, bool settled)
        => character != null && !busy && settled && alignedFor != Aligned(character, day);

    public static DateTime NextReset(DateTime utc)
        => utc.AddHours(-20).Date.AddDays(1).AddHours(20);

    public static int Crafts(int requested, int perCraft)
        => (Math.Max(1, requested) + Math.Max(1, perCraft) - 1) / Math.Max(1, perCraft);

    public static string Description(DateTime day, int count)
        => $"{Tag} {day:yyyy-MM-dd}, {count} item(s)";
}
