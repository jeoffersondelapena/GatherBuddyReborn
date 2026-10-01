using System.Numerics;
using Dalamud.Bindings.ImGui;
using ElliLib;
using GatherBuddy.Crafting;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

// fork: settings and tools that aren't tied to one list or one run's window
internal static class ForkSettingsPage
{
    public static void DrawRepairAfterRun()
    {
        var config   = GatherBuddy.Config.VulcanRepairConfig;
        var afterRun = config.RepairAfterRun;
        if (ImGui.Checkbox("Repair All Gear After a Run (fork)", ref afterRun))
        {
            config.RepairAfterRun = afterRun;
            GatherBuddy.Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When a crafting run or a gathering run is over, visit a mender (the preferred one in Vulcan's settings, else one in "
              + "the current zone, else the first one known), repair every gear category including the armoury chest and bags, then go home.");
    }

    public static void DrawAfterRunThreshold()
    {
        var config    = GatherBuddy.Config.VulcanRepairConfig;
        var threshold = config.AfterRunThreshold;
        ImGui.SetNextItemWidth(VulcanUiScaling.Scaled(150f));
        if (ImGui.SliderInt("After-Run Threshold % (fork)", ref threshold, 1, 100))
        {
            config.AfterRunThreshold = threshold;
            GatherBuddy.Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Visit the mender when any piece of gear is below this condition; 100 means anything not fully repaired");
    }

    public static void DrawResetSettings()
    {
        if (ImGuiUtil.DrawDisabledButton("Reset settings to policy (fork)", Vector2.Zero,
                "Apply the agreed settings again (repair, materia, every helper, honk off, fishing data, gearset names). Nothing else changes.",
                !PolicySettings.SnapshotExists))
            PolicySettings.Apply();
    }

    public static void DrawResetLists()
    {
        var lists = CraftingGatherBridge.ListsManager;
        if (ImGuiUtil.DrawDisabledButton("Reset generated lists (fork)", Vector2.Zero,
                "Put every list the generator made (gathering, fishing, crafting, vendor) back to its generated state: off, every item on.\n"
              + "Hand-made lists stay as they are. The copy being replaced is kept once as generated-lists.before-reset.json.",
                !GeneratedLists.SnapshotExists || lists == null))
            GeneratedLists.Restore(lists!);
    }

    public static void DrawElsewhere()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Also added by the fork, kept where they act:");
        ImGui.BulletText("Skip Logged Items (fork): on each gathering list, in the Auto-Gather tab.");
        ImGui.BulletText("Skip Logged Recipes (fork): on each crafting list, in Vulcan's list editor.");
        ImGui.BulletText("Craft What I Have (fork), Clear Orange Marks (fork) and Clear Green Marks (fork): in the Craft Status window.");
        ImGui.BulletText("Clear Green Marks (fork): in the buy-list window.");
        ImGui.BulletText("Retry skipped (fork): in Vulcan's Crafting Lists tab.");
    }
}
