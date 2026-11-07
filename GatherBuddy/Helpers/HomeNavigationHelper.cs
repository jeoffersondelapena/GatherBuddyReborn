using System;
using Dalamud.Game.ClientState.Conditions;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

public static class HomeNavigationHelper
{
    public static bool ShouldReturnHomeAfterCollectables()
        => GatherBuddy.Config.AutoGatherConfig.GoHomeWhenIdle;

    // fork: the game marks inn rooms and house interiors; a trip from inside one would only leave and come back
    public static bool AtHome()
        => TripRules.IsHome(Dalamud.GameData.GetExcelSheet<TerritoryType>().GetRowOrDefault(Dalamud.ClientState.TerritoryType)?.TerritoryIntendedUse.RowId ?? 0);

    public static bool TryStartReturnHome(out string? error, string reason = "after collectables")
    {
        error = null;
        if (AtHome())
        {
            ForkTrace.Info($"go home ({reason}): already home");
            return true;
        }

        if (Dalamud.Conditions[ConditionFlag.BoundByDuty])
        {
            error = "Cannot return home while bound by duty.";
            return false;
        }

        if (!Lifestream.Enabled)
        {
            error = "Lifestream is not available.";
            return false;
        }

        if (Lifestream.IsBusy())
            return false;

        var command = GatherBuddy.Config.AutoGatherConfig.LifestreamCommand;
        if (string.IsNullOrWhiteSpace(command))
            command = "auto";
        if (command.Contains("/li ", StringComparison.OrdinalIgnoreCase))
            command = command.Replace("/li ", string.Empty, StringComparison.OrdinalIgnoreCase);

        ForkTrace.Info($"go home ({reason}): Lifestream '{command}' from territory {Dalamud.ClientState.TerritoryType}");
        Lifestream.ExecuteCommand(command);
        return true;
    }

    public static bool IsReturnComplete()
        => !Lifestream.Enabled || !Lifestream.IsBusy();
}
