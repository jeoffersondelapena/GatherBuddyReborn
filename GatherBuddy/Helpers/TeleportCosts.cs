using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using GatherBuddy.Plugin;
using Lumina.Excel.Sheets;

namespace GatherBuddy.Helpers;

// fork: the game's teleport list prices from where the player stands and holds attuned aetherytes only, so no cost means no teleport
internal static class TeleportCosts
{
    private static readonly Dictionary<uint, int>    Costs = new();
    private static readonly Dictionary<uint, uint[]> Gates = new();
    private static DateTime _read = DateTime.MinValue;
    private static (ulong, uint) _readFor;

    public static int? For(uint aetheryteId)
    {
        Refresh();
        return aetheryteId != 0 && Costs.TryGetValue(aetheryteId, out var gil) ? gil : null;
    }

    // the cheapest of the zone's own aetherytes, else of the main aetheryte whose aethernet reaches the zone (Steps of Thal, Old Gridania)
    public static int? ToZone(uint territory)
    {
        if (!Gates.TryGetValue(territory, out var gates))
            Gates[territory] = gates = GatesFor(territory);
        int? best = null;
        foreach (var gate in gates)
            if (For(gate) is { } gil && (best == null || gil < best))
                best = gil;
        return best;
    }

    private static uint[] GatesFor(uint territory)
    {
        var sheet = Dalamud.GameData.GetExcelSheet<Aetheryte>();
        var own   = sheet.Where(a => a.IsAetheryte && a.Territory.RowId == territory).Select(a => a.RowId).ToArray();
        if (own.Length > 0)
            return own;

        var groups = sheet.Where(a => !a.IsAetheryte && a.Territory.RowId == territory && a.AethernetName.RowId != 0).Select(a => a.AethernetGroup).ToHashSet();
        return sheet.Where(a => a.IsAetheryte && groups.Contains(a.AethernetGroup)).Select(a => a.RowId).ToArray();
    }

    private static unsafe void Refresh()
    {
        var at = (PlayerState.Instance()->ContentId, (uint)Dalamud.ClientState.TerritoryType);
        if (at == _readFor && DateTime.UtcNow - _read < TimeSpan.FromSeconds(5))
            return;

        _read    = DateTime.UtcNow;
        _readFor = at;
        Costs.Clear();
        var telepo = Telepo.Instance();
        if (!Dalamud.ClientState.IsLoggedIn || telepo == null)
            return;

        telepo->UpdateAetheryteList();
        for (var i = 0; i < telepo->TeleportList.Count; i++)
        {
            var entry = telepo->TeleportList[i];
            Costs.TryAdd(entry.AetheryteId, (int)entry.GilCost);
        }
    }
}
