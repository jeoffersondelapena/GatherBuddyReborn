using System;
using System.Collections.Generic;
using System.IO;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;

namespace GatherBuddy.AutoGather.Lists;

// Fork only. Which lists are on and which items are ticked belongs to a character: two game windows load the same
// list file, and each window's run must not change what the other character has switched on.
public static class CharacterListState
{
    private static (string Key, string Text)? _lastWritten;

    public static string? Key()
    {
        var key = ForkLog.CharacterKey();
        return key == ForkLog.NoCharacter ? null : key;
    }

    public static string KeyOf(AutoGatherList list)
        => ListStateRules.KeyOf(list.FolderPath, list.Name);

    private static string PathFor(string key)
        => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, $"gather-state-{key}.json");

    public static Dictionary<string, ListStateRules.Entry> Load(string key)
    {
        var path = PathFor(key);
        try
        {
            if (File.Exists(path))
                return ListStateRules.Deserialize(SafeFile.Read(path, attempts: 1));
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[CharacterListState] {path} unreadable, kept as .unreadable and starting with every list off: {e.Message}");
            try
            {
                File.Copy(path, path + ".unreadable", true);
            }
            catch (Exception)
            {
            }
        }

        return new();
    }

    // A reset of the generated lists clears every character's choices for them, logged in or not, as the list generator's write does.
    public static void ForgetEverywhere(IReadOnlySet<string> keys)
    {
        foreach (var path in Directory.GetFiles(Dalamud.PluginInterface.ConfigDirectory.FullName, "gather-state-*.json"))
        {
            var key   = Path.GetFileNameWithoutExtension(path)["gather-state-".Length..];
            var state = Load(key);
            if (ListStateRules.Forget(state, keys))
                Save(key, state);
        }
    }

    public static void Save(string key, Dictionary<string, ListStateRules.Entry> state)
    {
        try
        {
            var text = ListStateRules.Serialize(state);
            if (_lastWritten is { } last && last.Key == key && last.Text == text)
                return;

            SafeFile.Write(PathFor(key), text);
            _lastWritten = (key, text);
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[CharacterListState] state file not saved: {e.Message}");
        }
    }
}
