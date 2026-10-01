using GatherBuddy.ForkLogic;
using Xunit;
using Mark = GatherBuddy.ForkLogic.KeepRules.Mark;

public class KeepRulesTests
{
    private static readonly Dictionary<uint, Mark> None = new();

    private static Dictionary<string, int> Runs(Mark mark)
        => mark.ByRun.ToDictionary(kv => kv.Key, kv => kv.Value);

    [Fact]
    public void Hq_and_collectable_ids_fold_back_to_the_item_and_event_items_are_never_marked()
    {
        Assert.Equal(5106u, KeepRules.BaseItemId(5106));
        Assert.Equal(5106u, KeepRules.BaseItemId(1_005_106));
        Assert.Equal(5106u, KeepRules.BaseItemId(505_106));
        Assert.Equal(0u, KeepRules.BaseItemId(2_000_123));
    }

    [Fact]
    public void Nothing_counted_is_not_marked_and_nq_with_hq_add_up()
    {
        var marks = KeepRules.Merge(None, new[] { (5106u, 12), (1_005_106u, 2), (5107u, 3), (5108u, 0) }, "ARM Lv 1-15");
        Assert.Equal(new[] { 5106u, 5107u }, marks.Keys.OrderBy(k => k));
        Assert.Equal(14, marks[5106].Count);
    }

    [Fact]
    public void A_runs_new_stop_replaces_everything_it_marked_before()
    {
        var first = KeepRules.Merge(None, new[] { (5106u, 12), (5107u, 3) }, "ARM Lv 1-15");
        var again = KeepRules.Merge(first, new[] { (5106u, 8) }, "ARM Lv 1-15");
        Assert.Equal(new[] { 5106u }, again.Keys);
        Assert.Equal(8, again[5106].Count);
    }

    [Fact]
    public void Two_runs_keep_their_own_counts_on_one_item()
    {
        var marks = KeepRules.Merge(KeepRules.Merge(None, new[] { (5106u, 12) }, "ARM Lv 1-15"), new[] { (5106u, 20) }, "MIN Lv 16-30");
        Assert.Equal(32, marks[5106].Count);
        Assert.Equal("[Keep] 12 for ARM Lv 1-15, 20 for MIN Lv 16-30 (fork)", KeepRules.TooltipLine(marks[5106]));

        var restop = KeepRules.Merge(marks, new[] { (5106u, 4) }, "ARM Lv 1-15");
        Assert.Equal(new Dictionary<string, int> { ["MIN Lv 16-30"] = 20, ["ARM Lv 1-15"] = 4 }, Runs(restop[5106]));
    }

    [Fact]
    public void A_finished_run_takes_only_its_own_share_away()
    {
        var marks = KeepRules.Merge(KeepRules.Merge(None, new[] { (5106u, 12), (4u, 2) }, "ARM Lv 1-15"), new[] { (4u, 5) }, "Buy");
        var left  = KeepRules.WithoutRun(marks, "ARM Lv 1-15");
        Assert.Equal(new[] { 4u }, left.Keys);
        Assert.Equal(new Dictionary<string, int> { ["Buy"] = 5 }, Runs(left[4]));
    }

    [Fact]
    public void The_chat_line_points_at_the_marks_and_the_button()
    {
        Assert.StartsWith("[GatherBuddy] Marked Keep in your bags: 3 item(s)", KeepRules.Summary(3));
        var bought = KeepRules.Merge(None, new[] { (5319u, 6) }, "[gbr-lists] LTW Lv 1-15 +vendor");
        Assert.Equal("[Keep] 6 for LTW Lv 1-15 +vendor (fork)", KeepRules.TooltipLine(bought[5319]));
        Assert.Contains("Clear Keep Marks (fork)", KeepRules.Summary(3));
    }
}
