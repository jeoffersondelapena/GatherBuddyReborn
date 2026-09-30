using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GatherBuddy.Helpers;

// Fork only. XIV Doctor repeats each line of its attention file in chat every half hour; the plugin's own chat line
// about a failed solve scrolls past once, so the failure also lives there until the solver works again.
public static class DoctorNote
{
    private const string Source = "GatherBuddyReborn crafting";

    private static readonly object Lock = new();

    public static string Brief(string? reason)
    {
        var line = (reason ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "unknown";
        return line.Length > 90 ? line[..87] + "..." : line;
    }

    public static void Set(string? note)
    {
        try
        {
            var configs = Dalamud.PluginInterface.ConfigDirectory.Parent;
            if (configs == null)
                return;

            var path = Path.Combine(configs.FullName, "XIVDoctor", "attention.txt");
            lock (Lock)
            {
                var kept = File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Trim().Length > 0).ToList() : new List<string>();
                var mine = kept.FindIndex(l => l.StartsWith(Source + ":"));
                if ((note != null) == (mine >= 0))
                    return;

                if (mine >= 0)
                    kept.RemoveAt(mine);
                else
                    kept.Add($"{Source}: {note}");

                if (kept.Count > 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllLines(path, kept);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[DoctorNote] attention file not updated: {ex.Message}");
        }
    }
}
