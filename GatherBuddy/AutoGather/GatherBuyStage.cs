using System;
using System.Collections.Generic;
using System.Linq;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;

namespace GatherBuddy.AutoGather;

// fork: before gathering, a plain run buys what its lists buy instead; it pauses on a shortfall as a crafting run's buying does
internal static class GatherBuyStage
{
    private enum Step { Off, Start, Buying, Paused }

    private static Step                    _step = Step.Off;
    private static AutoGatherListsManager? _lists;
    private static DateTime                _startBy;
    private static List<string>            _noVendor = [];

    public static bool Skipped { get; private set; }

    public static bool Moved { get; private set; }

    public static bool Paused
        => _step == Step.Paused;

    public static string? Reason { get; private set; }

    public static void Begin(AutoGatherListsManager lists)
    {
        Reset();
        _lists = lists;
        _step  = Step.Start;
    }

    public static void Reset()
    {
        if (_step == Step.Buying)
            GatherBuddy.VendorBuyListManager?.CancelRunPurchase();
        _step    = Step.Off;
        _lists   = null;
        Skipped  = false;
        Moved    = false;
        Reason   = null;
        _startBy = DateTime.MinValue;
        _noVendor.Clear();
    }

    public static bool Hold(out string status)
    {
        status = "Buying instead of gathering...";
        switch (_step)
        {
            case Step.Off:
                return false;
            case Step.Paused:
                status = $"Paused: {Reason}";
                return true;
            case Step.Start:
                Start();
                return _step != Step.Off;
            case Step.Buying:
                Buying();
                return _step != Step.Off;
        }

        return false;
    }

    private static List<(uint ItemId, uint Target)> Targets()
        => _lists?.BuyTargets() ?? [];

    private static List<string> Named(IEnumerable<(uint ItemId, uint Target)> targets)
        => targets.Select(t => ForkTrace.ItemName(t.ItemId)).ToList();

    private static void Start()
    {
        var targets = Targets();
        if (targets.Count == 0 || GatherBuddy.VendorBuyListManager is not { } manager)
        {
            _step = Step.Off;
            return;
        }

        if (!VendorAutomationRequirements.IsAvailable)
        {
            Pause(VendorAutomationRequirements.UnavailableStatusText, Named(targets));
            return;
        }

        if (_startBy == DateTime.MinValue)
            _startBy = DateTime.Now.AddMinutes(1);
        var requests = targets.Select(t => new VendorBuyListManager.VendorTargetRequest(t.ItemId, t.Target)).ToList();
        var result   = manager.StartForRun(RunLabel.Current ?? "gathering run", requests, out var noVendor);
        switch (result)
        {
            case VendorBuyListManager.StartResult.Started or VendorBuyListManager.StartResult.WaitingForPreviousInteraction:
                _noVendor = noVendor.Select(id => $"{ForkTrace.ItemName(id)} (no vendor GatherBuddy can walk to)").ToList();
                var buying = Named(targets.Where(t => !noVendor.Contains(t.ItemId)));
                ForkTrace.Info($"gathering run: buying instead of gathering: {string.Join(", ", buying)}");
                ForkChat.List("Buying first, instead of gathering:", buying, tone: Communicator.Tone.Info);
                _step = Step.Buying;
                return;
            case VendorBuyListManager.StartResult.NoPendingEntries:
                _step = Step.Off;
                return;
            case VendorBuyListManager.StartResult.Empty:
                Pause("no vendor GatherBuddy can walk to sells what is missing", Named(targets));
                return;
        }

        if (DateTime.Now >= _startBy)
            Pause(result == VendorBuyListManager.StartResult.AlreadyRunning
                ? "another vendor run was still going after a minute"
                : "the vendor data did not load within a minute", Named(targets));
    }

    private static void Buying()
    {
        if (GatherBuddy.VendorBuyListManager is not { } manager)
        {
            _step = Step.Off;
            return;
        }

        Moved |= manager.SetOut;
        if (manager.RunPurchase == VendorBuyListManager.RunPurchaseOutcome.Running || manager.IsBusy)
            return;

        var (outcome, detail, notBought) = manager.TakeRunPurchase();
        notBought.AddRange(_noVendor);
        ForkTrace.Info($"gathering run: buying {outcome}{(detail.Length > 0 ? $" ({detail})" : "")}; not bought: "
          + (notBought.Count == 0 ? "none" : string.Join(", ", notBought)));
        switch (outcome)
        {
            case VendorBuyListManager.RunPurchaseOutcome.Finished when notBought.Count == 0:
                _step = Step.Off;
                return;
            case VendorBuyListManager.RunPurchaseOutcome.Finished:
                Pause(PurchaseRules.NotBought(notBought.Count), notBought);
                return;
            case VendorBuyListManager.RunPurchaseOutcome.BagsFull:
                Pause(PurchaseRules.BagsFull, Named(Targets()));
                return;
            default:
                Pause(detail.Length > 0 ? detail : "the vendor run ended early", Named(Targets()));
                return;
        }
    }

    private static void Pause(string why, IReadOnlyList<string> missing)
    {
        _step  = Step.Paused;
        Reason = why;
        Communicator.PrintRun(TextRules.StoppedShort(TextRules.BuyList, why, TextRules.Gathering));
        ForkTrace.Info($"gathering run paused in its buying part: {why}");
        if (missing.Count > 0)
            ForkChat.List("Still missing:", missing, 12);
        KeepMarks.MarkPause(GatherBuddy.AutoGather.KeepRun, []);
        if (Moved)
            PauseHome.Request("buying");
        else
            ForkTrace.Info("gathering run paused before it went anywhere, so it stays where it was started");
    }

    public static void Resume()
    {
        if (_step != Step.Paused)
            return;

        ForkTrace.Info("gathering run: buying part resumed");
        Reason   = null;
        _startBy = DateTime.MinValue;
        _step    = Step.Start;
    }

    public static void Skip()
    {
        if (_step != Step.Paused)
            return;

        ForkTrace.Info("gathering run: buying part skipped; it gathers those items instead");
        Communicator.PrintRun("[GatherBuddy] Skipping the buying: this run gathers those items instead, waiting where one needs a time or "
          + "weather window (fork).", tone: Communicator.Tone.Info);
        Skipped = true;
        Reason  = null;
        _step   = Step.Off;
    }
}
