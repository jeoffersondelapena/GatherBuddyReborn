using System;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: the end divider waits out what still follows a run (the after-run repair, the trip home), so it is the run's last line
public static class RunChat
{
    private static readonly TimeSpan TravelLimit = TimeSpan.FromMinutes(2);

    private static string?  _open;
    private static bool     _ending;
    private static DateTime _quietSince;
    private static DateTime _travelSince;
    private static DateTime _endBy;

    public static void Begin(string? label)
    {
        if (label == null || (label == _open && !_ending))
            return;

        if (_open != null)
            Close();

        _open   = label;
        _ending = false;
        Communicator.PrintRun(TextRules.DividerStart(label), label, Communicator.Tone.Info);
    }

    public static void End()
    {
        if (_open == null || _ending)
            return;

        _ending      = true;
        _quietSince  = DateTime.MinValue;
        _travelSince = DateTime.MinValue;
        _endBy       = DateTime.Now.AddMinutes(10);
    }

    public static void Update()
    {
        if (!_ending)
            return;

        var limited    = AfterRunRepair.Busy || CraftingGatherBridge.IsQueueMode
         || GatherBuddy.VendorBuyListManager is { } vendor && (vendor.IsBusy || vendor.HomeTripPending);
        var travelling = !limited && Lifestream.Enabled && Lifestream.IsBusy();
        _travelSince = travelling ? (_travelSince == DateTime.MinValue ? DateTime.Now : _travelSince) : DateTime.MinValue;
        if (DateTime.Now < _endBy && (limited || travelling && DateTime.Now - _travelSince < TravelLimit))
        {
            _quietSince = DateTime.MinValue;
            return;
        }

        if (_quietSince == DateTime.MinValue)
            _quietSince = DateTime.Now;
        else if (DateTime.Now - _quietSince >= TimeSpan.FromSeconds(2))
            Close();
    }

    private static void Close()
    {
        var label = _open!;
        _open   = null;
        _ending = false;
        Communicator.PrintRun(TextRules.DividerEnd(label), label, Communicator.Tone.Info);
    }
}
