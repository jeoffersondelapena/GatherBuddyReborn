using System;
using System.IO;
using GatherBuddy.ForkLogic;

namespace GatherBuddy.Helpers;

// Fork only. XIV Doctor repeats each line of its attention file in chat every half hour; the plugin's own chat line
// about a failed solve scrolls past once, so the failure also lives there until the solver works again.
public static class DoctorNote
{
    private const string Source = "GatherBuddyReborn crafting";

    private static readonly object Lock = new();

    public static string Brief(string? reason)
        => TextRules.FirstLine(reason);

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
                var kept = TextRules.WithNote(File.Exists(path) ? File.ReadAllLines(path) : [], Source, note);
                if (kept == null)
                    return;

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
