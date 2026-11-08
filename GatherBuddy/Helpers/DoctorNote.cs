using System;
using System.IO;
using GatherBuddy.ForkLogic;

namespace GatherBuddy.Helpers;

// Fork only. XIV Helper repeats each line of its attention file in chat every half hour, so a problem lives there until it is gone.
public static class DoctorNote
{
    private const string Source = "GatherBuddyReborn crafting";
    private const string Reader = "XIVHelper";

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

            var path = Path.Combine(configs.FullName, Reader, "attention.txt");
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
