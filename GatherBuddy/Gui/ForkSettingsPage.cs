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
              + "sells), only what your bags lack, then gathers and crafts. What it can gather it gathers (unless the list has Buy Instead of "
              + "Gathering (fork) on), and in-between items it crafts.");
    }

    public static void DrawSprint()
    {
        var sprint = GatherBuddy.Config.SprintWhenWalking;
        if (ImGui.Checkbox("Sprint When Walking (fork)", ref sprint))
        {
            GatherBuddy.Config.SprintWhenWalking = sprint;
            GatherBuddy.Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Use Sprint whenever the game allows it on a walk GatherBuddy drives: to a vendor, a summoning bell or a mender, "
              + "or between gathering spots on foot. Skipped on walks under 30 yalms and while mounted. Your own movement is never touched.");
    }

    public static void DrawBellZone()
    {
        var chosen = CharacterSettings.BellZone;
        var zones  = BellLocations.Zones();
        string Label(uint zone)
            => zone switch
            {
                BellRules.Automatic => "Automatic: the town cheapest to teleport to",
                _                   => zones.FirstOrDefault(z => z.Territory == zone).Name ?? $"zone {zone}",
            };

        ImGui.SetNextItemWidth(VulcanUiScaling.Scaled(320f));
        if (ImGui.BeginCombo("Summoning Bell (per character, fork)", Label(chosen)))
        {
            foreach (var zone in zones.Select(z => z.Territory).Prepend(BellRules.Automatic))
                if (ImGui.Selectable(Label(zone), zone == chosen))
                    CharacterSettings.BellZone = zone;
            if (zones.Count == 0)
                ImGui.TextDisabled("Reading the towns and housing districts...");
            ImGui.EndCombo();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Where a crafting run goes to take from your retainers when no summoning bell is in sight and the zone it is in has none. "
              + "Automatic picks the town that is cheapest to teleport to. Saved for the character that is logged in.");
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
            ImGui.SetTooltip("When a crafting run or a gathering run is over, visit a mender, picked as a summoning bell is (one in sight, such as one hired "
              + "into your home, else the nearest in the current zone, else the preferred one in Vulcan's settings, else the one cheapest to teleport "
              + "to), repair every gear category including the armoury chest and bags, then go home unless already there.");
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
                "Put every list the generator made (gathering, fishing, crafting) back exactly as it wrote them last: every generated gathering and "
              + "fishing list off with all its items ticked for every character, and the folders it no longer uses removed. Hand-made lists stay "
              + "as they are.\n"
              + "Works while the game runs; with two game windows open, press it in each. Not during a run. The copy being replaced is kept once as "
              + "generated-lists.before-reset.json.",
                !GeneratedLists.SnapshotExists || lists == null || CraftingGatherBridge.IsQueueMode || GatherBuddy.AutoGather.Enabled))
            GeneratedLists.Restore(lists!);
    }

    public static void DrawGcMissionLists()
    {
        if (ImGuiUtil.DrawDisabledButton("GC Mission Lists (fork)", Vector2.Zero,
                $"Refill the crafting list '{MissionRules.SupplyList}' and the gathering list '{MissionRules.ProvisioningList}' with today's Grand "
              + "Company supply and provisioning missions.\n"
              + "The game holds the missions only while their Timers page is open, so a press opens that page, reads it, and leaves it "
              + "open to compare with the lists.\n"
              + "Missions differ by character, so the two lists follow the character that is logged in: at login they are rebuilt from that "
              + "character's own read of the day, or emptied when it has none.\n"
              + "Each press replaces what the two lists held, so earlier days never pile up. A mission item already held counts as done, logged "
              + "recipes and items are never skipped, and what would mean waiting for a time or weather window is left out. Not during a run.",
                GcMissions.Busy))
            GcMissions.MakeLists();
    }

    public static void DrawElsewhere()
    {
        ImGui.Spacing();
        ImGui.TextDisabled("Also added by the fork, kept where they act:");
        ImGui.BulletText("Skip Logged Items (fork), Get Only Missing Items (fork), Buy Instead of Waiting (fork) and, under it, Buy "
          + "Instead of Gathering Too (fork): on each gathering list, in the Auto-Gather tab.");
        ImGui.BulletText("Skip Logged Recipes (fork), Get Only Missing Materials (fork), But Treat NQ as Missing (fork), Buy Instead of "
          + "Waiting (fork) and, under it, Buy Instead of Gathering Too (fork): on each crafting list, in Vulcan's list editor.");
        ImGui.BulletText("Craft Only Missing Final Crafts works without Craft Only Missing Precrafts; GatherBuddy needs both.");
        ImGui.BulletText("Restock from Retainers (fork): on each buy list, in the Vendor Buy List window.");
        ImGui.BulletText("Skip Retainers (fork), Skip Buying (fork), Skip Gathering (fork) and Clear Green Marks (fork): in the Craft Status window.");
        ImGui.BulletText("Skip Retainers (fork): also in the Auto-Gather tab and the Vendor Buy List window, while that run's retainer part waits.");
        ImGui.BulletText("Clear Green Marks (fork): in the Vendor Buy List window.");
        ImGui.BulletText("Clear Green Marks (fork): in the right-click menu of an item marked green.");
        ImGui.BulletText("Retry skipped (fork): in Vulcan's Crafting Lists tab.");
        ImGui.BulletText("GC Mission Lists (fork): also under the game's Supply & Provisioning Missions window (Timers), while it is open.");
    }
}
