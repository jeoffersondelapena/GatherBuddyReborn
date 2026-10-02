using System;
using Dalamud.Game.ClientState.Objects.Types;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;

namespace GatherBuddy.Crafting;

// fork, temporary: runs the crafting run's bell trip on its own, for testing in game
internal static class BellTravelTest
{
    private static BellTravel? _travel;
    private static RetainerBellNavigator? _walk;
    private static DateTime _started;

    public static bool Running
        => _travel != null || _walk != null;

    public static void Start()
    {
        if (Running || CraftingGatherBridge.IsQueueMode)
            return;

        _started = DateTime.UtcNow;
        ForkTrace.Info($"bell travel test: started in territory {Dalamud.ClientState.TerritoryType}");
        if (RetainerTaskExecutor.FindNearestBellForNavigation() is { } bell)
        {
            Communicator.PrintRun("[GatherBuddy] Bell travel test: a summoning bell is in sight, walking to it (fork).", tone: Communicator.Tone.Info);
            Walk(bell);
            return;
        }

        _travel = new BellTravel();
    }

    public static void Stop()
    {
        _travel?.Stop();
        _walk?.Stop();
        if (Running)
            End("stopped by you");
    }

    public static void Update()
    {
        if (Running && CraftingGatherBridge.IsQueueMode)
        {
            _travel?.Stop();
            _walk?.Stop();
            End("a crafting run started");
            return;
        }

        if (_travel != null)
        {
            if (_travel.Tick() == CraftingTasks.TaskResult.Retry)
                return;

            var failure = _travel.Failure;
            _travel = null;
            if (failure != null)
                End($"could not get to a summoning bell: {failure}");
            else if (RetainerTaskExecutor.FindNearestBellForNavigation() is { } bell)
                Walk(bell);
            else
                End("the trip ended with no summoning bell in sight");
            return;
        }

        if (_walk == null)
            return;

        _walk.Update();
        if (_walk.IsComplete)
            End(_walk.IsFailed ? "the walk to the bell failed" : "standing at a summoning bell");
    }

    private static void Walk(IGameObject bell)
    {
        _walk = new RetainerBellNavigator();
        if (!_walk.StartNavigation(bell))
            End("the walk to the bell could not start");
    }

    private static void End(string result)
    {
        _travel = null;
        _walk   = null;
        var seconds = (int)(DateTime.UtcNow - _started).TotalSeconds;
        ForkTrace.Info($"bell travel test: {result} after {seconds} s, in territory {Dalamud.ClientState.TerritoryType}");
        Communicator.PrintRun($"[GatherBuddy] Bell travel test: {result} after {seconds} s (fork).",
            tone: result.StartsWith("standing", StringComparison.Ordinal) ? Communicator.Tone.Good : Communicator.Tone.Problem);
    }
}
