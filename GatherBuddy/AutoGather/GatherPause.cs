using System.Collections.Generic;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;

namespace GatherBuddy.AutoGather;

// fork: a plain gathering run that stops with items left waits here, auto-gather still on, so Resume keeps the run's starting counts
internal static class GatherPause
{
    public static string? Reason { get; private set; }

    public static bool Paused
        => Reason != null;

    public static void Start(string? why, IReadOnlyList<string> missing)
    {
        Reason = string.IsNullOrWhiteSpace(why) ? "it could not go on" : why.Trim().TrimEnd('.');
        Communicator.PrintRun(TextRules.GatheringRunStopped(Reason));
        ForkTrace.Info($"gathering run paused: {Reason}; still missing: {(missing.Count == 0 ? "nothing" : string.Join(", ", missing))}");
        if (missing.Count > 0)
            ForkChat.List("Still missing:", missing, 12);
        KeepMarks.MarkPause(GatherBuddy.AutoGather.KeepRun, []);
    }

    public static void Resume()
    {
        if (!Paused)
            return;

        ForkTrace.Info("gathering run resumed");
        Reason = null;
    }

    public static void Reset()
        => Reason = null;
}
