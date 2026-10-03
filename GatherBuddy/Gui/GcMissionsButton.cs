using System.Numerics;
using Dalamud.Bindings.ImGui;
using ElliLib;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Automation;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

// fork: the missions are read from this game window, so the button that reads them also sits under it
internal static unsafe class GcMissionsButton
{
    public static void Draw()
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(GcMissions.Window, out var addon) || !addon->IsVisible
         || !GcMissions.WindowShowsMissions(addon))
            return;

        if (GcMissions.TakeFillRequest())
            GcMissions.MakeLists();
        var corner = ImGui.GetMainViewport().Pos + new Vector2(addon->X, addon->Y + addon->GetScaledHeight(true));
        ImGui.SetNextWindowPos(corner + new Vector2(16f, -4f) * ImGui.GetIO().FontGlobalScale);
        if (ImGui.Begin("##gcMissionsButton", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove
              | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)
         && ImGuiUtil.DrawDisabledButton("GC Mission Lists (fork)", Vector2.Zero,
                $"Refill GatherBuddy's lists '{MissionRules.SupplyList}' and '{MissionRules.ProvisioningList}' with these missions. Not during a run.",
                GcMissions.Busy))
            GcMissions.MakeLists();
        ImGui.End();
    }
}
