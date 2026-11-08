using System.Collections.Generic;
using System.Linq;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: a repair the character cannot do by hand holds the run, as a full inventory does; the reason goes to a toast and the run's end-of-run lists
public static class RepairPause
{
    private static readonly List<string> Issues = new();

    public static bool DarkMatterOnly
        => GatherBuddy.Config.VulcanRepairConfig.DarkMatterOnly;

    public static void Paused(string run, string why)
    {
        Issues.Add(why);
        ForkTrace.Info($"repair ({TextRules.RunName(run)}): paused, {why}");
        Dalamud.ToastGui.ShowNormal(RepairRules.PausedToast(run, why));
    }

    public static void SkippedAfterRun(string why, string? label)
    {
        ForkTrace.Info($"after-run repair: skipped, {why}");
        Communicator.PrintRun(RepairRules.AfterRunLine(why), label);
        Dalamud.ToastGui.ShowNormal(RepairRules.AfterRunToast(why));
    }

    public static void BeginRun()
        => Issues.Clear();

    public static void ListAtEnd()
    {
        if (Issues.Count > 0)
            ForkChat.List(RepairRules.IssuesHeader, Issues.Distinct().ToList(), 12);
        Issues.Clear();
    }
}
