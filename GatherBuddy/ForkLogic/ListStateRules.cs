#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GatherBuddy.ForkLogic;

// Fork only. Nothing in this folder uses a game or plugin type, so GatherBuddy.Tests compiles these files as they ship.
public static class ListStateRules
{
    public sealed class Entry
    {
        public bool       Enabled { get; set; }
        public List<uint> Off     { get; set; } = new();
    }

    public readonly record struct ListView(string Key, bool Enabled, IReadOnlyCollection<uint> Off);

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static string KeyOf(string? folderPath, string name)
        => $"{folderPath}/{name}";

    public static Dictionary<string, Entry> Capture(IEnumerable<ListView> lists)
    {
        var state = new Dictionary<string, Entry>();
        foreach (var list in lists)
        {
            var off = list.Off.Distinct().OrderBy(id => id).ToList();
            if (list.Enabled || off.Count > 0)
                state[list.Key] = new Entry { Enabled = list.Enabled, Off = off };
        }

        return state;
    }

    public static Entry For(IReadOnlyDictionary<string, Entry>? state, string key)
        => state != null && state.TryGetValue(key, out var entry) ? entry : new Entry();

    // A character's file is only ever written from the state that was loaded for that character.
    public static bool MayWrite(string? appliedFor, string? current)
        => current != null && appliedFor == current;

    public static string Serialize(Dictionary<string, Entry> state)
        => JsonSerializer.Serialize(state, Indented);

    public static Dictionary<string, Entry> Deserialize(string text)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, Entry?>>(text) ?? new();
        return state.Where(kv => kv.Value != null)
            .ToDictionary(kv => kv.Key, kv => new Entry { Enabled = kv.Value!.Enabled, Off = kv.Value.Off ?? new() });
    }

    public static bool SameJson(string a, string b)
    {
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
