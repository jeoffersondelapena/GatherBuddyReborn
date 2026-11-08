using System.Numerics;
using GatherBuddy.ForkLogic;
using Xunit;
using Category = GatherBuddy.ForkLogic.RepairRules.Category;
using Mender = GatherBuddy.ForkLogic.RepairRules.Mender;
using Need = GatherBuddy.ForkLogic.RepairRules.Need;
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
    public void The_nearest_in_this_zone_wins_then_the_preferred_mender_then_the_cheapest_trip()
    {
        var uldah   = new Mender(1, 130, Vector3.Zero);
        var limsa   = new Mender(2, 129, Vector3.Zero);
        var farHere = new Mender(3, 641, new Vector3(50, 0, 0));
        var near    = new Mender(4, 641, new Vector3(5, 0, 0));
        var menders = new[] { uldah, limsa, farHere, near };
        TripRules.Trip? Trip(Mender m) => m.Territory switch { 130 => new(500, true), 129 => new(0, true), _ => null };

        Assert.Equal(near, RepairRules.Choose(menders, preferredId: 1, territory: 641, Vector3.Zero, Trip));
        Assert.Equal(uldah, RepairRules.Choose(menders, preferredId: 1, territory: 655, Vector3.Zero, Trip));
        Assert.Equal(near, RepairRules.Choose(menders, preferredId: 0, territory: 641, Vector3.Zero, Trip));
        Assert.Equal(limsa, RepairRules.Choose(menders, preferredId: 0, territory: 655, Vector3.Zero, Trip));
        Assert.Equal(limsa, RepairRules.Choose(menders, preferredId: 99, territory: 655, Vector3.Zero, Trip));
        Assert.Null(RepairRules.Choose([farHere, near], preferredId: 0, territory: 655, Vector3.Zero, Trip));
    }

    private static readonly Dictionary<string, int> Smiths = new() { ["Blacksmith"] = 50, ["Weaver"] = 50 };

    private static Need Piece(string name, int grade, string repairClass = "Blacksmith", int level = 40)
        => new(name, grade, $"Grade {grade} Dark Matter", repairClass, level);

    private static Dictionary<int, int> Stock(params (int Grade, int Count)[] units)
        => units.ToDictionary(u => u.Grade, u => u.Count);

    [Fact]
    public void Gear_is_repaired_by_hand_when_the_dark_matter_and_the_class_level_are_there()
    {
        var needs = new[] { Piece("the main hand", 8), Piece("the body", 6, "Weaver", 30) };
        Assert.Null(RepairRules.SelfRepairBlocker(needs, Stock((8, 2)), Smiths, 10));
        Assert.Null(RepairRules.SelfRepairBlocker(needs, Stock((8, 1), (6, 1)), Smiths, 10));
        Assert.Null(RepairRules.SelfRepairBlocker([], Stock(), Smiths, 10));
    }

    [Fact]
    public void Missing_dark_matter_names_the_grade_that_would_repair_every_piece()
    {
        var needs = new[] { Piece("the main hand", 8), Piece("the body", 6), Piece("the legs", 6) };
        Assert.Equal("Grade 8 Dark Matter is missing, 3 pieces below 10%", RepairRules.SelfRepairBlocker(needs, Stock(), Smiths, 10));
        Assert.Equal("Grade 8 Dark Matter is missing, 3 pieces below 10%", RepairRules.SelfRepairBlocker(needs, Stock((6, 5)), Smiths, 10));
        Assert.Equal("Grade 6 Dark Matter is short, 1 on hand for 3 pieces below 10%", RepairRules.SelfRepairBlocker(needs, Stock((8, 1)), Smiths, 10));
        Assert.Equal("Grade 8 Dark Matter is short, 2 on hand for 3 pieces below 50%",
            RepairRules.SelfRepairBlocker([Piece("a", 8), Piece("b", 8), Piece("c", 8)], Stock((8, 2)), Smiths, 50));
        Assert.Equal("Grade 8 Dark Matter is missing, 1 piece below 10%", RepairRules.SelfRepairBlocker([Piece("the main hand", 8)], Stock((7, 9)), Smiths, 10));
    }

    [Fact]
    public void A_low_repair_class_names_the_piece_that_needs_the_most()
    {
        var needs = new[] { Piece("the main hand", 8, "Blacksmith", 40), Piece("the off hand", 8, "Blacksmith", 20) };
        Assert.Equal("Blacksmith is level 12, needs 40 for the main hand",
            RepairRules.SelfRepairBlocker(needs, Stock((8, 2)), new Dictionary<string, int> { ["Blacksmith"] = 12 }, 10));
        Assert.Equal("Blacksmith is not unlocked, needs level 40 for the main hand",
            RepairRules.SelfRepairBlocker(needs, Stock((8, 2)), new Dictionary<string, int>(), 10));
        Assert.Null(RepairRules.SelfRepairBlocker(needs, Stock((8, 2)), new Dictionary<string, int> { ["Blacksmith"] = 40 }, 10));
    }

    [Fact]
    public void Every_shortfall_is_named_dark_matter_first_then_each_class()
    {
        var needs = new[]
        {
            Piece("the main hand", 8, "Blacksmith", 40),
            Piece("Cotton Doublet in the armoury chest", 5, "Weaver", 30),
            Piece("the feet", 5, "Leatherworker", 25),
        };
        var levels = new Dictionary<string, int> { ["Blacksmith"] = 12, ["Weaver"] = 50, ["Leatherworker"] = 1 };
        Assert.Equal("Grade 8 Dark Matter is missing, 3 pieces below 50%; Blacksmith is level 12, needs 40 for the main hand; "
          + "Leatherworker is level 1, needs 25 for the feet", RepairRules.SelfRepairBlocker(needs, Stock((5, 3)), levels, 50));
    }

    [Fact]
    public void Slots_counts_and_the_pause_texts_read_as_the_game_and_the_chat_do()
    {
        Assert.Equal("main hand", RepairRules.SlotName(0));
        Assert.Equal("left ring", RepairRules.SlotName(12));
        Assert.Equal("slot 14", RepairRules.SlotName(14));
        Assert.Equal("1 piece below 10%", RepairRules.Pieces(1, 10));
        Assert.Equal("gear is still below 10% after a dark matter repair", RepairRules.StillWorn(10));

        const string why = "Grade 8 Dark Matter is missing, 1 piece below 10%";
        Assert.Equal("paused: " + why, RepairRules.PausedNote(why));
        Assert.Equal("GatherBuddy: gathering run paused, " + why, RepairRules.PausedToast(TextRules.Gathering, why));
        Assert.Equal("GatherBuddy: crafting run paused, " + why, RepairRules.PausedToast(TextRules.Crafting, why));
        Assert.Equal("not repaired after the run: " + why, RepairRules.AfterRunNote(why));
        Assert.Equal("GatherBuddy: gear not repaired after the run, " + why, RepairRules.AfterRunToast(why));
        Assert.Equal("[GatherBuddy] After-run repair skipped: " + why + " (fork).", RepairRules.AfterRunLine(why));
        Assert.Equal("[GatherBuddy] This crafting run stopped: " + why + ". Fix the cause and press Resume in the Craft Status window to craft "
          + "the rest, or press Stop there to end the run (fork).", TextRules.RunStopped(TextRules.Crafting, why));
    }
}
