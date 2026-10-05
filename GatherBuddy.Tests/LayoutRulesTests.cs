using GatherBuddy.ForkLogic;
using Xunit;

public class LayoutRulesTests
{
    [Fact]
    public void With_room_to_spare_the_options_get_their_height_and_the_list_the_rest()
        => Assert.Equal((300f, 250f), LayoutRules.SplitPane(available: 620, options: 250, buttons: 60, gaps: 10, minList: 60, minOptions: 80));

    [Fact]
    public void In_a_short_pane_the_options_give_way_and_scroll_while_the_buttons_and_the_list_keep_their_room()
    {
        var (list, options) = LayoutRules.SplitPane(available: 300, options: 250, buttons: 60, gaps: 10, minList: 60, minOptions: 80);
        Assert.Equal((60f, 170f), (list, options));
        Assert.Equal(300f, list + options + 60 + 10);
    }

    [Fact]
    public void A_new_row_of_options_takes_its_height_from_the_list_and_never_from_the_buttons()
    {
        var before = LayoutRules.SplitPane(available: 620, options: 250, buttons: 60, gaps: 10, minList: 60, minOptions: 80);
        var after  = LayoutRules.SplitPane(available: 620, options: 276, buttons: 60, gaps: 10, minList: 60, minOptions: 80);
        Assert.Equal(before.List - 26, after.List);
        Assert.Equal(620f, after.List + after.Options + 60 + 10);
    }

    [Fact]
    public void A_pane_too_short_for_everything_still_gives_the_options_and_the_list_their_least()
        => Assert.Equal((60f, 80f), LayoutRules.SplitPane(available: 150, options: 250, buttons: 60, gaps: 10, minList: 60, minOptions: 80));
}
