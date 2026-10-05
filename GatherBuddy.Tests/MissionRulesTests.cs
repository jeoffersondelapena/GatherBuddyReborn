using System;
using System.Collections.Generic;
using GatherBuddy.ForkLogic;
using Xunit;
using Mission = GatherBuddy.ForkLogic.MissionRules.Mission;

public class MissionRulesTests
{
    private static readonly Dictionary<string, Mission> Known = new()
    {
        ["Iron Spear"]      = new(1, 1),
        ["Roasted Nopales"] = new(2, 3),
        ["Silver Sand"]     = new(3, 10),
    };

    [Fact]
    public void Only_text_that_names_a_mission_item_becomes_a_mission_and_each_item_once()
    {
        var texts = new[] { "Supply Missions", "Requested", " Iron Spear ", "1", "Roasted Nopales", "Iron Spear", "Close" };
        Assert.Equal([new Mission(1, 1), new Mission(2, 3)], MissionRules.FromNames(texts, Known));
    }

    [Fact]
    public void An_exact_requested_amount_replaces_the_tables_when_one_is_known()
    {
        var exact = new[] { new Mission(3, 99), new Mission(1, 0) };
        Assert.Equal([new Mission(1, 1), new Mission(3, 99)], MissionRules.FromNames(["Iron Spear", "Silver Sand"], Known, exact));
    }

    [Fact]
    public void An_item_waits_only_when_a_window_is_the_one_way_left_to_get_it()
    {
        Assert.True(MissionRules.Waits(nodeLocation: 3, fishLocation: null, diademRaw: false, soldForGil: false, held: 0, needed: 10));
        Assert.True(MissionRules.Waits(nodeLocation: null, fishLocation: 7, diademRaw: false, soldForGil: false, held: 0, needed: 1));
        Assert.False(MissionRules.Waits(nodeLocation: 0, fishLocation: null, diademRaw: false, soldForGil: false, held: 0, needed: 10));
        Assert.False(MissionRules.Waits(nodeLocation: 3, fishLocation: null, diademRaw: false, soldForGil: true, held: 0, needed: 10));
        Assert.False(MissionRules.Waits(nodeLocation: 3, fishLocation: null, diademRaw: false, soldForGil: false, held: 10, needed: 10));
        Assert.False(MissionRules.Waits(nodeLocation: null, fishLocation: null, diademRaw: false, soldForGil: false, held: 0, needed: 1));
    }

    [Fact]
    public void A_mission_day_runs_from_one_2000_UTC_reset_to_the_next()
    {
        Assert.Equal("2026-10-02", MissionRules.Day(new DateTime(2026, 10, 3, 19, 59, 0)));
        Assert.Equal("2026-10-03", MissionRules.Day(new DateTime(2026, 10, 3, 20, 0, 0)));
        Assert.Equal("2026-10-03", MissionRules.Day(new DateTime(2026, 10, 4, 7, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 3, 20, 0, 0), MissionRules.NextReset(new DateTime(2026, 10, 3, 8, 23, 0)));
        Assert.Equal(new DateTime(2026, 10, 4, 20, 0, 0), MissionRules.NextReset(new DateTime(2026, 10, 3, 20, 0, 0)));
    }

    [Fact]
    public void A_days_read_comes_back_whole_on_the_same_mission_day()
    {
        var text = MissionRules.Serialize("2026-10-03", [new Mission(1, 1), new Mission(3, 10)], [59, 59, 353]);
        var (missions, tried) = MissionRules.SavedFor(text, "2026-10-03");
        Assert.Equal([new Mission(1, 1), new Mission(3, 10)], missions);
        Assert.Equal([59u, 353u], tried);
        Assert.Empty(MissionRules.SavedFor(MissionRules.Serialize("2026-10-03", [new Mission(1, 1)]), "2026-10-03").Tried);
    }

    [Fact]
    public void The_lists_are_brought_in_line_once_per_character_and_mission_day_and_never_during_a_run()
    {
        var aligned = MissionRules.Aligned("a", "2026-10-03");
        Assert.True(MissionRules.Realign("a", "2026-10-03", null, busy: false, settled: true));
        Assert.False(MissionRules.Realign("a", "2026-10-03", aligned, busy: false, settled: true));
        Assert.True(MissionRules.Realign("b", "2026-10-03", aligned, busy: false, settled: true));
        Assert.True(MissionRules.Realign("a", "2026-10-04", aligned, busy: false, settled: true));
        Assert.False(MissionRules.Realign("b", "2026-10-03", aligned, busy: true, settled: true));
        Assert.False(MissionRules.Realign("b", "2026-10-03", aligned, busy: false, settled: false));
        Assert.False(MissionRules.Realign(null, "2026-10-03", aligned, busy: false, settled: true));
    }

    [Fact]
    public void A_read_from_another_day_or_an_unreadable_one_holds_nothing()
    {
        var text = MissionRules.Serialize("2026-10-02", [new Mission(1, 1)]);
        Assert.Empty(MissionRules.SavedFor(text, "2026-10-03").Missions);
        Assert.Empty(MissionRules.SavedFor(null, "2026-10-03").Missions);
        Assert.Empty(MissionRules.SavedFor("", "2026-10-03").Missions);
        Assert.Empty(MissionRules.SavedFor("{ not json", "2026-10-03").Missions);
        Assert.Empty(MissionRules.SavedFor("{\"On\":\"2026-10-03\"}", "2026-10-03").Missions);
    }

    [Fact]
    public void A_request_is_rounded_up_to_whole_crafts()
    {
        Assert.Equal(1, MissionRules.Crafts(1, 1));
        Assert.Equal(1, MissionRules.Crafts(3, 3));
        Assert.Equal(2, MissionRules.Crafts(4, 3));
        Assert.Equal(1, MissionRules.Crafts(0, 0));
    }

    [Fact]
    public void A_lists_description_carries_the_tag_and_the_day()
        => Assert.Equal("[gc-missions] 2026-10-03, 8 item(s)", MissionRules.Description(new DateTime(2026, 10, 3, 15, 0, 0), 8));
}
