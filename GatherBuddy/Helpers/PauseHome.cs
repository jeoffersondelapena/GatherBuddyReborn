using System;
using Dalamud.Game.ClientState.Conditions;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// fork: a run waiting on the player waits at home, not among other players
internal static class PauseHome
{
    private const uint HousingInterior = 14;

    private static string? _part;
    private static DateTime _since;

    public static void Request(string part)
    {
        if (!GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone)
            return;

        _part  = part;
        _since = DateTime.UtcNow;
    }

    public static void Update()
    {
        if (_part == null)
            return;

        if (Dalamud.GameData.GetExcelSheet<TerritoryType>().GetRowOrDefault(Dalamud.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId == HousingInterior)
        {
            ForkTrace.Info($"go home (paused while {_part}): already home");
            _part = null;
            return;
        }

        if (DateTime.UtcNow - _since > TimeSpan.FromMinutes(1))
        {
            ForkTrace.Info($"go home (paused while {_part}): {Busy() ?? "nothing"} kept the character here for a minute, staying put");
            _part = null;
            return;
        }

        if (Busy() != null)
            return;

        if (HomeNavigationHelper.TryStartReturnHome(out var error, $"paused while {_part}"))
            _part = null;
        else if (error != null)
        {
            ForkTrace.Info($"go home (paused while {_part}): {error}");
            _part = null;
        }
    }

    private static string? Busy()
    {
        var c = Dalamud.Conditions;
        return VendorInteractionHelper.GetVendorExitBlocker() != null ? "a vendor window"
            : c[ConditionFlag.Crafting] || c[ConditionFlag.ExecutingCraftingAction] || c[ConditionFlag.PreparingToCraft] ? "the crafting log"
            : c[ConditionFlag.Gathering] ? "a gathering node"
            : c[ConditionFlag.BetweenAreas] || c[ConditionFlag.BetweenAreas51] ? "a zone change"
            : c[ConditionFlag.OccupiedInQuestEvent] ? "a dialogue"
            : c[ConditionFlag.OccupiedSummoningBell] ? "a summoning bell"
            : c[ConditionFlag.Casting] ? "a cast"
            : Dalamud.Objects.LocalPlayer == null ? "loading"
            : null;
    }
}
