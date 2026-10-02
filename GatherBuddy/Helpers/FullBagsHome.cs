using System;
using Dalamud.Game.ClientState.Conditions;
using GatherBuddy.Crafting;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// fork: a crafting run paused on full bags goes home to make room, as a finished run does, unless a summoning bell is in sight
internal static class FullBagsHome
{
    private const uint HousingInterior = 14;

    private static string? _part;
    private static DateTime _since;

    public static void Request(string part)
    {
        if (!GatherBuddy.Config.AutoGatherConfig.GoHomeWhenDone)
            return;

        if (RetainerTaskExecutor.FindNearestBellForNavigation() != null)
        {
            ForkTrace.Info($"go home (full bags, {part}): a summoning bell is in sight, staying put");
            Communicator.PrintRun("[GatherBuddy] Staying here: a summoning bell is in sight, so your retainers can take what you want to keep (fork).",
                tone: Communicator.Tone.Info);
            return;
        }

        if (Dalamud.GameData.GetExcelSheet<TerritoryType>().GetRowOrDefault(Dalamud.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId == HousingInterior)
        {
            ForkTrace.Info($"go home (full bags, {part}): already inside a home, staying put");
            return;
        }

        _part  = part;
        _since = DateTime.UtcNow;
    }

    public static void Update()
    {
        if (_part == null)
            return;

        if (DateTime.UtcNow - _since > TimeSpan.FromMinutes(1))
        {
            ForkTrace.Info($"go home (full bags, {_part}): {Busy() ?? "nothing"} kept the character here for a minute, staying put");
            _part = null;
            return;
        }

        if (Busy() != null)
            return;

        if (HomeNavigationHelper.TryStartReturnHome(out var error, $"full bags, {_part}"))
            _part = null;
        else if (error != null)
        {
            ForkTrace.Info($"go home (full bags, {_part}): {error}");
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
            : c[ConditionFlag.Casting] ? "a cast"
            : Dalamud.Objects.LocalPlayer == null ? "loading"
            : null;
    }
}
