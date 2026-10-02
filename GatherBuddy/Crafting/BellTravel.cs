using System;
using System.Collections.Generic;
using System.Numerics;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Crafting;

internal sealed class BellTravel
{
    private static readonly TimeSpan ReadLimit   = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan TravelLimit = TimeSpan.FromMinutes(3);

    private readonly VendorNavigator _navigator = new();
    private readonly DateTime _created = DateTime.UtcNow;
    private DateTime _started;
    private BellRules.Bell? _target;

    public string? Failure { get; private set; }

    public CraftingTasks.TaskResult Tick()
    {
        if (_target == null)
            return Begin();

        if (RetainerTaskExecutor.FindNearestBellForNavigation() != null)
        {
            Stop();
            ForkTrace.Info("bell travel: a bell is in sight, walking to it");
            return CraftingTasks.TaskResult.Done;
        }

        if (DateTime.UtcNow - _started > TravelLimit)
            return Fail("it took longer than 3 minutes");

        return Ride();
    }

    public void Stop()
        => _navigator.Stop();

    private CraftingTasks.TaskResult Begin()
    {
        var territory = Dalamud.ClientState.TerritoryType;
        var chosen    = CharacterSettings.BellZone;
        if (!BellLocations.TryGet(territory, chosen, out var bells))
            return DateTime.UtcNow - _created > ReadLimit ? Fail("the zone files could not be read") : CraftingTasks.TaskResult.Retry;

        // inside an inn room or a house a usable bell would already be in sight, so one the files list there is not one to walk to
        if (Dalamud.GameData.GetExcelSheet<TerritoryType>().GetRowOrDefault(territory)?.TerritoryIntendedUse.RowId is 2 or 14)
            bells.RemoveAll(b => b.Territory == territory);

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
        _navigator.StartNavigation(new VendorNpcLocation(2000401, "Summoning Bell", bell.Territory, row?.Map.RowId ?? 0, bell.Position,
            VendorNpcLocationSource.Lgb));

        ForkTrace.Info($"bell travel: to {place} ({bell.Territory}) at {bell.Position}"
          + (bell.Territory == territory ? ", same zone" : $", teleport {TeleportCosts.For(Route(bell))} gil") + (chosen == BellRules.Automatic ? "" : $", chosen zone {chosen}"));
        Communicator.PrintRun($"[GatherBuddy] No summoning bell in sight for your retainers: going to the one in {place} (fork).", tone: Communicator.Tone.Info);
        return CraftingTasks.TaskResult.Retry;
    }

    private CraftingTasks.TaskResult Ride()
    {
        _navigator.Update();
        if (_navigator.IsFailed)
            return Fail("the way there failed");
        return _navigator.IsReadyToPurchase ? CraftingTasks.TaskResult.Done : CraftingTasks.TaskResult.Retry;
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
