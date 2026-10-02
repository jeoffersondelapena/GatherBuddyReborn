using System.Collections.Generic;
using System.Linq;
using GatherBuddy.ForkLogic;
using Xunit;

public class PurchaseRulesTests
{
    private const uint Ore = 5, Hide = 5300, Rivets = 5070, Drop = 7000;

    private static readonly HashSet<uint> Gathered = [Ore];
    private static readonly HashSet<uint> SoldForGil = [Ore, Hide, Rivets];

    private static List<(uint ItemId, uint Target, int Missing)> Buy(Dictionary<uint, int> materials, Dictionary<uint, int>? held = null)
        => PurchaseRules.BeforeGathering(materials, Gathered.Contains, SoldForGil.Contains, id => held?.GetValueOrDefault(id) ?? 0);

    [Fact]
    public void What_the_run_can_gather_is_gathered_even_when_a_vendor_sells_it()
        => Assert.DoesNotContain(Buy(new() { [Ore] = 6, [Hide] = 2 }), b => b.ItemId == Ore);

    [Fact]
    public void Buy_instead_of_gathering_buys_what_a_vendor_sells_and_still_leaves_the_rest_to_gathering()
    {
        var materials = new Dictionary<uint, int> { [Ore] = 6, [Hide] = 2, [Drop] = 1 };
        var bought    = PurchaseRules.BeforeGathering(materials, PurchaseRules.Gathered(true, Gathered.Contains), SoldForGil.Contains, _ => 0);
        Assert.Equal([Ore, Hide], bought.Select(b => b.ItemId).Order());
        Assert.DoesNotContain(PurchaseRules.BeforeGathering(materials, PurchaseRules.Gathered(false, Gathered.Contains), SoldForGil.Contains, _ => 0),
            b => b.ItemId == Ore);
    }

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
}
