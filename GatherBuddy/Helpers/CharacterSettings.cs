using System;
using System.IO;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.ForkLogic;

namespace GatherBuddy.Helpers;

// Fork only. Two game windows share the main settings file, so what differs between characters lives beside it.
public static class CharacterSettings
{
    private const string NoOwner = "<none>";

    private static string?                   _owner = NoOwner;
    private static CharacterSettingsRules.Own _own  = new();

    public static uint MountId
    {
        get => CharacterSettingsRules.Mount(Current(), GatherBuddy.Config.AutoGatherConfig.AutoGatherMountId);
        set
        {
            var own = Current();
            if (own == null)
            {
                GatherBuddy.Config.AutoGatherConfig.AutoGatherMountId = value;
                GatherBuddy.Config.Save();
                return;
            }

            own.MountId = value;
            Save(own);
        }
    }

    public static uint BellZone
    {
        get => Current()?.BellZone ?? BellRules.Automatic;
        set
        {
            if (Current() is not { } own)
                return;

            own.BellZone = value == BellRules.Automatic ? null : value;
            Save(own);
        }
    }

    private static void Save(CharacterSettingsRules.Own own)
    {
        try
        {
            SafeFile.Write(PathFor(_owner!), CharacterSettingsRules.Serialize(own));
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[CharacterSettings] not saved: {e.Message}");
        }
    }

    private static string PathFor(string key)
        => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, $"character-{key}.json");

    private static CharacterSettingsRules.Own? Current()
    {
        var key = CharacterListState.Key();
        if (key == null)
            return null;
        if (key == _owner)
            return _own;

        _own = new CharacterSettingsRules.Own();
        try
        {
            var path = PathFor(key);
            if (File.Exists(path))
                _own = CharacterSettingsRules.Deserialize(SafeFile.Read(path, attempts: 1));
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Warning($"[CharacterSettings] unreadable, using the shared settings: {e.Message}");
        }

        _owner = key;
        return _own;
    }
}
