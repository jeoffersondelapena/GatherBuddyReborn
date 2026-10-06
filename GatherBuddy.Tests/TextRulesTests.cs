using GatherBuddy.ForkLogic;
using Xunit;

public class TextRulesTests
{
    private const string Source = "GatherBuddyReborn crafting";

    [Fact]
    public void A_list_goes_to_chat_one_item_per_line()
    {
        Assert.Equal(
            new[] { "[GatherBuddy] Run finished: 1 recipe(s) done, 2 could not be made (fork):", "    - Brass Alembic (missing Brass Ingot)", "    - Iron Plate (needs Armorer level 14, yours is 9)" },
            TextRules.ListLines("Run finished: 1 recipe(s) done, 2 could not be made:", new[] { "Brass Alembic (missing Brass Ingot)", "Iron Plate (needs Armorer level 14, yours is 9)" }));
    }

    [Fact]
    public void A_long_list_is_cut_with_a_count_and_a_footer_comes_last()
    {
        var lines = TextRules.ListLines("Left out:", new[] { "a", "b", "c", "d" }, 2, "Retry skipped (fork) tries them again.").ToList();
        Assert.Equal(new[] { "[GatherBuddy] Left out (fork):", "    - a", "    - b", "    - and 2 more", "    Retry skipped (fork) tries them again." }, lines);
        Assert.Single(TextRules.ListLines("Nothing:", Array.Empty<string>()));
    }

    [Fact]
    public void Names_are_summed_up_after_the_first_few()
    {
        Assert.Equal("a, b", TextRules.Brief(new[] { "a", "b" }));
        Assert.Equal("a, b and 3 more", TextRules.Brief(new[] { "a", "b", "c", "d", "e" }, 2));
        Assert.Equal("", TextRules.Brief(Array.Empty<string>()));
    }

    private const string NoSolution =
        "Exit code -1073740791: \nthread 'main' (2592) panicked at raphael-cli\\src\\commands\\solve.rs:357:34:\nFailed to solve: NoSolution\nnote: run with `RUST_BACKTRACE=1` environment variable to display a backtrace\n";

    [Fact]
    public void A_solver_that_found_no_way_is_told_apart_from_a_solver_that_failed()
    {
        Assert.True(TextRules.IsNoSolution(NoSolution));
        Assert.False(TextRules.IsNoSolution("Solve timeout"));
        Assert.False(TextRules.IsNoSolution("raphael-cli.exe not found at Z:\\plugin\\raphael-cli.exe"));
        Assert.False(TextRules.IsNoSolution(null));
    }

    [Fact]
    public void A_failure_is_named_by_its_first_line_kept_short()
    {
        Assert.Equal("Exit code -1073740791:", TextRules.FirstLine(NoSolution));
        Assert.Equal("Solve timeout", TextRules.FirstLine("\n\n  Solve timeout  \nmore"));
        Assert.Equal("unknown", TextRules.FirstLine(null));
        Assert.Equal("unknown", TextRules.FirstLine(" \n "));

        var cut = TextRules.FirstLine(new string('x', 200));
        Assert.Equal(90, cut.Length);
        Assert.EndsWith("...", cut);
    }

    [Fact]
    public void A_note_is_added_beside_the_other_sources_notes()
    {
        var lines = TextRules.WithNote(new[] { "Boot: a boot wedged at 10:02", "" }, Source, "the Raphael solver failed (Solve timeout)");
        Assert.Equal(new[] { "Boot: a boot wedged at 10:02", "GatherBuddyReborn crafting: the Raphael solver failed (Solve timeout)" }, lines);
    }

    [Fact]
    public void The_first_failure_stands_until_it_is_cleared()
    {
        var first = TextRules.WithNote(Array.Empty<string>(), Source, "first failure")!;
        Assert.Null(TextRules.WithNote(first, Source, "second failure"));
        Assert.Equal(new[] { "GatherBuddyReborn crafting: first failure" }, first);
    }

    [Fact]
    public void Clearing_removes_only_this_sources_line()
    {
        var lines = new[] { "GatherBuddyReborn solver: binary missing", "GatherBuddyReborn crafting: first failure", "Network: packet loss" };
        Assert.Equal(new[] { "GatherBuddyReborn solver: binary missing", "Network: packet loss" }, TextRules.WithNote(lines, Source, null));
        Assert.Null(TextRules.WithNote(new[] { "Network: packet loss" }, Source, null));
        Assert.Empty(TextRules.WithNote(new[] { "GatherBuddyReborn crafting: x" }, Source, null)!);
    }

    [Fact]
    public void A_run_is_named_by_its_list_or_lists()
    {
        Assert.Null(TextRules.RunLabel(Array.Empty<string>()));
        Assert.Equal("ARM Lv 1-15", TextRules.RunLabel(new[] { "ARM Lv 1-15" }));
        Assert.Equal("MIN Lv 1-15 + BTN Lv 1-15", TextRules.RunLabel(new[] { "MIN Lv 1-15", "BTN Lv 1-15" }));
        Assert.Equal("MIN Lv 1-15 + 2 more", TextRules.RunLabel(new[] { "MIN Lv 1-15", "BTN Lv 1-15", "FSH Lv 1-15" }));
    }

    [Fact]
    public void A_chat_line_carries_the_run_label_after_the_plugin_tag()
    {
        Assert.Equal("[GatherBuddy] [ARM Lv 1-15] Run finished: all 3 recipe(s) done (fork).",
            TextRules.WithRunLabel("[GatherBuddy] Run finished: all 3 recipe(s) done (fork).", "ARM Lv 1-15"));
        Assert.Equal("[GatherBuddyReborn] [Buy] Vendor stopped.", TextRules.WithRunLabel("[GatherBuddyReborn] Vendor stopped.", "Buy"));
        Assert.Equal("[MIN Lv 1-15] No position data for fishing spot X.", TextRules.WithRunLabel("No position data for fishing spot X.", "MIN Lv 1-15"));
        Assert.Equal("[GatherBuddyReborn] Vendor list 'Buy' complete.", TextRules.WithRunLabel("[GatherBuddyReborn] Vendor list 'Buy' complete.", "Buy"));
        Assert.Equal("plain", TextRules.WithRunLabel("plain", null));
    }

    [Fact]
    public void A_generated_buy_list_shows_without_its_tag_and_a_bare_line_gets_the_plugin_tag()
    {
        Assert.Equal("[GatherBuddy] [GSM Lv 1-15 +vendor] Run finished: all 6 item(s) bought (fork).",
            TextRules.WithRunLabel("[GatherBuddy] Run finished: all 6 item(s) bought (fork).", "[gbr-lists] GSM Lv 1-15 +vendor"));
        Assert.Equal("[GatherBuddyReborn] Vendor list '[gbr-lists] GSM Lv 1-15 +vendor' complete.",
            TextRules.WithRunLabel("[GatherBuddyReborn] Vendor list '[gbr-lists] GSM Lv 1-15 +vendor' complete.", "[gbr-lists] GSM Lv 1-15 +vendor"));
        Assert.Equal("[only a tag]", TextRules.ShownLabel("[only a tag]"));
        Assert.Equal("[GatherBuddy] No saved gear set for Miner found.", TextRules.Tagged("No saved gear set for Miner found."));
        Assert.Equal("[GatherBuddyReborn] Timed out.", TextRules.Tagged("[GatherBuddyReborn] Timed out."));
        Assert.Equal("Leatherworker", TextRules.Capitalized("leatherworker"));
    }

    [Fact]
    public void The_label_says_which_kind_of_run_a_line_comes_from()
    {
        var crafting = TextRules.KindLabel(TextRules.Crafting, "GSM Lv 1-15 +vendor");
        var buying   = TextRules.KindLabel(TextRules.BuyList, "[gbr-lists] GSM Lv 1-15 +vendor");
        Assert.Equal("[GatherBuddy] [Crafting: GSM Lv 1-15 +vendor] Run finished: all 3 recipe(s) done (fork).",
            TextRules.WithRunLabel("[GatherBuddy] Run finished: all 3 recipe(s) done (fork).", crafting));
        Assert.Equal("[GatherBuddy] [Buy list: GSM Lv 1-15 +vendor] Run finished: all 6 item(s) bought (fork).",
            TextRules.WithRunLabel("[GatherBuddy] Run finished: all 6 item(s) bought (fork).", buying));
        Assert.Equal("[GatherBuddyReborn] Vendor list '[gbr-lists] GSM Lv 1-15 +vendor' complete.",
            TextRules.WithRunLabel("[GatherBuddyReborn] Vendor list '[gbr-lists] GSM Lv 1-15 +vendor' complete.", buying));
        Assert.Null(TextRules.KindLabel(TextRules.Gathering, null));
        Assert.Equal(TextRules.BuyList, TextRules.KindOfVerb("bought"));
        Assert.Equal(TextRules.Gathering, TextRules.KindOfVerb("gathered"));
        Assert.Equal(TextRules.Crafting, TextRules.KindOfVerb("crafted"));
    }

    [Fact]
    public void A_run_is_framed_by_dividers_naming_its_kind_and_list_and_they_take_no_second_label()
    {
        var label = TextRules.KindLabel(TextRules.Crafting, "GSM Lv 1-15 +vendor")!;
        Assert.Equal("[GatherBuddy] ======== Crafting: GSM Lv 1-15 +vendor STARTED (fork) ========", TextRules.DividerStart(label));
        Assert.Equal("[GatherBuddy] ======== Crafting: GSM Lv 1-15 +vendor ENDED (fork) ========", TextRules.DividerEnd(label));
        Assert.Equal(TextRules.DividerEnd(label), TextRules.WithRunLabel(TextRules.DividerEnd(label), label));
    }

    [Fact]
    public void A_part_that_stops_short_names_its_own_skip_button_in_the_same_words()
    {
        var buying = TextRules.StoppedShort(TextRules.BuyList, "Vendor location data is still loading.");
        Assert.Equal("[GatherBuddy] Buying for this crafting run stopped: Vendor location data is still loading. Fix the cause and press Resume to buy "
          + "the rest, or press Skip Buying (fork) in the Craft Status window to go on to gathering and crafting without them (fork).", buying);
        var gathering = TextRules.StoppedShort(TextRules.Gathering, null);
        Assert.Equal("[GatherBuddy] Gathering for this crafting run stopped: it could not go on. Fix the cause and press Resume to gather the rest, "
          + "or press Skip Gathering (fork) in the Craft Status window to craft with what is here (fork).", gathering);
        Assert.Contains(TextRules.SkipBuying, TextRules.StoppedShortReason(TextRules.BuyList, "your bags are full"));
        Assert.Contains(TextRules.SkipGathering, TextRules.StoppedShortReason(TextRules.Gathering, "your bags are full"));
        Assert.EndsWith("(fork)", TextRules.SkipBuying);
        Assert.EndsWith("(fork)", TextRules.SkipGathering);
        Assert.Equal("[GatherBuddy] Taking from your retainers for this crafting run stopped: no summoning bell could be reached (no path to it). "
          + "Fix the cause and press Resume to try again, or press Skip Retainers (fork) in the Craft Status window to go on without them (fork).",
            TextRules.StoppedShort(TextRules.Retainers, "no summoning bell could be reached (no path to it)"));
        Assert.Equal("[GatherBuddy] Taking from your retainers for this gathering run stopped: your bags are full. "
          + "Fix the cause and press Resume to try again, or press Skip Retainers (fork) in the Auto-Gather tab to go on without them (fork).",
            TextRules.StoppedShort(TextRules.Retainers, "your bags are full", TextRules.Gathering));
        Assert.Equal("[GatherBuddy] Taking from your retainers for this buy run stopped: your bags are full. "
          + "Fix the cause and press Resume to try again, or press Skip Retainers (fork) in the Vendor Buy List window to go on without them (fork).",
            TextRules.StoppedShort(TextRules.Retainers, "your bags are full", TextRules.BuyList));
        Assert.Equal("[GatherBuddy] Buying for this gathering run stopped: 1 item(s) could not be bought. Fix the cause and press Resume to buy the rest, "
          + "or press Skip Buying (fork) in the Auto-Gather tab to gather them instead (fork).",
            TextRules.StoppedShort(TextRules.BuyList, "1 item(s) could not be bought", TextRules.Gathering));
        Assert.Equal("[GatherBuddy] This gathering run stopped: Inventory is full. Fix the cause and press Resume in the Auto-Gather tab to gather "
          + "the rest, or untick Enabled there to end the run (fork).", TextRules.RunStopped(TextRules.Gathering, "Inventory is full"));
        Assert.Equal("[GatherBuddy] This buy run stopped: your bags are full. Fix the cause and press Resume in the Vendor Buy List window to buy "
          + "the rest, or press Stop there to end the run (fork).", TextRules.RunStopped(TextRules.BuyList, "your bags are full"));
        Assert.Contains(TextRules.SkipRetainers, TextRules.StoppedShortReason(TextRules.Retainers, "x"));
    }

    [Fact]
    public void A_run_that_cannot_get_a_material_says_to_get_it_and_does_not_speak_of_buying()
    {
        var chat = TextRules.CannotGet(2);
        Assert.Equal("[GatherBuddy] This crafting run needs 2 material(s) it cannot get itself, each listed below with the reason. Get them "
          + "and press Resume, or press Skip Buying (fork) in the Craft Status window to go on without them (fork).", chat);
        Assert.DoesNotContain("buy the rest", chat);
        Assert.Equal("2 material(s) the run cannot get itself. Get them and press Resume, or Skip Buying (fork).", TextRules.CannotGetReason(2));
    }

    [Fact]
    public void A_chat_line_quoted_as_a_reason_loses_its_tag_and_its_closing_mark()
    {
        Assert.Equal("No fishing gearset was found", TextRules.AsReason("[GatherBuddy] No fishing gearset was found (fork)."));
        Assert.Equal("Fishing data collection is off. Turn it on or take fish off the lists",
            TextRules.AsReason("[GatherBuddyReborn] [Auto-Gather] Fishing data collection is off. Turn it on or take fish off the lists."));
        Assert.Equal("plain words", TextRules.AsReason("  plain words  "));
        Assert.Null(TextRules.AsReason(null));
        Assert.Null(TextRules.AsReason("[GatherBuddy] (fork)."));
    }

    [Fact]
    public void An_hq_try_reports_how_long_an_nq_result_counts()
    {
        Assert.Equal("Bronze Awl: HQ", TextRules.HqOutcome("Bronze Awl", true, true, "04:00"));
        Assert.Equal("Bronze Awl: NQ, counts as done until 04:00", TextRules.HqOutcome("Bronze Awl", false, true, "04:00"));
        Assert.Equal("Bronze Awl: NQ, counts as done for this run", TextRules.HqOutcome("Bronze Awl", false, false, "04:00"));
    }

    [Fact]
    public void A_pause_over_crafts_names_the_button_that_goes_on_without_them()
    {
        Assert.Contains(TextRules.LeaveOut, TextRules.CannotMake(2));
        Assert.Contains("2 craft(s)", TextRules.CannotMakeReason(2));
        Assert.Contains("'Bronze Awl' is missing Jellyfish Humours (needs 1, you have 0)", TextRules.MissingAtCraft("Bronze Awl", "Jellyfish Humours (needs 1, you have 0)"));
        Assert.Contains(TextRules.LeaveOut, TextRules.MissingAtCraftReason("Bronze Awl", "x"));
    }

    [Fact]
    public void A_recipe_yielding_more_than_one_shows_what_the_crafts_come_to()
    {
        Assert.Equal("\u2192 9", TextRules.Yield(3, 3));
        Assert.Null(TextRules.Yield(3, 1));
        Assert.Equal("Quantity counts crafts. This recipe yields 3 per craft, so 3 craft(s) give 9 items.", TextRules.YieldTip(3, 3));
    }
}
