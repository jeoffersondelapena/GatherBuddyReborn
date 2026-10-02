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
    private static Task?          _scan;
    private static DateTime       _lastScan = DateTime.MinValue;

    // the bells of the zone the run is in and of every town; false while they are still being read
    public static bool TryGet(uint territory, out List<BellRules.Bell> bells)
    {
        if (_towns is { } towns && Read.ContainsKey(territory) && towns.All(Read.ContainsKey))
        {
            bells = towns.Append(territory).Distinct().SelectMany(t => Read[t]).ToList();
            return true;
        }

        bells = [];
        if (_scan is not { IsCompleted: false } && DateTime.UtcNow - _lastScan > TimeSpan.FromSeconds(5))
        {
            _lastScan = DateTime.UtcNow;
            _scan     = Task.Run(() => Scan(territory));
        }

        return false;
    }

    private static void Scan(uint territory)
    {
        try
        {
            var sheet = Dalamud.GameData.GetExcelSheet<TerritoryType>();
            _bellIds ??= BellIds();
            _towns   ??= sheet.Where(t => t.TerritoryIntendedUse.RowId == 0).Select(t => t.RowId).ToArray();
            foreach (var id in _towns.Append(territory).Distinct())
                if (!Read.ContainsKey(id))
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
