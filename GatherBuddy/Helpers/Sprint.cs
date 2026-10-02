using System;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: only walks GatherBuddy itself drives; Lifestream sprints on its own walks
internal static unsafe class Sprint
{
    private static DateTime _checked = DateTime.MinValue;

    public static void Update()
    {
        if (!GatherBuddy.Config.SprintWhenWalking || DateTime.UtcNow - _checked < TimeSpan.FromMilliseconds(500))
            return;

        _checked = DateTime.UtcNow;
        if (!Driving() || Dalamud.Objects.LocalPlayer is not { } player || !VNavmesh.Path.IsRunning())
            return;

        var actions = ActionManager.Instance();
        var ready   = actions != null && actions->GetActionStatus(ActionType.GeneralAction, SprintRules.GeneralActionId) == 0;
        var left    = SprintRules.Left(player.Position, VNavmesh.Path.ListWaypoints());
        if (!SprintRules.Now(true, Dalamud.Conditions[ConditionFlag.Mounted], ready, left))
            return;

        if (actions->UseAction(ActionType.GeneralAction, SprintRules.GeneralActionId))
            GatherBuddy.Log.Debug($"[fork] sprint: {left:F0} yalms of walk left");
    }

    private static bool Driving()
        => GatherBuddy.AutoGather.Enabled
         || CraftingGatherBridge.IsQueueMode
         || GatherBuddy.VendorBuyListManager.IsBusy
         || GatherBuddy.VendorNavigator.IsActive
         || AfterRunRepair.Busy;
}
