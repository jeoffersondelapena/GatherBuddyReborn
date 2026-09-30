using GatherBuddy.ForkLogic;
using Xunit;

public class CharacterSettingsRulesTests
{
    [Fact]
    public void A_character_without_a_choice_uses_the_shared_mount()
    {
        Assert.Equal(62u, CharacterSettingsRules.Mount(null, 62));
        Assert.Equal(62u, CharacterSettingsRules.Mount(new CharacterSettingsRules.Own(), 62));
    }

    [Fact]
    public void A_characters_own_mount_wins_and_roulette_is_a_choice()
    {
        Assert.Equal(71u, CharacterSettingsRules.Mount(new CharacterSettingsRules.Own { MountId = 71 }, 62));
        Assert.Equal(0u, CharacterSettingsRules.Mount(new CharacterSettingsRules.Own { MountId = 0 }, 62));
    }

    [Fact]
    public void Two_characters_keep_different_mounts_beside_one_shared_setting()
    {
        var first  = CharacterSettingsRules.Deserialize(CharacterSettingsRules.Serialize(new CharacterSettingsRules.Own { MountId = 71 }));
        var second = CharacterSettingsRules.Deserialize(CharacterSettingsRules.Serialize(new CharacterSettingsRules.Own { MountId = 0 }));

        Assert.Equal(71u, CharacterSettingsRules.Mount(first, 62));
        Assert.Equal(0u, CharacterSettingsRules.Mount(second, 62));
    }

    [Fact]
    public void A_file_without_a_mount_falls_back_to_the_shared_one()
    {
        Assert.Equal(62u, CharacterSettingsRules.Mount(CharacterSettingsRules.Deserialize("{}"), 62));
        Assert.Equal(62u, CharacterSettingsRules.Mount(CharacterSettingsRules.Deserialize("{ \"MountId\": null }"), 62));
        Assert.Equal(5u, CharacterSettingsRules.Mount(CharacterSettingsRules.Deserialize("{ \"MountId\": 5, \"SomethingNewer\": true }"), 62));
    }
}
