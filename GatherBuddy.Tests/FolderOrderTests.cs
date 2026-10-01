using GatherBuddy.ForkLogic;
using Xunit;

public class FolderOrderTests
{
    private static readonly Dictionary<string, uint> Ranks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["carpenter"] = 8, ["blacksmith"] = 9, ["armorer"] = 10, ["goldsmith"] = 11, ["leatherworker"] = 12,
        ["weaver"] = 13, ["alchemist"] = 14, ["culinarian"] = 15, ["miner"] = 16, ["botanist"] = 17, ["fisher"] = 18,
    };

    [Fact]
    public void Crafter_folders_follow_the_character_window()
    {
        var alphabetical = new[] { "Alchemist", "Armorer", "Blacksmith", "Carpenter", "Culinarian", "Goldsmith", "Leatherworker", "Weaver" };
        Assert.Equal(new[] { "Carpenter", "Blacksmith", "Armorer", "Goldsmith", "Leatherworker", "Weaver", "Alchemist", "Culinarian" },
            FolderOrder.Sort(alphabetical, f => f, Ranks));
    }

    [Fact]
    public void Gatherer_folders_follow_it_too()
    {
        Assert.Equal(new[] { "Miner", "Botanist" }, FolderOrder.Sort(new[] { "Botanist", "Miner" }, f => f, Ranks));
    }

    [Fact]
    public void Other_folders_keep_the_alphabet_after_the_class_folders()
    {
        Assert.Equal(new[] { "Weaver", "Crafting Log", "Fishing Log", "Gathering Log" },
            FolderOrder.Sort(new[] { "Gathering Log", "Fishing Log", "Weaver", "Crafting Log" }, f => f, Ranks));
        Assert.Equal(new[] { "Lv 1-15", "Lv 16-30", "Lv 91-100" }, FolderOrder.Sort(new[] { "Lv 91-100", "Lv 16-30", "Lv 1-15" }, f => f, Ranks));
    }
}
