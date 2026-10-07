using GatherBuddy.ForkLogic;
using Xunit;
using Trip = GatherBuddy.ForkLogic.TripRules.Trip;

public class TripRulesTests
{
    [Fact]
    public void Fewer_gil_wins_and_at_the_same_gil_no_teleport_wins()
    {
        Assert.True(TripRules.Cheaper(new Trip(100, true), new Trip(300, true)));
        Assert.True(TripRules.Cheaper(TripRules.Aethernet, new Trip(0, true)));
        Assert.False(TripRules.Cheaper(new Trip(0, true), TripRules.Aethernet));
        Assert.False(TripRules.Cheaper(new Trip(0, true), new Trip(0, true)));
    }

    [Fact]
    public void With_no_shard_in_view_the_character_walks_rather_than_waiting_for_one()
    {
        Assert.True(TripRules.WalkInstead(400, null, 5, 15));
        Assert.False(TripRules.WalkInstead(400, 20, 5, 15));
        Assert.True(TripRules.WalkInstead(30, 20, 5, 15));
    }

    [Fact]
    public void On_foot_near_a_vendor_the_run_walks_instead_of_mounting_for_a_landing_point()
    {
        Assert.True(TripRules.WalksToVendor(true, 24.7f));
        Assert.False(TripRules.WalksToVendor(true, 120f));
        Assert.False(TripRules.WalksToVendor(false, 24.7f));
    }
}
