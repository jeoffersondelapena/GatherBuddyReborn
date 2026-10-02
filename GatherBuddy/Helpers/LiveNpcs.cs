using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// fork: event NPCs loaded around the player, nearest copy of each, read at most twice a second
internal static class LiveNpcs
{
    private static readonly Dictionary<uint, Vector3> Seen = new();
    private static DateTime _read = DateTime.MinValue;

    public static bool TryGet(uint npcId, out Vector3 position)
    {
        position = default;
        if (!Dalamud.Framework.IsInFrameworkUpdateThread)
            return false;

        Refresh();
        return Seen.TryGetValue(npcId, out position);
    }

    private static void Refresh()
    {
        if (DateTime.UtcNow - _read < TimeSpan.FromMilliseconds(500))
            return;

        _read = DateTime.UtcNow;
        Seen.Clear();
        if (Dalamud.Objects.LocalPlayer is not { } player)
            return;

        foreach (var obj in Dalamud.Objects)
        {
            if (obj.ObjectKind != ObjectKind.EventNpc || !obj.IsTargetable)
                continue;

            if (!Seen.TryGetValue(obj.BaseId, out var known)
             || Vector3.DistanceSquared(obj.Position, player.Position) < Vector3.DistanceSquared(known, player.Position))
                Seen[obj.BaseId] = obj.Position;
        }
    }
}
