using GatherBuddy.ForkLogic;
using Xunit;
using Mark = GatherBuddy.ForkLogic.KeepRules.Mark;
using Run = GatherBuddy.ForkLogic.KeepRules.Run;

public class KeepRulesTests
{
    private static readonly Dictionary<uint, Mark> None = new();
    private static readonly Run Ltw    = new(1, "LTW Lv 1-15", "crafted");
    private static readonly Run Gsm    = new(2, "GSM Lv 1-15", "crafted");
    private static readonly Run Bought = new(3, "[gbr-lists] GSM Lv 1-15 +vendor", "bought");

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
        var marks = KeepRules.Merge(None, new[] { (5106u, 12), (1_005_106u, 2), (5107u, 3), (5108u, 0) }, Ltw);
        Assert.Equal(new[] { 5106u, 5107u }, marks.Keys.OrderBy(k => k));
        Assert.Equal(14, marks[5106].Count);
    }

    [Fact]
    public void A_run_marking_again_replaces_its_own_marks_and_leaves_other_runs()
    {
        var first = KeepRules.Merge(KeepRules.Merge(None, new[] { (5106u, 12), (5107u, 3) }, Ltw), new[] { (5106u, 20) }, Gsm);
        var again = KeepRules.Merge(first, new[] { (5106u, 4) }, Ltw);
        Assert.Equal(24, again[5106].Count);
        Assert.False(again.ContainsKey(5107));
        Assert.Equal(new[] { 5106u }, KeepRules.WithoutRun(again, Ltw).Keys);
        Assert.Equal(20, KeepRules.WithoutRun(again, Ltw)[5106].Count);
    }

    [Fact]
    public void Two_runs_of_one_list_are_kept_apart_but_read_as_one()
    {
        var again = new Run(4, "LTW Lv 1-15", "crafted");
        var marks = KeepRules.Merge(KeepRules.Merge(None, new[] { (5319u, 2) }, Ltw), new[] { (5319u, 3) }, again);
        Assert.Equal(5, marks[5319].Count);
        Assert.Equal("[Keep] 5 crafted by LTW Lv 1-15 (fork)", KeepRules.MadeLine(marks[5319]));
    }

    [Fact]
    public void The_tooltip_names_each_run_and_drops_a_generated_list_tag()
    {
        var made = KeepRules.Merge(KeepRules.Merge(None, new[] { (5319u, 3) }, Gsm), new[] { (5319u, 6) }, Bought);
        Assert.Equal("[Keep] 3 crafted by GSM Lv 1-15, 6 bought by GSM Lv 1-15 +vendor (fork)", KeepRules.MadeLine(made[5319]));
        var needed = KeepRules.Merge(None, new[] { (5319u, 11) }, Ltw);
        Assert.Equal("[Keep] 11 still needed by LTW Lv 1-15 (fork)", KeepRules.NeededLine(needed[5319]));
    }

    [Fact]
    public void A_mark_shrinks_with_what_leaves_the_bags_oldest_run_first()
    {
        var marks = KeepRules.Merge(KeepRules.Merge(None, new[] { (5319u, 4), (5320u, 2) }, Ltw), new[] { (5319u, 6) }, Gsm);
        var held  = new Dictionary<uint, int> { [5319] = 7, [5320] = 0 };
        var left  = KeepRules.Shrink(marks, held);
        Assert.Equal(new[] { 5319u }, left.Keys);
        Assert.Equal(new Dictionary<Run, int> { [Ltw] = 1, [Gsm] = 6 }, left[5319].ByRun.ToDictionary(kv => kv.Key, kv => kv.Value));
        Assert.Same(marks[5319], KeepRules.Shrink(marks, new Dictionary<uint, int> { [5319] = 10, [5320] = 2 })[5319]);
    }

    [Fact]
    public void What_a_run_made_is_what_it_added_to_the_bags()
    {
        var before = new Dictionary<uint, int> { [5319] = 2, [5320] = 5, [5321] = 0 };
        var now    = new Dictionary<uint, int> { [5319] = 5, [5320] = 3, [5321] = 1 };
        Assert.Equal(new Dictionary<uint, int> { [5319] = 3, [5321] = 1 }, KeepRules.Made(before, now));
    }

    [Fact]
    public void The_chat_lines_say_which_color_means_what()
    {
        Assert.Contains("9 item(s) this run still needs, in orange", KeepRules.PauseSummary(9, 0));
        Assert.DoesNotContain("green", KeepRules.PauseSummary(9, 0));
        Assert.Contains("2 item(s) it has made so far, in green", KeepRules.PauseSummary(9, 2));
        Assert.StartsWith("[GatherBuddy] Marked in green in your bags: 12 item(s) this run crafted.", KeepRules.EndSummary(12, "crafted"));
    }
}
