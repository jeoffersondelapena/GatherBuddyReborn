using System;
using System.Collections.Generic;
using System.Numerics;
using GatherBuddy.Automation;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Crafting;

// fork: goes to a bell the zone files place (BellRules) until one is in sight, then the usual walk takes over; a bell in the
// same zone is walked to, since the vendor route would leave a housing ward and come back in through the city
internal sealed class BellTravel
{
    private static readonly TimeSpan ReadLimit   = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TravelLimit = TimeSpan.FromMinutes(3);
    private const float ArrivalDistance = 5f;

    private readonly VendorNavigator _navigator = new();
    private readonly DateTime _created = DateTime.UtcNow;
    private DateTime _started;
    private BellRules.Bell? _target;
    private bool _walking;
    private bool _goingHome;
    private DateTime _homeSince = DateTime.MinValue;
    private int _walkRestarts;
    private DateTime _nextWalkRestart = DateTime.MinValue;

    public string? Failure { get; private set; }

    public CraftingTasks.TaskResult Tick()
    {
        if (_target == null && !_goingHome)
            return Begin();

        if (RetainerTaskExecutor.FindNearestBellForNavigation() != null)
        {
            Stop();
            ForkTrace.Info("bell travel: a bell is in sight, walking to it");
            return CraftingTasks.TaskResult.Done;
        }

        if (DateTime.UtcNow - _started > TravelLimit)
            return Fail("it took longer than 3 minutes");

        if (_goingHome)
            return AtHome();
        return _walking ? Walk(_target!.Value) : Ride();
    }

    public void Stop()
    {
        _navigator.Stop();
        if (_walking && VNavmesh.Path.IsRunning())
            VNavmesh.Path.Stop();
    }

    private CraftingTasks.TaskResult Begin()
    {
        var territory = Dalamud.ClientState.TerritoryType;
        var chosen    = CharacterSettings.BellZone;
        if (!BellLocations.TryGet(territory, chosen, out var bells))
            return DateTime.UtcNow - _created > ReadLimit ? Fail("the zone files could not be read") : CraftingTasks.TaskResult.Retry;

        // inside an inn room or a house a usable bell would already be in sight, so one the files list there is not one to walk to
        if (Dalamud.GameData.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory)?.TerritoryIntendedUse.RowId is 2 or 14)
            bells.RemoveAll(b => b.Territory == territory);
        if (chosen == BellRules.Home && !bells.Exists(b => b.Territory == territory))
            return GoHome();

        var routes = new Dictionary<uint, uint>();
        uint Route(BellRules.Bell bell)
        {
            if (!routes.TryGetValue(bell.Territory, out var aetheryte))
                routes[bell.Territory] = aetheryte = VendorNavigator.GetPrimaryRouteAetheryteId(bell.Territory, bell.Position);
            return aetheryte;
        }

        var player = Dalamud.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (BellRules.Pick(territory, player, bells, b => TeleportCosts.For(Route(b)), chosen) is not { } bell)
            return Fail("none of the towns with a bell is one you can teleport to");

        if (bell.Territory != territory && Arrival(Route(bell), bell.Territory) is { } arrival)
            bell = BellRules.NearestTo(arrival, bells, bell.Territory) ?? bell;

        var row   = Dalamud.GameData.GetExcelSheet<TerritoryType>().GetRowOrDefault(bell.Territory);
        var place = row?.PlaceName.ValueNullable?.Name.ExtractText() ?? $"zone {bell.Territory}";
        _target  = bell;
        _started = DateTime.UtcNow;
        _walking = bell.Territory == territory;
        if (_walking)
            VNavmesh.SimpleMove.PathfindAndMoveTo(bell.Position, false);
        else
            _navigator.StartNavigation(new VendorNpcLocation(2000401, "Summoning Bell", bell.Territory, row?.Map.RowId ?? 0, bell.Position,
                VendorNpcLocationSource.Lgb));

        ForkTrace.Info($"bell travel: to {place} ({bell.Territory}) at {bell.Position}"
          + (_walking ? ", same zone, walking" : $", teleport {TeleportCosts.For(Route(bell))} gil") + (chosen == BellRules.Automatic ? "" : $", chosen zone {chosen}"));
        Communicator.PrintRun($"[GatherBuddy] No summoning bell in sight for your retainers: going to the one in {place} (fork).", tone: Communicator.Tone.Info);
        return CraftingTasks.TaskResult.Retry;
    }

    // house bells are furniture, not in the zone files: go home instead
    private CraftingTasks.TaskResult GoHome()
    {
        if (!HomeNavigationHelper.TryStartReturnHome(out var error, "to the bell at home"))
            return error != null
                ? Fail(error.TrimEnd('.').ToLowerInvariant())
                : DateTime.UtcNow - _created > ReadLimit ? Fail("Lifestream stayed busy") : CraftingTasks.TaskResult.Retry;

        _goingHome = true;
        _started   = DateTime.UtcNow;
        Communicator.PrintRun("[GatherBuddy] No summoning bell in sight for your retainers: going home to the one in your house (fork).", tone: Communicator.Tone.Info);
        return CraftingTasks.TaskResult.Retry;
    }

    private CraftingTasks.TaskResult AtHome()
    {
        if (!HomeNavigationHelper.IsReturnComplete() || !GenericHelpers.IsScreenReady())
        {
            _homeSince = DateTime.MinValue;
            return CraftingTasks.TaskResult.Retry;
        }

        if (_homeSince == DateTime.MinValue)
            _homeSince = DateTime.UtcNow;
        return DateTime.UtcNow - _homeSince < TimeSpan.FromSeconds(3)
            ? CraftingTasks.TaskResult.Retry
            : Fail("no summoning bell is in sight at home");
    }

    private CraftingTasks.TaskResult Ride()
    {
        _navigator.Update();
        if (_navigator.IsFailed)
            return Fail("the way there failed");
        return _navigator.IsReadyToPurchase ? CraftingTasks.TaskResult.Done : CraftingTasks.TaskResult.Retry;
    }

    private CraftingTasks.TaskResult Walk(BellRules.Bell target)
    {
        var player = Dalamud.Objects.LocalPlayer?.Position ?? Vector3.Zero;
        if (player != Vector3.Zero && Vector3.Distance(player, target.Position) <= ArrivalDistance)
        {
            Stop();
            return CraftingTasks.TaskResult.Done;
        }

        if (VNavmesh.Path.IsRunning() || DateTime.UtcNow < _nextWalkRestart)
            return CraftingTasks.TaskResult.Retry;
        if (++_walkRestarts > 5)
            return Fail("no path to it");

        _nextWalkRestart = DateTime.UtcNow.AddSeconds(1);
        VNavmesh.SimpleMove.PathfindAndMoveTo(target.Position, false);
        return CraftingTasks.TaskResult.Retry;
    }

    private CraftingTasks.TaskResult Fail(string why)
    {
        Stop();
        Failure = why;
        ForkTrace.Info($"bell travel: gave up, {why}");
        return CraftingTasks.TaskResult.Done;
    }

    // where a teleport to this aetheryte lands, when that is in the bell's own zone
    private static Vector2? Arrival(uint aetheryteId, uint territory)
        => Dalamud.GameData.GetExcelSheet<Aetheryte>().GetRowOrDefault(aetheryteId) is { } aetheryte && aetheryte.Territory.RowId == territory
            ? VendorNavigator.GetAetheryteXZ(aetheryte)
            : null;
}
