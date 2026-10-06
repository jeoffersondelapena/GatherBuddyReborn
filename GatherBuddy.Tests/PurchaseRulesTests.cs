using System.Collections.Generic;
using System.Linq;
using GatherBuddy.ForkLogic;
using Xunit;

public class PurchaseRulesTests
{
    private const uint Ore = 5, Hide = 5300, Rivets = 5070, Drop = 7000, Sand = 5500;

    private static readonly HashSet<uint> Gathered = [Ore];
    private static readonly HashSet<uint> Gatherable = [Ore, Sand];
    private static readonly HashSet<uint> SoldForGil = [Ore, Hide, Rivets, Sand];

    private static List<(uint ItemId, uint Target, int Missing)> Buy(Dictionary<uint, int> materials, Dictionary<uint, int>? held = null)
        => PurchaseRules.BeforeGathering(materials, Gathered.Contains, SoldForGil.Contains, id => held?.GetValueOrDefault(id) ?? 0);

    [Fact]
    public void What_the_run_can_gather_is_gathered_even_when_a_vendor_sells_it()
        => Assert.DoesNotContain(Buy(new() { [Ore] = 6, [Hide] = 2 }), b => b.ItemId == Ore);

    [Fact]
    public void Buy_instead_of_gathering_buys_what_a_vendor_sells_and_still_leaves_the_rest_to_gathering()
    {
        var materials = new Dictionary<uint, int> { [Ore] = 6, [Hide] = 2, [Drop] = 1 };
        var bought    = PurchaseRules.BeforeGathering(materials, PurchaseRules.Gathered(true, true, Gathered.Contains, Gatherable.Contains),
            SoldForGil.Contains, _ => 0);
        Assert.Equal([Ore, Hide], bought.Select(b => b.ItemId).Order());
        Assert.DoesNotContain(PurchaseRules.BeforeGathering(materials, PurchaseRules.Gathered(true, false, Gathered.Contains, Gatherable.Contains),
            SoldForGil.Contains, _ => 0), b => b.ItemId == Ore);
    }

    [Fact]
    public void Buy_instead_of_waiting_decides_whether_a_windowed_material_is_bought_and_what_only_vendors_sell_is_bought_either_way()
    {
        var materials = new Dictionary<uint, int> { [Ore] = 6, [Hide] = 2, [Sand] = 4 };
        List<uint> Bought(bool waiting, bool gathering)
            => PurchaseRules.BeforeGathering(materials, PurchaseRules.Gathered(waiting, gathering, Gathered.Contains, Gatherable.Contains),
                SoldForGil.Contains, _ => 0).Select(b => b.ItemId).Order().ToList();

        Assert.Equal([Hide, Sand], Bought(waiting: true, gathering: false));
        Assert.Equal([Hide], Bought(waiting: false, gathering: false));
        Assert.Equal([Hide], Bought(waiting: false, gathering: true));
        Assert.Equal([Ore, Hide, Sand], Bought(waiting: true, gathering: true));
    }

    [Fact]
    public void What_is_neither_gathered_nor_sold_for_gil_and_still_missing_is_beyond_a_runs_reach()
    {
        var materials = new Dictionary<uint, int> { [Ore] = 6, [Hide] = 2, [Drop] = 3, [Sand] = 4 };
        var held      = new Dictionary<uint, int> { [Drop] = 1 };
        Assert.Equal([(Drop, 2, PurchaseRules.NeitherGatheredNorSold)],
            PurchaseRules.BeyondReach(materials, Gatherable.Contains, SoldForGil.Contains, id => held.GetValueOrDefault(id), buying: true));
        held[Drop] = 3;
        Assert.Empty(PurchaseRules.BeyondReach(materials, Gatherable.Contains, SoldForGil.Contains, id => held.GetValueOrDefault(id), buying: true));
    }

    [Fact]
    public void With_buying_switched_off_what_only_a_vendor_sells_is_beyond_reach_too_and_says_why()
    {
        var materials = new Dictionary<uint, int> { [Ore] = 6, [Hide] = 2, [Drop] = 3 };
        Assert.Equal([(Hide, 2, PurchaseRules.NotBuying), (Drop, 3, PurchaseRules.NeitherGatheredNorSold)],
            PurchaseRules.BeyondReach(materials, Gatherable.Contains, SoldForGil.Contains, _ => 0, buying: false));
    }

    [Fact]
    public void An_item_no_vendor_could_supply_names_who_sells_it()
    {
        Assert.Equal("no vendor is known for the shop that sells it", PurchaseRules.NoVendor([], PurchaseRules.NoVendorCause.ShopKind));
        Assert.Equal("sold by Estate Manservant, but through a kind of shop window GatherBuddy cannot work",
            PurchaseRules.NoVendor(["Estate Manservant"], PurchaseRules.NoVendorCause.ShopKind));
        Assert.Equal("sold by A and B, which GatherBuddy's own list of vendors leaves out",
            PurchaseRules.NoVendor(["A", "B"], PurchaseRules.NoVendorCause.LeftOut));
        Assert.Equal("sold by A, B and 2 more, which GatherBuddy's own list of vendors leaves out",
            PurchaseRules.NoVendor(["A", "B", "C", "D"], PurchaseRules.NoVendorCause.LeftOut));
    }

    [Fact]
    public void A_vendor_whose_place_is_unknown_is_named_and_the_reason_is_walking()
        => Assert.Equal("GatherBuddy does not know where Estate Manservant is, so it cannot walk there.", PurchaseRules.PlaceUnknown("Estate Manservant"));

    [Fact]
    public void A_shortfall_is_gathered_instead_only_when_the_list_asks_and_nothing_left_must_be_bought()
    {
        Assert.True(PurchaseRules.GatherInstead(true, 0));
        Assert.False(PurchaseRules.GatherInstead(true, 1));
        Assert.False(PurchaseRules.GatherInstead(false, 0));
    }

    [Fact]
    public void A_drop_that_a_gil_vendor_sells_is_bought_and_one_no_vendor_sells_is_left_alone()
        => Assert.Equal([Hide], Buy(new() { [Hide] = 2, [Drop] = 1 }).Select(b => b.ItemId));

    [Fact]
    public void The_target_is_the_whole_need_and_only_the_shortfall_counts_as_missing()
    {
        var buy = Buy(new() { [Hide] = 5, [Rivets] = 3 }, new() { [Hide] = 2, [Rivets] = 3 });
        Assert.Equal([(Hide, 5u, 3)], buy);
    }

    [Fact]
    public void Nothing_is_bought_when_the_bags_already_hold_enough()
        => Assert.Empty(Buy(new() { [Hide] = 2 }, new() { [Hide] = 4 }));

    [Fact]
    public void An_empty_need_buys_nothing()
        => Assert.Empty(Buy(new() { [Hide] = 0 }));

    [Fact]
    public void A_timed_node_or_a_windowed_fish_counts_as_not_gathered_so_a_vendor_that_sells_it_is_used()
    {
        Assert.True(PurchaseRules.GatheredWithoutWaiting(0, null, false));
        Assert.True(PurchaseRules.GatheredWithoutWaiting(-3, null, false));
        Assert.True(PurchaseRules.GatheredWithoutWaiting(null, -8, false));
        Assert.True(PurchaseRules.GatheredWithoutWaiting(null, null, true));
        Assert.False(PurchaseRules.GatheredWithoutWaiting(12, null, false));
        Assert.False(PurchaseRules.GatheredWithoutWaiting(null, 40, false));
        Assert.False(PurchaseRules.GatheredWithoutWaiting(null, null, false));
    }

    [Fact]
    public void A_bought_craft_s_target_keeps_the_copies_the_plan_already_spent_from_the_bags()
    {
        // one Bronze Ingot held, two recipes wanting one each: the first is skipped for the held one, the second is bought
        var target = PurchaseRules.BoughtTarget(1, 1);
        Assert.Equal(2, target);
        Assert.Equal([(Rivets, 2u, 1)], PurchaseRules.BeforeGathering(new Dictionary<uint, int> { [Rivets] = target }, Gathered.Contains, SoldForGil.Contains, _ => 1));
        Assert.Equal(3, PurchaseRules.BoughtTarget(3, 0));
    }
}
