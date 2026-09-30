using System;
using System.Collections.Generic;
using System.IO;
using GatherBuddy.Helpers;
using Newtonsoft.Json;

namespace GatherBuddy.AutoGather.Lists;

// Fork only. Which lists are on and which items are ticked belongs to a character: two game windows load the same
// list file, and each window's run must not change what the other character has switched on.
public static class CharacterListState
{
    public sealed class Entry
    {
        public bool       Enabled { get; set; }
        public List<uint> Off     { get; set; } = new();
    }

    private static (string Key, string Text)? _lastWritten;

    public static string? Key()
    {
        var key = ForkLog.CharacterKey();
        return key == "pre-login" ? null : key;
    }

    public static string KeyOf(string folderPath, string name)
        => $"{folderPath}/{name}";

    private static string PathFor(string key)
        => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, $"gather-state-{key}.json");

    /// <summary>Null when no character is logged in; empty on a character's first use.</summary>
    public static Dictionary<string, Entry>? Load()
    {
        var key = Key();
        if (key == null)
            return null;

        try
        {
            var path = PathFor(key);
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<Dictionary<string, Entry>>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[CharacterListState] state file unreadable, starting with every list off: {e.Message}");
        }

        return new();
    }

    public static void Save(Dictionary<string, Entry> state)
    {
        var key = Key();
        if (key == null)
            return;

        try
        {
            var text = JsonConvert.SerializeObject(state, Formatting.Indented);
            if (_lastWritten is { } last && last.Key == key && last.Text == text)
                return;
            File.WriteAllText(PathFor(key), text);
            _lastWritten = (key, text);
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[CharacterListState] state file not saved: {e.Message}");
        }
    }
}
