using GatherBuddy.ForkLogic;
using Xunit;
using OutOfReach = GatherBuddy.ForkLogic.GatherLevelRules.OutOfReach;

public class GatherLevelRulesTests
{
    [Fact]
    public void The_level_named_is_the_lowest_the_filter_lets_through()
    {
        for (var node = 1; node <= 100; node++)
        {
            var need = GatherLevelRules.LevelNeeded(node);
            Assert.True(GatherLevelRules.Reach(need) >= node, $"node {node}: level {need} is not enough");
            if (need > 1)
                Assert.True(GatherLevelRules.Reach(need - 1) < node, $"node {node}: level {need - 1} would already do");
        }

        Assert.Equal(25, GatherLevelRules.LevelNeeded(30));
        Assert.Equal(25, GatherLevelRules.LevelNeeded(27));
        Assert.Equal(20, GatherLevelRules.LevelNeeded(25));
    }

    [Fact]
    public void One_item_reads_as_one_sentence()
        => Assert.Equal("Silex needs Miner 25 (yours is 21) for its level 30 node, so GatherBuddy left it out",
            GatherLevelRules.Reason(new[] { new OutOfReach("Silex", "Miner", 21, 30) }));

    [Fact]
    public void Several_items_are_joined_and_a_long_list_is_cut()
    {
        var two = GatherLevelRules.Reason(new[] { new OutOfReach("Silex", "Miner", 21, 30), new OutOfReach("Ash Log", "Botanist", 22, 35) });
        Assert.Equal("Silex needs Miner 25 (yours is 21) for its level 30 node and Ash Log needs Botanist 30 (yours is 22) for its level 35 node, "
          + "so GatherBuddy left them out", two);

        var five = Enumerable.Range(1, 5).Select(i => new OutOfReach($"Item {i}", "Miner", 21, 30)).ToList();
        Assert.EndsWith("Item 3 needs Miner 25 (yours is 21) for its level 30 node and 2 more, so GatherBuddy left them out", GatherLevelRules.Reason(five));
    }
}
