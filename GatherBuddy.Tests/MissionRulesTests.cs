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
