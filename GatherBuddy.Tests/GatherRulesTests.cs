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
}
