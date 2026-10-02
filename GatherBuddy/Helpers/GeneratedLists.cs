using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;
using GatherBuddy.Vulcan.Vendors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GatherBuddy.Helpers;

// Fork only. The list generator leaves generated-lists.json beside the config; one click puts every generated list back to it.
public static class GeneratedLists
{
    private static string SnapshotPath => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, "generated-lists.json");
    private static string BackupPath   => Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, "generated-lists.before-reset.json");

    public static bool SnapshotExists => File.Exists(SnapshotPath);

    public static void Restore(AutoGatherListsManager gatherLists)
    {
        try
        {
            var snap = JObject.Parse(File.ReadAllText(SnapshotPath));
            var tag  = snap.Value<string>("tag") ?? "[gbr-lists]";

            var crafting = string.IsNullOrEmpty(GatherBuddy.Config.CraftingLists)
                ? new List<CraftingListDefinition>()
                : JsonConvert.DeserializeObject<List<CraftingListDefinition>>(GatherBuddy.Config.CraftingLists) ?? new();
            var vendors = GatherBuddy.Config.VendorBuyLists ?? new();
            BackUp(tag, gatherLists, crafting, vendors);

            var gatherConfigs = snap["gather"]?.ToObject<AutoGatherList.Config[]>() ?? Array.Empty<AutoGatherList.Config>();
            var former   = snap["gather_former"]?.ToObject<Dictionary<string, string[]>>() ?? new();
            var choices  = gatherLists.Lists.Where(l => IsGenerated(l, tag))
                .GroupBy(CharacterListState.KeyOf)
                .ToDictionary(g => g.Key, g => new Choice(g.First().Enabled || gatherLists.PausedByRun.Contains(g.First()),
                    g.First().EnabledItems.Where(kv => !kv.Value).Select(kv => kv.Key.ItemId).ToHashSet()));
            var gathered = gatherLists.ReplaceGenerated(tag, gatherConfigs);
            foreach (var list in gatherLists.Lists.Where(l => IsGenerated(l, tag)).ToList())
            {
                if (GeneratedListRules.Carried(CharacterListState.KeyOf(list), choices, former) is not { } choice)
                    continue;

                list.Enabled = choice.On;
                foreach (var item in list.Items.ToList())
                    list.SetEnabled(item, !choice.Off.Contains(item.ItemId));
            }
            gatherLists.Save();
            gatherLists.SetActiveItems();

            var kept = crafting.Where(l => !(l.Description ?? string.Empty).StartsWith(tag)).ToList();
            var fresh = snap["crafting"]?.ToObject<List<CraftingListDefinition>>() ?? new();
            kept.AddRange(fresh);
            GatherBuddy.Config.CraftingLists = JsonConvert.SerializeObject(kept);
            GatherBuddy.Config.CraftingFolders = GeneratedListRules.Folders(GatherBuddy.Config.CraftingFolders ?? new(),
                snap["folders"]?.ToObject<List<string>>() ?? new(), snap["drop_folders"]?.ToObject<List<string>>() ?? new(),
                kept.Select(l => l.FolderPath ?? string.Empty));

            var keptVendors = vendors.Where(v => !(v.Name ?? string.Empty).StartsWith(tag)).ToList();
            var freshVendors = snap["vendor"]?.ToObject<List<VendorBuyListDefinition>>() ?? new();
            keptVendors.AddRange(freshVendors);
            GatherBuddy.Config.VendorBuyLists = keptVendors;
            GatherBuddy.Config.Save();
            GatherBuddy.VulcanWindow?.CloseListEditor();
            GatherBuddy.CraftingListManager.Reload();

            var text = $"Generated lists reset: {gathered} gathering/fishing, {fresh.Count} crafting, {freshVendors.Count} vendor (game {snap.Value<string>("game")}).";
            Communicator.Print(text);
            GatherBuddy.Log.Information($"[GeneratedLists] {text}");
        }
        catch (Exception ex)
        {
            Communicator.PrintError($"Generated lists could not be reset: {ex.Message}");
            GatherBuddy.Log.Error($"[GeneratedLists] Reset failed: {ex}");
        }
    }

    private sealed record Choice(bool On, HashSet<uint> Off);

    private static bool IsGenerated(AutoGatherList list, string tag)
        => (list.Description ?? string.Empty).StartsWith(tag);

    // One rolling copy of what the reset replaces, for a click that was a mistake.
    private static void BackUp(string tag, AutoGatherListsManager gatherLists, List<CraftingListDefinition> crafting, List<VendorBuyListDefinition> vendors)
    {
        var backup = new JObject
        {
            ["taken"]    = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["gather"]   = JArray.FromObject(gatherLists.Lists.Where(l => (l.Description ?? string.Empty).StartsWith(tag)).Select(l => new AutoGatherList.Config(l))),
            ["crafting"] = JArray.FromObject(crafting.Where(l => (l.Description ?? string.Empty).StartsWith(tag))),
            ["vendor"]   = JArray.FromObject(vendors.Where(v => (v.Name ?? string.Empty).StartsWith(tag))),
        };
        File.WriteAllText(BackupPath, backup.ToString(Formatting.Indented));
    }
}
