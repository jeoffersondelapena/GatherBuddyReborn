using GatherBuddy.ForkLogic;
using Xunit;

public class ListStateRulesTests
{
    private static ListStateRules.ListView View(string key, bool enabled)
        => new(key, enabled);

    [Fact]
    public void Only_lists_that_are_on_are_kept()
    {
        var state = ListStateRules.Capture(new[]
        {
            View("Gathering Log/Miner/MIN Lv 1-15", true),
            View("Gathering Log/Miner/MIN Lv 16-30", false),
            View("Fishing Log/FSH Lv 1-15", true),
        });

        Assert.Equal(new[] { "Fishing Log/FSH Lv 1-15", "Gathering Log/Miner/MIN Lv 1-15" }, state.Keys.OrderBy(k => k));
        Assert.True(state["Gathering Log/Miner/MIN Lv 1-15"].Enabled);
    }

    [Fact]
    public void A_list_the_file_does_not_name_is_off()
    {
        var state = ListStateRules.Capture(new[] { View("a/on", true) });

        Assert.True(ListStateRules.For(state, "a/on").Enabled);
        Assert.False(ListStateRules.For(state, "a/other").Enabled);
        Assert.False(ListStateRules.For(null, "a/on").Enabled);
    }

    [Theory]
    [InlineData("first", "first", true)]
    [InlineData("first", "second", false)]
    [InlineData(null, "first", false)]
    [InlineData("<none>", "first", false)]
    [InlineData("first", null, false)]
    [InlineData(null, null, false)]
    public void A_characters_file_is_written_only_from_the_state_loaded_for_it(string? appliedFor, string? current, bool expected)
        => Assert.Equal(expected, ListStateRules.MayWrite(appliedFor, current));

    [Fact]
    public void One_characters_choices_never_reach_the_other_characters_file()
    {
        var files = new Dictionary<string, string>();
        void Save(string? appliedFor, string? current, params ListStateRules.ListView[] lists)
        {
            if (ListStateRules.MayWrite(appliedFor, current))
                files[current!] = ListStateRules.Serialize(ListStateRules.Capture(lists));
        }

        Save("first", "first", View("x/list", true));
        // the second character has logged in but its own state is not in memory yet: the first one's is
        Save("first", "second", View("x/list", true));
        Assert.False(files.ContainsKey("second"));

        Save("second", "second", View("x/list", false));
        Assert.True(ListStateRules.For(ListStateRules.Deserialize(files["first"]), "x/list").Enabled);
        Assert.False(ListStateRules.For(ListStateRules.Deserialize(files["second"]), "x/list").Enabled);
    }

    [Fact]
    public void State_round_trips_and_an_older_files_ticks_are_ignored()
    {
        var state = ListStateRules.Capture(new[] { View("Gathering Log/Botanist/BTN Lv 1-15", true) });
        var back  = ListStateRules.Deserialize(ListStateRules.Serialize(state));
        Assert.True(back["Gathering Log/Botanist/BTN Lv 1-15"].Enabled);

        var older = "{\n  \"Gathering Log/Miner/MIN Lv 1-15\": {\n    \"Enabled\": true,\n    \"Off\": [\n      5106\n    ]\n  }\n}";
        Assert.True(ListStateRules.Deserialize(older)["Gathering Log/Miner/MIN Lv 1-15"].Enabled);
    }

    [Fact]
    public void A_hand_edited_file_with_gaps_still_loads()
    {
        var read = ListStateRules.Deserialize("{ \"a/x\": null, \"a/y\": { \"Enabled\": true }, \"a/z\": { \"Enabled\": false } }");
        Assert.Equal(new[] { "a/y", "a/z" }, read.Keys.OrderBy(k => k));
        Assert.True(read["a/y"].Enabled);
        Assert.False(read["a/z"].Enabled);
        Assert.Empty(ListStateRules.Deserialize("{}"));
    }

    [Fact]
    public void The_shared_file_counts_as_unchanged_when_only_its_formatting_differs()
    {
        var plugin    = "[\r\n  {\r\n    \"ItemIds\": [\r\n      5106,\r\n      5\r\n    ],\r\n    \"Name\": \"MIN Lv 1-15\",\r\n    \"Enabled\": false\r\n  }\r\n]";
        var generator = "[\n  {\n    \"Name\": \"MIN Lv 1-15\",\n    \"ItemIds\": [5106, 5],\n    \"Enabled\": false\n  }\n]";
        Assert.True(ListStateRules.SameJson(plugin, generator));

        Assert.False(ListStateRules.SameJson(plugin, generator.Replace("false", "true")));
        Assert.False(ListStateRules.SameJson(plugin, generator.Replace("5106, 5", "5, 5106")));
        Assert.False(ListStateRules.SameJson(plugin, "[ not json"));
    }

    [Fact]
    public void A_list_is_keyed_by_folder_and_name_as_the_generator_keys_it()
    {
        Assert.Equal("Gathering Log/Miner/MIN Lv 1-15", ListStateRules.KeyOf("Gathering Log/Miner", "MIN Lv 1-15"));
        Assert.Equal("/Loose list", ListStateRules.KeyOf("", "Loose list"));
        Assert.Equal("/Loose list", ListStateRules.KeyOf(null, "Loose list"));
    }

    [Fact]
    public void Forgetting_the_generated_lists_keeps_the_choices_for_hand_made_ones()
    {
        var state = new Dictionary<string, ListStateRules.Entry>
        {
            ["Gathering Log/1. Miner/MIN Lv 1-15"] = new() { Enabled = true },
            ["Gathering Log/1. Miner/Lv 1-15"]     = new() { Enabled = true },
            ["/My own"]                            = new() { Enabled = true },
        };
        var generated = new HashSet<string> { "Gathering Log/1. Miner/MIN Lv 1-15", "Gathering Log/1. Miner/Lv 1-15", "Gathering Log/1. Miner/MIN Lv 16-30" };
        Assert.True(ListStateRules.Forget(state, generated));
        Assert.Equal(["/My own"], state.Keys);
        Assert.False(ListStateRules.Forget(state, generated));
    }
}
