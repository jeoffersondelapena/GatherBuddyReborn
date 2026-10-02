using System.Numerics;
using GatherBuddy.ForkLogic;
using Xunit;
using Category = GatherBuddy.ForkLogic.RepairRules.Category;
using Mender = GatherBuddy.ForkLogic.RepairRules.Mender;
using Piece = GatherBuddy.ForkLogic.RepairRules.Piece;

public class RepairRulesTests
{
    [Fact]
    public void Condition_is_read_as_the_percent_the_game_shows()
    {
        Assert.Equal(100, RepairRules.Percent(30000));
        Assert.Equal(99, RepairRules.Percent(29999));
        Assert.Equal(0, RepairRules.Percent(0));
    }

    [Fact]
    public void Every_category_has_a_name_for_the_chat_line()
    {
        foreach (var category in System.Enum.GetValues<Category>())
            Assert.False(string.IsNullOrWhiteSpace(RepairRules.Label(category)));
        Assert.Equal("head/body/hands", RepairRules.Label(Category.HeadBodyHands));
    }

    [Fact]
    public void Only_categories_holding_a_worn_repairable_piece_are_visited_in_window_order()
    {
        var pieces = new[]
        {
            new Piece(Category.WristsRings, 40, true),
            new Piece(Category.Equipped, 99, true),
            new Piece(Category.Equipped, 100, true),
            new Piece(Category.HeadBodyHands, 100, true),
            new Piece(Category.LegsFeet, 0, false),
            new Piece(Category.WristsRings, 10, true),
            new Piece(Category.Inventory, 97, true),
        };

        Assert.Equal(new[] { Category.Equipped, Category.WristsRings, Category.Inventory }, RepairRules.Worn(pieces, 100));
        Assert.Equal(new[] { Category.WristsRings }, RepairRules.Worn(pieces, 50));
        Assert.Empty(RepairRules.Worn(pieces, 5));
    }

    [Fact]
    public void A_run_ends_with_a_repair_only_when_it_is_really_over()
    {
        Assert.True(RepairRules.AfterRun(enabled: true, partOfCraftingRun: false, boundByDuty: false));
        Assert.False(RepairRules.AfterRun(enabled: true, partOfCraftingRun: true, boundByDuty: false));
        Assert.False(RepairRules.AfterRun(enabled: true, partOfCraftingRun: false, boundByDuty: true));
        Assert.False(RepairRules.AfterRun(enabled: false, partOfCraftingRun: false, boundByDuty: false));
    }

    [Fact]
    public void The_preferred_mender_wins_then_the_nearest_in_this_zone_then_the_cheapest_trip()
    {
        var uldah   = new Mender(1, 130, Vector3.Zero);
        var limsa   = new Mender(2, 129, Vector3.Zero);
        var farHere = new Mender(3, 641, new Vector3(50, 0, 0));
        var near    = new Mender(4, 641, new Vector3(5, 0, 0));
        var menders = new[] { uldah, limsa, farHere, near };
        TripRules.Trip? Trip(Mender m) => m.Territory switch { 130 => new(500, true), 129 => new(0, true), _ => null };

        Assert.Equal(uldah, RepairRules.Choose(menders, preferredId: 1, territory: 641, Vector3.Zero, Trip));
        Assert.Equal(near, RepairRules.Choose(menders, preferredId: 0, territory: 641, Vector3.Zero, Trip));
        Assert.Equal(limsa, RepairRules.Choose(menders, preferredId: 0, territory: 655, Vector3.Zero, Trip));
        Assert.Equal(limsa, RepairRules.Choose(menders, preferredId: 99, territory: 655, Vector3.Zero, Trip));
        Assert.Null(RepairRules.Choose([farHere, near], preferredId: 0, territory: 655, Vector3.Zero, Trip));
    }
}
