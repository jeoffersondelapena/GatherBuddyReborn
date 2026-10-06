using GatherBuddy.ForkLogic;
using Xunit;

public class ListRulesTests
{
    [Fact]
    public void Aiming_for_hq_keeps_buying_a_precraft_out_of_reach_but_never_one_a_vendor_merely_sells()
    {
        Assert.Equal(BuyCrafts.WhenOutOfReach, ListRules.Precrafts(true, BuyCrafts.InsteadOfCrafting));
        Assert.Equal(BuyCrafts.WhenOutOfReach, ListRules.Precrafts(true, BuyCrafts.WhenOutOfReach));
        Assert.Equal(BuyCrafts.Craft, ListRules.Precrafts(true, BuyCrafts.Craft));
        Assert.Equal(BuyCrafts.InsteadOfCrafting, ListRules.Precrafts(false, BuyCrafts.InsteadOfCrafting));
    }

    [Fact]
    public void Aiming_for_hq_never_buys_a_final_craft()
    {
        Assert.Equal(BuyCrafts.Craft, ListRules.Finals(true, BuyCrafts.WhenOutOfReach));
        Assert.Equal(BuyCrafts.Craft, ListRules.Finals(true, BuyCrafts.InsteadOfCrafting));
        Assert.Equal(BuyCrafts.InsteadOfCrafting, ListRules.Finals(false, BuyCrafts.InsteadOfCrafting));
    }

    [Theory]
    [InlineData(Synthesis.Normal, false, false)]
    [InlineData(Synthesis.QuickSynthPrecrafts, true, true)]
    [InlineData(Synthesis.QuickSynthEverything, true, false)]
    public void Synthesis_maps_onto_the_two_stored_flags_both_ways(Synthesis choice, bool quickSynthAll, bool precraftsOnly)
    {
        Assert.Equal((quickSynthAll, precraftsOnly), ListRules.SynthesisFlags(choice));
        Assert.Equal(choice, ListRules.SynthesisOf(quickSynthAll, precraftsOnly));
    }

    [Fact]
    public void Aiming_for_hq_synthesises_normally_and_leaves_the_nq_only_box_nothing_to_apply_to()
    {
        Assert.Equal(Synthesis.Normal, ListRules.EffectiveSynthesis(true, Synthesis.QuickSynthEverything));
        Assert.Equal(Synthesis.QuickSynthEverything, ListRules.EffectiveSynthesis(false, Synthesis.QuickSynthEverything));
        Assert.False(ListRules.NqOnlyApplies(true, Synthesis.QuickSynthPrecrafts));
        Assert.True(ListRules.NqOnlyApplies(false, Synthesis.QuickSynthPrecrafts));
        Assert.False(ListRules.NqOnlyApplies(false, Synthesis.Normal));
    }

    [Theory]
    [InlineData(Materials.GatherAll, false, false)]
    [InlineData(Materials.BuyInsteadOfWaiting, true, false)]
    [InlineData(Materials.BuyInsteadOfGathering, true, true)]
    public void Materials_map_onto_the_two_stored_flags_both_ways(Materials choice, bool insteadOfWaiting, bool insteadOfGathering)
    {
        Assert.Equal((insteadOfWaiting, insteadOfGathering), ListRules.MaterialsFlags(choice));
        Assert.Equal(choice, ListRules.MaterialsOf(insteadOfWaiting, insteadOfGathering));
    }

    [Fact]
    public void A_stray_instead_of_gathering_flag_without_its_parent_still_reads_as_gather_all()
        => Assert.Equal(Materials.GatherAll, ListRules.MaterialsOf(false, true));

    [Fact]
    public void Only_a_list_aiming_for_hq_without_the_daily_rule_starts_its_tries_afresh_each_run()
    {
        Assert.True(ListRules.TriesStartAfresh(true, false));
        Assert.False(ListRules.TriesStartAfresh(true, true));
        Assert.False(ListRules.TriesStartAfresh(false, false));
    }
}
