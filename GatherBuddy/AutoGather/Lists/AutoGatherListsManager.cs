using GatherBuddy.Classes;
using FFXIVClientStructs.FFXIV.Client.Game;
using ElliLib.Filesystem;
using GatherBuddy.Interfaces;
using GatherBuddy.Plugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Functions = GatherBuddy.Plugin.Functions;

namespace GatherBuddy.AutoGather.Lists;

public class ManualOrderSortMode : ISortMode<AutoGatherList>
{
    public ReadOnlySpan<byte> Name
        => "Manual Order"u8;

    public ReadOnlySpan<byte> Description
        => "Sort by manually assigned order, with folders first."u8;

    public IEnumerable<FileSystem<AutoGatherList>.IPath> GetChildren(FileSystem<AutoGatherList>.Folder folder)
    {
        var folders = folder.GetSubFolders().Cast<FileSystem<AutoGatherList>.IPath>();
        var leaves = folder.GetLeaves()
            .OrderBy(l => l.Value.Order)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Cast<FileSystem<AutoGatherList>.IPath>();
        return folders.Concat(leaves);
    }
}

public partial class AutoGatherListsManager : IDisposable
{
    public event Action? ActiveItemsChanged;

    private const string FileName         = "auto_gather_lists.json";
    public const  string TemporaryListName = "Crafting Materials (Auto-Generated)";

    private string? _sharedJson;
    private int     _loginRetries;
    private const string FileNameFallback = "gather_window.json";

    private readonly FileSystem<AutoGatherList>             _fileSystem;
    private readonly List<(IGatherable Item, uint Quantity)> _activeItems   = [];
    private readonly List<(IGatherable Item, uint Quantity)> _fallbackItems = [];
    private readonly HashSet<IGatherable>                    _localInventoryActiveItems = [];
    public static ManualOrderSortMode SortMode { get; } = new();

    public FileSystem<AutoGatherList> FileSystem
        => _fileSystem;

    public IEnumerable<AutoGatherList> Lists
        => _fileSystem.Select(kvp => kvp.Key);

    public ReadOnlyCollection<(IGatherable Item, uint Quantity)> ActiveItems
        => _activeItems.AsReadOnly();

    public ReadOnlyCollection<(IGatherable Item, uint Quantity)> FallbackItems
        => _fallbackItems.AsReadOnly();

    internal bool UsesRetainerInventory(IGatherable item)
        => !_localInventoryActiveItems.Contains(item);

    public AutoGatherListsManager()
    {
        _fileSystem = new FileSystem<AutoGatherList>();
        _fileSystem.Changed += OnFileSystemChanged;
        Dalamud.ClientState.Login  += OnLogin;
        Dalamud.ClientState.Logout += OnLogout;
    }

    private AutoGatherListsManager(AutoGatherList.Config[] configs, string rawText)
    {
        _fileSystem = new FileSystem<AutoGatherList>();
        var change = false;

        foreach (var cfg in configs)
        {
            if (cfg.Name == TemporaryListName)
                continue;
            change |= AutoGatherList.FromConfig(cfg, out var list);

            var folderPath = string.IsNullOrEmpty(list.FolderPath) ? string.Empty : list.FolderPath;

            if (folderPath == list.Name)
            {
                folderPath = string.Empty;
                change = true;
            }

            var folderNames = folderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var folder = _fileSystem.Root;
            foreach (var folderName in folderNames)
            {
                (folder, _) = _fileSystem.FindOrCreateFolder(folder, folderName);
            }

            try
            {
                _fileSystem.CreateLeaf(folder, list.Name, list);
            }
            catch
            {
                _fileSystem.CreateDuplicateLeaf(folder, list.Name, list);
                change = true;
            }
        }

        ApplyCharacterState();
        var normalized = SharedJson(SharedLists());
        if (normalized == rawText)
            _sharedJson = normalized;
        if (change)
            Save();

        _fileSystem.Changed += OnFileSystemChanged;
        Dalamud.ClientState.Login  += OnLogin;
        Dalamud.ClientState.Logout += OnLogout;
        SetActiveItems();
    }

    private List<AutoGatherList> SharedLists()
    {
        var lists = _fileSystem.Select(kvp => kvp.Key).Where(l => l.Name != TemporaryListName).ToList();
        foreach (var list in lists)
            if (_fileSystem.TryGetValue(list, out var leaf))
                list.FolderPath = leaf.Parent.IsRoot ? string.Empty : leaf.Parent.FullName();
        return lists;
    }

    // The shared file holds what the lists are; whether one is on, and which items are ticked, is kept per character.
    private static string SharedJson(IEnumerable<AutoGatherList> lists)
        => JsonConvert.SerializeObject(lists.Select(l =>
        {
            var cfg = new AutoGatherList.Config(l) { Enabled = false };
            foreach (var id in cfg.EnabledItems.Keys.ToList())
                cfg.EnabledItems[id] = true;
            return cfg;
        }), Formatting.Indented);

    private void ApplyCharacterState()
    {
        var state = CharacterListState.Load();
        foreach (var list in SharedLists())
        {
            if (state == null)
            {
                list.Enabled = false;
                continue;
            }

            state.TryGetValue(CharacterListState.KeyOf(list.FolderPath, list.Name), out var entry);
            list.Enabled = entry?.Enabled ?? false;
            foreach (var item in list.Items.ToList())
                list.SetEnabled(item, entry == null || !entry.Off.Contains(item.ItemId));
        }
    }

    private void SaveCharacterState(IEnumerable<AutoGatherList> lists)
    {
        if (CharacterListState.Key() == null)
            return;

        var state = new Dictionary<string, CharacterListState.Entry>();
        foreach (var list in lists)
        {
            var off = list.EnabledItems.Where(kv => !kv.Value).Select(kv => kv.Key.ItemId).OrderBy(id => id).ToList();
            if (list.Enabled || off.Count > 0)
                state[CharacterListState.KeyOf(list.FolderPath, list.Name)] = new CharacterListState.Entry { Enabled = list.Enabled, Off = off };
        }

        CharacterListState.Save(state);
    }

    private void OnLogin()
    {
        if (CharacterListState.Key() == null && _loginRetries++ < 10)
        {
            Dalamud.Framework.RunOnTick(OnLogin, TimeSpan.FromSeconds(1));
            return;
        }

        _loginRetries = 0;
        ApplyCharacterState();
        SetActiveItems();
    }

    private void OnLogout(int type, int code)
    {
        foreach (var list in SharedLists())
            list.Enabled = false;
        SetActiveItems();
    }

    private void OnFileSystemChanged(FileSystemChangeType type, FileSystem<AutoGatherList>.IPath changedObject, FileSystem<AutoGatherList>.IPath? previousParent, FileSystem<AutoGatherList>.IPath? newParent)
    {
        // Not renumbering the source folder on ObjectRemoved or ObjectMoved makes the numbering sparse, but it's fine for ordering.
        if (type is FileSystemChangeType.ObjectMoved or FileSystemChangeType.LeafAdded && changedObject is FileSystem<AutoGatherList>.Leaf newLeaf)
        {
            newLeaf.Value.Order = newLeaf.Parent.GetLeaves().Where(leaf => leaf != newLeaf).Select(leaf => leaf.Value.Order).DefaultIfEmpty().Max() + 1;
            Save();
        }
    }

    public void Dispose()
    {
        Dalamud.ClientState.Login  -= OnLogin;
        Dalamud.ClientState.Logout -= OnLogout;
    }

    // the gathering and fishing logs of the logged-in character
    private static bool IsLogged(IGatherable item)
        => item switch
        {
            Gatherable g => QuestManager.IsGatheringItemGathered((ushort)g.GatheringId),
            Fish f       => GatherBuddy.FishLog?.IsUnlocked(f) ?? false,
            _            => false,
        };

    private static readonly List<string> _skippedAsLogged = [];

    private static bool SkipAsLogged(IGatherable item)
    {
        if (!IsLogged(item))
            return false;
        _skippedAsLogged.Add(item.Name[GatherBuddy.Language]);
        return true;
    }

    // one line per refresh, so the always-on log stays readable
    private static void ReportSkippedAsLogged()
    {
        if (_skippedAsLogged.Count == 0)
            return;
        var shown = string.Join(", ", _skippedAsLogged.Take(12)) + (_skippedAsLogged.Count > 12 ? $", +{_skippedAsLogged.Count - 12} more" : "");
        GatherBuddy.Log.Information($"[AutoGather] Skip Logged Items left out {_skippedAsLogged.Count} item(s) already in the log: {shown}");
        _skippedAsLogged.Clear();
    }

    public void SetActiveItems(bool removeCompletedItems = false)
    {
        if (removeCompletedItems && RemoveCompletedItemsFromEnabledLists())
            Save();
        _activeItems.Clear();
        _fallbackItems.Clear();
        _localInventoryActiveItems.Clear();

        var items = _fileSystem.Root.GetAllDescendants(SortMode)
            .OfType<FileSystem<AutoGatherList>.Leaf>()
            .Select(leaf => leaf.Value)
            .Where(l => l.Enabled)
            .SelectMany(l => l.Items.Select(i => (Item: i, Quantity: l.Quantities[i], l.Fallback, ItemEnabled: l.EnabledItems[i], l.UsesRetainerInventory, l.SkipLoggedItems)))
            .Where(i => i.ItemEnabled && !(i.SkipLoggedItems && SkipAsLogged(i.Item)))
            .GroupBy(i => (i.Item, i.Fallback))
            .Select(x => (x.Key.Item, Quantity: (uint)Math.Min(x.Sum(g => g.Quantity), uint.MaxValue), x.Key.Fallback, UsesRetainerInventory: x.All(g => g.UsesRetainerInventory)));

        ReportSkippedAsLogged();
        foreach (var (item, quantity, fallback, usesRetainerInventory) in items)
        {
            if (fallback)
            {
                _fallbackItems.Add((item, quantity));
            }
            else
            {
                if (!usesRetainerInventory)
                    _localInventoryActiveItems.Add(item);
                _activeItems.Add((item, quantity));
            }
        }

        ActiveItemsChanged?.Invoke();
    }

    public void Save()
    {
        var file = Functions.ObtainSaveFile(FileName);
        if (file == null)
        {
            GatherBuddy.Log.Error("Failed to obtain save file for auto-gather lists");
            return;
        }

        try
        {
            var lists = SharedLists();
            var text  = SharedJson(lists);
            if (text != _sharedJson)
            {
                File.WriteAllText(file.FullName, text);
                _sharedJson = text;
            }

            SaveCharacterState(lists);
        }
        catch (Exception e)
        {
            GatherBuddy.Log.Error($"Error serializing auto-gather lists data:\n{e}");
        }
    }

    public static AutoGatherListsManager Load()
    {
        var file = Functions.ObtainSaveFile(FileName);
        if (file is not { Exists: true })
        {
            file = Functions.ObtainSaveFile(FileNameFallback);
        }

        if (file is { Exists: true })
        {
            try
            {
                var text = File.ReadAllText(file.FullName);
                var configs = JsonConvert.DeserializeObject<AutoGatherList.Config[]>(text);
                if (configs != null)
                    return new AutoGatherListsManager(configs, text);
            }
            catch (Exception e)
            {
                GatherBuddy.Log.Error($"Error deserializing auto gather lists:\n{e}");
                Communicator.PrintError($"[GatherBuddy Reborn] Auto gather lists failed to load and have been reset.");
            }
        }

        return new AutoGatherListsManager();
    }
}
