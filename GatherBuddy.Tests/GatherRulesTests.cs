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
    public void An_item_waits_when_only_a_window_gives_it()
    {
        Assert.True(GatherRules.Waits(nodeLocation: 4, fishLocation: null, diademRaw: false));
        Assert.True(GatherRules.Waits(nodeLocation: null, fishLocation: 2, diademRaw: false));
        Assert.False(GatherRules.Waits(nodeLocation: 0, fishLocation: null, diademRaw: false));
        Assert.False(GatherRules.Waits(nodeLocation: null, fishLocation: null, diademRaw: false));
        Assert.False(GatherRules.Waits(nodeLocation: null, fishLocation: null, diademRaw: true));
    }

    [Fact]
    public void A_list_buys_what_waits_and_everything_once_it_buys_instead_of_gathering()
    {
        Assert.True(GatherRules.Buys(insteadOfWaiting: true, insteadOfGathering: false, waits: true));
        Assert.False(GatherRules.Buys(insteadOfWaiting: true, insteadOfGathering: false, waits: false));
        Assert.True(GatherRules.Buys(insteadOfWaiting: true, insteadOfGathering: true, waits: false));
        Assert.False(GatherRules.Buys(insteadOfWaiting: false, insteadOfGathering: true, waits: true));
        Assert.False(GatherRules.Buys(insteadOfWaiting: false, insteadOfGathering: false, waits: true));
    }

    [Fact]
    public void A_share_is_bought_only_from_a_gil_vendor()
    {
        Assert.True(GatherRules.BuyInstead(soldForGil: true, toBuy: 10));
        Assert.False(GatherRules.BuyInstead(soldForGil: false, toBuy: 10));
        Assert.False(GatherRules.BuyInstead(soldForGil: true, toBuy: 0));
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
