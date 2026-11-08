using System.Collections.Generic;
using System.Linq;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: a repair the character cannot do by hand holds the run; the reason goes to a toast, the attention note and the run's end-of-run lists
public static class RepairPause
{
    private const string Source = "GatherBuddyReborn repair";

    private static readonly List<string> Issues = new();

    public static bool DarkMatterOnly
        => GatherBuddy.Config.VulcanRepairConfig.DarkMatterOnly;

    public static void Paused(string run, string why)
    {
        Issues.Add(why);
        ForkTrace.Info($"repair ({TextRules.RunName(run)}): paused, {why}");
        Dalamud.ToastGui.ShowNormal(RepairRules.PausedToast(run, why));
        DoctorNote.Set(Source, RepairRules.PausedNote(why));
    }

    public static void SkippedAfterRun(string why, string? label)
    {
        ForkTrace.Info($"after-run repair: skipped, {why}");
        Communicator.PrintRun(RepairRules.AfterRunLine(why), label);
        Dalamud.ToastGui.ShowNormal(RepairRules.AfterRunToast(why));
        NoteAfterRun(why);
    }

    public static void NoteAfterRun(string why)
        => DoctorNote.Set(Source, RepairRules.AfterRunNote(why));

    public static void Repaired()
        => DoctorNote.Set(Source, null);

    public static void BeginRun()
        => Issues.Clear();

    public static void ListAtEnd()
    {
        if (Issues.Count > 0)
            ForkChat.List(RepairRules.IssuesHeader, Issues.Distinct().ToList(), 12);
        Issues.Clear();
    }
}
