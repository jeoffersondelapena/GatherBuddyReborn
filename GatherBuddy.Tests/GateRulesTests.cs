using GatherBuddy.ForkLogic;
using Xunit;

public class GateRulesTests
{
    private static readonly Gate Level = new(GateKind.Level, "Alchemist level 19 (yours is 16)");
    private static readonly Gate Book  = new(GateKind.Book, "the book Master Alchemist I");

    [Fact]
    public void A_craft_the_character_can_start_is_crafted_unless_every_sold_one_is_bought()
    {
        Assert.Equal(Gated.Craft, GateRules.Decide(null, BuyCrafts.Craft, true));
        Assert.Equal(Gated.Craft, GateRules.Decide(null, BuyCrafts.WhenOutOfReach, true));
        Assert.Equal(Gated.Buy, GateRules.Decide(null, BuyCrafts.InsteadOfCrafting, true));
        Assert.Equal(Gated.Craft, GateRules.Decide(null, BuyCrafts.InsteadOfCrafting, false));
    }

    [Fact]
    public void A_craft_out_of_reach_is_bought_when_the_list_allows_and_a_vendor_sells_it()
    {
        Assert.Equal(Gated.Buy, GateRules.Decide(Level, BuyCrafts.WhenOutOfReach, true));
        Assert.Equal(Gated.Buy, GateRules.Decide(Book, BuyCrafts.InsteadOfCrafting, true));
        Assert.Equal(Gated.Defer, GateRules.Decide(Level, BuyCrafts.WhenOutOfReach, false));
        Assert.Equal(Gated.Block, GateRules.Decide(Book, BuyCrafts.WhenOutOfReach, false));
    }

    [Fact]
    public void Only_a_level_can_change_during_a_run_so_only_that_waits_for_the_end()
    {
        Assert.Equal(Gated.Defer, GateRules.Decide(Level, BuyCrafts.Craft, true));
        Assert.Equal(Gated.Block, GateRules.Decide(Book, BuyCrafts.Craft, true));
        Assert.Equal(Gated.Block, GateRules.Decide(new Gate(GateKind.Gearset, "a gearset for Alchemist, which has none"), BuyCrafts.Craft, true));
    }

    [Fact]
    public void Reasons_name_the_need_and_what_the_character_has()
    {
        Assert.Equal("craftsmanship 100 (yours is 85)", GateRules.Need("craftsmanship", 100, 85));
        Assert.True(GateRules.Short(100, 85));
        Assert.False(GateRules.Short(0, 0));
        Assert.False(GateRules.Short(100, 100));
        Assert.Equal("Jellyfish Humours x2 (Alchemist level 19 (yours is 16))", GateRules.Bought("Jellyfish Humours", 2, Level.Need));
        Assert.Equal("Bronze Ingot x4 (a vendor sells it)", GateRules.Bought("Bronze Ingot", 4, null));
        Assert.Equal("needs Jellyfish Humours: the book Master Alchemist I", GateRules.Blocked("Jellyfish Humours", Book.Need));
    }

    [Fact]
    public void A_craft_met_twice_is_reported_once()
        => Assert.Equal(["a", "b"], GateRules.Distinct(["a", "b", "a"]));
}
