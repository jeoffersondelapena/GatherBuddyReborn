using GatherBuddy.ForkLogic;
using Xunit;

public class GeneratedListRulesTests
{
    [Fact]
    public void Dropped_folders_go_unless_a_list_still_sits_in_one_or_below_it()
    {
        var current = new[] { "Crafting Log", "Crafting Log/1. Carpenter", "Crafting Log/1. Carpenter/Lv 1-15", "Crafting Log/1. Carpenter/Lv 16-30", "Mine" };
        var added   = new[] { "Crafting Log/1. Carpenter/1. Plain" };
        var dropped = new[] { "Crafting Log/1. Carpenter/Lv 1-15", "Crafting Log/1. Carpenter/Lv 16-30" };
        var lists   = new[] { "Crafting Log/1. Carpenter/1. Plain", "Crafting Log/1. Carpenter/Lv 16-30/My own", "" };
        Assert.Equal(new[] { "Crafting Log", "Crafting Log/1. Carpenter", "Crafting Log/1. Carpenter/1. Plain", "Crafting Log/1. Carpenter/Lv 16-30", "Mine" },
            GeneratedListRules.Folders(current, added, dropped, lists));
    }
}
