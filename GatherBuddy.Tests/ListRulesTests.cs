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

    [Fact]
    public void The_master_switch_maps_onto_its_two_flags_both_ways()
    {
        foreach (var master in new[] { VendorRuns.Off, VendorRuns.AsListsSay, VendorRuns.EverythingSold })
        {
            var (on, everything) = ListRules.MasterFlags(master);
            Assert.Equal(master, ListRules.Master(on, everything));
        }
    }

    [Fact]
    public void Off_buys_nothing_and_the_override_buys_whatever_is_sold_without_touching_the_stored_choice()
    {
        Assert.Equal(Materials.GatherAll, ListRules.EffectiveMaterials(VendorRuns.Off, Materials.BuyInsteadOfGathering));
        Assert.Equal(Materials.BuyInsteadOfGathering, ListRules.EffectiveMaterials(VendorRuns.EverythingSold, Materials.GatherAll));
        Assert.Equal(Materials.BuyInsteadOfWaiting, ListRules.EffectiveMaterials(VendorRuns.AsListsSay, Materials.BuyInsteadOfWaiting));
        Assert.Equal(BuyCrafts.Craft, ListRules.EffectivePrecrafts(VendorRuns.Off, false, BuyCrafts.InsteadOfCrafting));
        Assert.Equal(BuyCrafts.InsteadOfCrafting, ListRules.EffectivePrecrafts(VendorRuns.EverythingSold, false, BuyCrafts.Craft));
        Assert.Equal(BuyCrafts.WhenOutOfReach, ListRules.EffectivePrecrafts(VendorRuns.EverythingSold, true, BuyCrafts.Craft));
    }

    [Fact]
    public void Final_crafts_are_never_bought_on_the_override_s_account()
    {
        Assert.Equal(BuyCrafts.Craft, ListRules.EffectiveFinals(VendorRuns.EverythingSold, false, BuyCrafts.Craft));
        Assert.Equal(BuyCrafts.InsteadOfCrafting, ListRules.EffectiveFinals(VendorRuns.EverythingSold, false, BuyCrafts.InsteadOfCrafting));
        Assert.Equal(BuyCrafts.Craft, ListRules.EffectiveFinals(VendorRuns.Off, false, BuyCrafts.InsteadOfCrafting));
    }
}
