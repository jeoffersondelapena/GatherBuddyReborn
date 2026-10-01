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
    public void The_preferred_mender_wins_then_one_in_this_zone_then_the_first_known()
    {
        var menders = new[] { new Mender(1, 129), new Mender(2, 641), new Mender(3, 628) };

        Assert.Equal(new Mender(3, 628), RepairRules.Choose(menders, preferredId: 3, territory: 641));
        Assert.Equal(new Mender(2, 641), RepairRules.Choose(menders, preferredId: 0, territory: 641));
        Assert.Equal(new Mender(2, 641), RepairRules.Choose(menders, preferredId: 99, territory: 641));
        Assert.Equal(new Mender(1, 129), RepairRules.Choose(menders, preferredId: 0, territory: 400));
        Assert.Null(RepairRules.Choose(System.Array.Empty<Mender>(), preferredId: 3, territory: 641));
    }
}
