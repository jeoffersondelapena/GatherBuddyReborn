using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// fork: where the game's own zone files place summoning bells (any of the EObj rows named like row 2000401), read off the framework thread
internal static class BellLocations
{
    private const uint SummoningBell = 2000401;

    private static readonly string[] LayoutFiles = ["planlive.lgb", "planmap.lgb", "planevent.lgb", "bg.lgb"];
    private static readonly ConcurrentDictionary<uint, BellRules.Bell[]> Read = new();

    private static HashSet<uint>? _bellIds;
    private static uint[]?        _towns;
    private static uint[]?        _wards;
    private static Task?          _scan;
    private static DateTime       _lastScan = DateTime.MinValue;

    // false until the zone files have been read off the framework thread
    public static bool TryGet(uint territory, uint chosen, out List<BellRules.Bell> bells)
    {
        var wanted = Wanted(territory, chosen);
        if (wanted != null && wanted.All(Read.ContainsKey))
        {
            bells = wanted.SelectMany(t => Read[t]).ToList();
            return true;
        }

        bells = [];
        StartScan(territory, chosen);
        return false;
    }

    public static List<(uint Territory, string Name)> Zones()
    {
        if (_towns is not { } towns || _wards is not { } wards || !towns.Concat(wards).All(Read.ContainsKey))
        {
            StartScan(Dalamud.ClientState.TerritoryType, BellRules.Automatic);
            return [];
        }

        var sheet = Dalamud.GameData.GetExcelSheet<TerritoryType>();
        return towns.Concat(wards)
            .Where(t => Read[t].Length > 0)
            .Select(t => (t, sheet.GetRowOrDefault(t)?.PlaceName.ValueNullable?.Name.ExtractText() ?? $"zone {t}"))
            .OrderBy(z => z.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static uint[]? Wanted(uint territory, uint chosen)
        => _towns is { } towns
            ? towns.Append(territory).Concat(chosen is BellRules.Automatic or BellRules.Home ? [] : [chosen]).Distinct().ToArray()
            : null;

    private static void StartScan(uint territory, uint chosen)
    {
        if (_scan is { IsCompleted: false } || DateTime.UtcNow - _lastScan <= TimeSpan.FromSeconds(5))
            return;

        _lastScan = DateTime.UtcNow;
        _scan     = Task.Run(() => Scan(territory, chosen));
    }

    private static void Scan(uint territory, uint chosen)
    {
        try
        {
            var sheet = Dalamud.GameData.GetExcelSheet<TerritoryType>();
            _bellIds ??= BellIds();
            // housing districts need an aethernet hop into a ward, so they are used only when chosen
            _towns ??= sheet.Where(t => t.TerritoryIntendedUse.RowId == 0 && !t.Bg.ExtractText().Contains("/hou/")).Select(t => t.RowId).ToArray();
            _wards ??= sheet.Where(t => t.TerritoryIntendedUse.RowId == 13).Select(t => t.RowId).ToArray();
            foreach (var id in _towns.Concat(_wards).Append(territory).Append(chosen).Distinct())
                if (id is not (BellRules.Automatic or BellRules.Home) && !Read.ContainsKey(id))
                    Read[id] = sheet.TryGetRow(id, out var row) ? ReadZone(row) : [];
            ForkTrace.Info($"bell travel: read {Read.Values.Sum(b => b.Length)} summoning bells in {Read.Count(r => r.Value.Length > 0)} of {Read.Count} zones");
        }
        catch (Exception ex)
        {
            GatherBuddy.Log.Warning($"[fork] reading summoning bells from the zone files failed: {ex.Message}");
        }
    }

    private static BellRules.Bell[] ReadZone(TerritoryType territory)
    {
        var bg    = territory.Bg.ExtractText();
        var level = bg.IndexOf("/level/", StringComparison.Ordinal);
        if (level < 0)
            return [];

        var bells = new List<BellRules.Bell>();
        foreach (var file in LayoutFiles)
        {
            LgbFile? lgb;
            try
            {
                lgb = Dalamud.GameData.GetFile<LgbFile>($"bg/{bg[..(level + 1)]}level/{file}");
            }
            catch (Exception)
            {
                continue;
            }

            if (lgb == null)
                continue;

            foreach (var layer in lgb.Layers)
            {
                if (layer.FestivalID != 0)
                    continue;

                foreach (var obj in layer.InstanceObjects)
                {
                    if (obj.AssetType != LayerEntryType.EventObject
                     || !_bellIds!.Contains(((LayerCommon.EventInstanceObject)obj.Object).ParentData.BaseId))
                        continue;

                    var at = obj.Transform.Translation;
                    bells.Add(new BellRules.Bell(territory.RowId, new Vector3(at.X, at.Y, at.Z)));
                }
            }
        }

        return bells.ToArray();
    }

    private static HashSet<uint> BellIds()
    {
        var names = Dalamud.GameData.GetExcelSheet<EObjName>();
        var bell  = names.GetRowOrDefault(SummoningBell)?.Singular.ExtractText() ?? string.Empty;
        return bell.Length == 0
            ? [SummoningBell]
            : names.Where(r => r.Singular.ExtractText().Equals(bell, StringComparison.OrdinalIgnoreCase)).Select(r => r.RowId).ToHashSet();
    }
}
