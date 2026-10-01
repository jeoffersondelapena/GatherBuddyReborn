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

        Assert.Equal(26, GatherLevelRules.LevelNeeded(30));
        Assert.Equal(21, GatherLevelRules.LevelNeeded(25));
        Assert.Equal(1, GatherLevelRules.LevelNeeded(5));
    }

    [Fact]
    public void The_wiki_example_holds_and_a_tier_opens_one_level_after_the_multiple_of_five()
    {
        Assert.Equal(25, GatherLevelRules.Reach(21));
        Assert.True(GatherLevelRules.Reach(25) < 30);
        Assert.True(GatherLevelRules.Reach(26) >= 30);
    }

    [Fact]
    public void One_item_reads_as_one_sentence()
        => Assert.Equal("Silex needs Miner 26 (yours is 21) for its level 30 node, so GatherBuddy left it out",
            GatherLevelRules.Reason(new[] { new OutOfReach("Silex", "Miner", 21, 30) }));

    [Fact]
    public void The_window_gets_a_short_form()
    {
        Assert.Equal("Silex needs Miner 26", GatherLevelRules.Short(new[] { new OutOfReach("Silex", "Miner", 22, 30) }));
        Assert.Equal("2 items need a higher level",
            GatherLevelRules.Short(new[] { new OutOfReach("Silex", "Miner", 22, 30), new OutOfReach("Ash Log", "Botanist", 22, 35) }));
    }

    [Fact]
    public void Several_items_are_joined_and_a_long_list_is_cut()
    {
        var two = GatherLevelRules.Reason(new[] { new OutOfReach("Silex", "Miner", 21, 30), new OutOfReach("Ash Log", "Botanist", 22, 35) });
        Assert.Equal("Silex needs Miner 26 (yours is 21) for its level 30 node and Ash Log needs Botanist 31 (yours is 22) for its level 35 node, "
          + "so GatherBuddy left them out", two);

        var five = Enumerable.Range(1, 5).Select(i => new OutOfReach($"Item {i}", "Miner", 21, 30)).ToList();
        Assert.EndsWith("Item 3 needs Miner 26 (yours is 21) for its level 30 node and 2 more, so GatherBuddy left them out", GatherLevelRules.Reason(five));
    }
}
