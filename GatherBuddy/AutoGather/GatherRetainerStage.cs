using System.Collections.Generic;
using System.Linq;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;

namespace GatherBuddy.AutoGather;

// fork: Allagan Tools' record decides; a record that knows no retainers still sends the run to a bell, as crafting runs do
internal static class GatherRetainerStage
{
    private enum Step { Off, Check, Travel, Walk, Withdraw, Paused }

    private static Step                    _step = Step.Off;
    private static Dictionary<uint, int>   _targets = new();
    private static bool                    _travelTried;
    private static BellTravel?             _travel;
    private static RetainerBellNavigator?  _walk;
    private static RetainerTaskExecutor?   _executor;

    public static bool Skipped { get; private set; }

    public static bool Paused
        => _step == Step.Paused;

    public static string? Reason { get; private set; }

    public static void Begin(AutoGatherListsManager lists)
    {
        Reset();
        if (!GatherBuddy.Config.AutoGatherConfig.CheckRetainers || !AllaganTools.Enabled)
            return;

        _targets = lists.ActiveItems.Where(i => lists.UsesRetainerInventory(i.Item))
            .GroupBy(i => i.Item.ItemId)
            .ToDictionary(g => g.Key, g => (int)System.Math.Min(g.Sum(i => (long)i.Quantity), int.MaxValue));
        if (_targets.Count > 0)
            _step = Step.Check;
    }

    public static void Reset()
    {
        _travel?.Stop();
        _walk?.Stop();
        _travel      = null;
        _walk        = null;
        _executor    = null;
        _travelTried = false;
        _step        = Step.Off;
        Skipped      = false;
        Reason       = null;
    }

    public static bool Hold(out string status)
    {
        status = "Taking from your retainers...";
        switch (_step)
        {
            case Step.Off:
                return false;
            case Step.Paused:
                status = $"Paused: {Reason}";
                return true;
            case Step.Check:
                Check();
                return _step != Step.Off;
            case Step.Travel:
                if (_travel!.Tick() == CraftingTasks.TaskResult.Done)
                {
                    var failure = _travel.Failure;
                    _travel = null;
                    if (failure != null)
                        Pause($"no summoning bell could be reached ({failure})");
                    else
                        _step = Step.Check;
                }
                return true;
            case Step.Walk:
                _walk!.Update();
                if (_walk.IsComplete)
                {
                    _walk     = null;
                    _executor = new RetainerTaskExecutor(_targets, new Dictionary<uint, IngredientQualityDemand>());
                    _step     = Step.Withdraw;
                }
                return true;
            case Step.Withdraw:
                if (_executor!.Tick() == CraftingTasks.TaskResult.Done)
                {
                    var executor = _executor;
                    _executor = null;
                    if (executor.IsAborted)
                        Pause(executor.FullBags ? PurchaseRules.BagsFull : "the summoning bell or the retainer window did not respond");
                    else
                    {
                        ForkTrace.Info("gathering run: took what the lists count from your retainers");
                        _step = Step.Off;
                    }
                }
                return _step != Step.Off;
        }

        return false;
    }

    private static void Check()
    {
        if (RetainerTaskExecutor.WouldTakeAnything(_targets, new Dictionary<uint, IngredientQualityDemand>(), []) == false)
        {
            ForkTrace.Info("gathering run: Allagan Tools shows nothing these lists count in your retainers, so no bell");
            _step = Step.Off;
            return;
        }

        var bell = RetainerTaskExecutor.FindNearestBellForNavigation();
        if (bell == null && !_travelTried)
        {
            _travelTried = true;
            _travel      = new BellTravel();
            _step        = Step.Travel;
            return;
        }

        if (bell == null)
        {
            Pause("no summoning bell was in sight where the trip ended");
            return;
        }

        _walk = new RetainerBellNavigator();
        if (_walk.StartNavigation(bell))
        {
            _step = Step.Walk;
            return;
        }

        _walk     = null;
        _executor = new RetainerTaskExecutor(_targets, new Dictionary<uint, IngredientQualityDemand>());
        _step     = Step.Withdraw;
    }

    private static void Pause(string why)
    {
        _step  = Step.Paused;
        Reason = why;
        Communicator.PrintRun(TextRules.StoppedShort(TextRules.Retainers, why, gatheringRun: true));
        ForkTrace.Info($"gathering run paused in its retainer part: {why}");
        if (RetainerTaskExecutor.WouldTake(_targets, new Dictionary<uint, IngredientQualityDemand>(), []) is { Count: > 0 } held)
            ForkChat.List("Your retainers hold for this run:", held.Select(kv => $"{ForkTrace.ItemName(kv.Key)} x{kv.Value}").ToList(), 12);
        KeepMarks.MarkPause(GatherBuddy.AutoGather.KeepRun, []);
        PauseHome.Request("retainer");
    }

    public static void Resume()
    {
        if (_step != Step.Paused)
            return;

        ForkTrace.Info("gathering run: retainer part resumed");
        _travelTried = false;
        Reason       = null;
        _step        = Step.Check;
    }

    public static void Skip()
    {
        if (_step != Step.Paused)
            return;

        ForkTrace.Info("gathering run: retainer part skipped; the lists now count only your bags");
        Communicator.PrintRun("[GatherBuddy] Skipping your retainers: this run gathers the full amounts into your bags instead (fork).",
            tone: Communicator.Tone.Info);
        Skipped = true;
        Reason  = null;
        _step   = Step.Off;
    }
}
