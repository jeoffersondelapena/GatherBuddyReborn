using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GatherBuddy.Helpers;

namespace GatherBuddy.Crafting;

// Fork only. Recipes the game would not open for this character, kept per character until a retry or a game patch.
public static class SkippedRecipes
{
    private sealed class Store
    {
        public string                   Game    { get; set; } = "";
        public Dictionary<uint, string> Recipes { get; set; } = new();
    }

    private static readonly object Lock = new();
    private static ulong           _owner = ulong.MaxValue;
    private static string          _path  = "";
    private static Store           _store = new();

    public static List<string> LeftOutThisRun { get; } = new();
    public static List<string> LockedThisRun  { get; } = new();

    public static int Count
    {
        get { lock (Lock) return Current().Recipes.Count; }
    }

    public static bool IsRemembered(uint recipeId)
    {
        lock (Lock) return Current().Recipes.ContainsKey(recipeId);
    }

    public static void Remember(uint recipeId, string reason)
    {
        lock (Lock)
        {
            Current().Recipes[recipeId] = reason;
            Save();
        }
    }

    public static int Clear()
    {
        lock (Lock)
        {
            var n = Current().Recipes.Count;
            _store.Recipes.Clear();
            Save();
            return n;
        }
    }

    public static List<string> Names()
    {
        lock (Lock)
            return Current().Recipes.Keys.Select(NameOf).OrderBy(n => n).ToList();
    }

    public static string NameOf(uint recipeId)
    {
        var recipe = RecipeManager.GetRecipe(recipeId);
        return recipe != null ? recipe.Value.ItemResult.Value.Name.ExtractText() : $"Recipe {recipeId}";
    }

    public static string Summary(int shown = 5) => Brief(Names(), shown);

    public static string Brief(IReadOnlyList<string> names, int shown = 5)
        => string.Join(", ", names.Take(shown)) + (names.Count > shown ? $" and {names.Count - shown} more" : "");

    private static unsafe Store Current()
    {
        var owner = Dalamud.ClientState.IsLoggedIn ? PlayerState.Instance()->ContentId : 0;
        if (owner == _owner)
            return _store;

        _owner = owner;
        _path  = Path.Combine(Dalamud.PluginInterface.ConfigDirectory.FullName, $"skipped-{ForkLog.CharacterKey()}.json");
        _store = new Store { Game = GameVersion() };
        try
        {
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<Store>(File.ReadAllText(_path));
                if (loaded != null && loaded.Game == _store.Game)
                    _store = loaded;
            }
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[SkippedRecipes] {_path} unreadable, starting empty: {ex.Message}");
        }
        return _store;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_store));
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[SkippedRecipes] {_path} not saved: {ex.Message}");
        }
    }

    private static string GameVersion()
    {
        try { return Dalamud.GameData.GameData.Repositories["ffxiv"].Version; }
        catch (Exception) { return ""; }
    }
}
