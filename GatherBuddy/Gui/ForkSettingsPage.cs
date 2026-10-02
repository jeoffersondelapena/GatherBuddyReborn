using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ElliLib;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

// fork: settings and tools that aren't tied to one list or one run's window
internal static class ForkSettingsPage
{
    public static void DrawBuyBeforeGathering()
    {
        var buy = GatherBuddy.Config.VulcanBuyBeforeGathering;
        if (ImGui.Checkbox("Buy From Vendors Before Gathering (fork)", ref buy))
        {
            GatherBuddy.Config.VulcanBuyBeforeGathering = buy;
            GatherBuddy.Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("A crafting run first buys, for gil, every material it can neither gather nor fish (vendor items, drops a vendor also "
              + "sells), only what your bags lack, then gathers and crafts. What it can gather it gathers, and in-between items it crafts.");
    }

    public static void DrawBellZone()
    {
        var chosen = CharacterSettings.BellZone;
        var zones  = BellLocations.Zones();
        string Label(uint zone)
            => zone switch
            {
                BellRules.Automatic => "Automatic: the town cheapest to teleport to",
                BellRules.Home      => "Home: a summoning bell you placed in your house",
                _                   => zones.FirstOrDefault(z => z.Territory == zone).Name ?? $"zone {zone}",
            };

        ImGui.SetNextItemWidth(VulcanUiScaling.Scaled(320f));
        if (ImGui.BeginCombo("Summoning Bell (per character, fork)", Label(chosen)))
        {
            foreach (var zone in new[] { BellRules.Automatic, BellRules.Home }.Concat(zones.Select(z => z.Territory)))
                if (ImGui.Selectable(Label(zone), zone == chosen))
                    CharacterSettings.BellZone = zone;
            if (zones.Count == 0)
                ImGui.TextDisabled("Reading the towns and housing districts...");
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Where a crafting run goes to take from your retainers when no summoning bell is in sight and the zone it is in has none. "
              + "Automatic picks the town that is cheapest to teleport to. Home takes the trip home, for a bell you placed in your house. Saved for the "
              + "character that is logged in.");
    }

    public static void DrawBellTest()
    {
        if (BellTravelTest.Running)
        {
            if (ImGui.Button("Stop Bell Travel Test (fork)"))
                BellTravelTest.Stop();
            return;
        }

        if (ImGuiUtil.DrawDisabledButton("Test Bell Travel (fork)", Vector2.Zero,
                "Temporary: from where you stand, go to a summoning bell the way a crafting run does when your retainers hold something it needs, "
              + "and stop at the bell. Chat says what it did.", CraftingGatherBridge.IsQueueMode))
            BellTravelTest.Start();
    }

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
        ImGui.BulletText("Skip Retainers (fork), Skip Buying (fork), Skip Gathering (fork) and Clear Green Marks (fork): in the Craft Status window.");
        ImGui.BulletText("Clear Green Marks (fork): in the buy-list window.");
        ImGui.BulletText("Clear Green Marks (fork): in the right-click menu of an item marked green.");
        ImGui.BulletText("Retry skipped (fork): in Vulcan's Crafting Lists tab.");
    }
}
