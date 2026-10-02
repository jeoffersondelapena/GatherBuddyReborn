using System.Collections.Generic;
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

    [Fact]
    public void A_list_keeps_its_choices_under_its_own_key_or_the_key_it_had_before_a_rename()
    {
        var before = new Dictionary<string, string> { ["F/MIN Lv 1-15"] = "on", ["F/Lv 16-30"] = "off" };
        var former = new Dictionary<string, string[]> { ["F/MIN Lv 16-30"] = ["G/MIN Lv 16-30", "F/Lv 16-30"] };
        Assert.Equal("on", GeneratedListRules.Carried("F/MIN Lv 1-15", before, former));
        Assert.Equal("off", GeneratedListRules.Carried("F/MIN Lv 16-30", before, former));
        Assert.Null(GeneratedListRules.Carried("F/BTN Lv 1-15", before, former));
    }
}
