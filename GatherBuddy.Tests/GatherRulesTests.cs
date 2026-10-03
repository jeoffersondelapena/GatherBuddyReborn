using GatherBuddy.ForkLogic;
using Xunit;

public class GatherRulesTests
{
    [Fact]
    public void Counting_what_you_hold_stops_at_the_amount_and_on_top_gathers_the_amount_again()
    {
        Assert.False(GatherRules.StillNeeded(held: 5, heldAtStart: null, quantity: 3));
        Assert.True(GatherRules.StillNeeded(held: 2, heldAtStart: null, quantity: 3));
        Assert.True(GatherRules.StillNeeded(held: 5, heldAtStart: 5, quantity: 1));
        Assert.False(GatherRules.StillNeeded(held: 6, heldAtStart: 5, quantity: 1));
        Assert.Equal(3, GatherRules.Missing(held: 7, heldAtStart: 5, quantity: 5));
        Assert.Equal(0, GatherRules.Missing(held: 9, heldAtStart: null, quantity: 5));
    }

    [Fact]
    public void An_item_is_bought_instead_only_when_gathering_it_means_waiting_and_a_vendor_sells_it()
    {
        Assert.True(GatherRules.BuyInstead(nodeLocation: 4, fishLocation: null, diademRaw: false, soldForGil: true, toBuy: 10));
        Assert.True(GatherRules.BuyInstead(nodeLocation: null, fishLocation: 2, diademRaw: false, soldForGil: true, toBuy: 1));
        Assert.False(GatherRules.BuyInstead(nodeLocation: 0, fishLocation: null, diademRaw: false, soldForGil: true, toBuy: 10));
        Assert.False(GatherRules.BuyInstead(nodeLocation: 4, fishLocation: null, diademRaw: false, soldForGil: false, toBuy: 10));
        Assert.False(GatherRules.BuyInstead(nodeLocation: 4, fishLocation: null, diademRaw: false, soldForGil: true, toBuy: 0));
    }

    [Fact]
    public void The_share_of_lists_that_do_not_buy_is_left_for_gathering()
    {
        Assert.Equal(10, GatherRules.ToBuy(missing: 11, keptForGathering: 1));
        Assert.Equal(3, GatherRules.ToBuy(missing: 4, keptForGathering: 1));
        Assert.Equal(4, GatherRules.ToBuy(missing: 4, keptForGathering: 0));
        Assert.Equal(0, GatherRules.ToBuy(missing: 1, keptForGathering: 1));
        Assert.Equal(0, GatherRules.ToBuy(missing: 0, keptForGathering: 5));
    }
}
