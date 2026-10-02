using System.Numerics;
using GatherBuddy.ForkLogic;
using Xunit;

public class SprintRulesTests
{
    [Fact]
    public void The_walk_left_follows_the_path_not_the_straight_line()
    {
        Assert.Equal(0f, SprintRules.Left(Vector3.Zero, []));
        Assert.Equal(40f, SprintRules.Left(Vector3.Zero, [new Vector3(0, 0, 20), new Vector3(20, 0, 20)]));
    }

    [Fact]
    public void Sprint_only_on_foot_when_ready_on_a_long_enough_walk_and_not_already_faster()
    {
        Assert.True(SprintRules.Now(walking: true, mounted: false, faster: false, ready: true, left: 120));
        Assert.False(SprintRules.Now(walking: false, mounted: false, faster: false, ready: true, left: 120));
        Assert.False(SprintRules.Now(walking: true, mounted: true, faster: false, ready: true, left: 120));
        Assert.False(SprintRules.Now(walking: true, mounted: false, faster: true, ready: true, left: 120));
        Assert.False(SprintRules.Now(walking: true, mounted: false, faster: false, ready: false, left: 120));
        Assert.False(SprintRules.Now(walking: true, mounted: false, faster: false, ready: true, left: 12));
    }
}
