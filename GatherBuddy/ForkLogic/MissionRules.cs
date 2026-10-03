#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

public static class MissionRules
{
    public const string SupplyList       = "GC Supply Missions";
    public const string ProvisioningList = "GC Provisioning Missions";
    public const string Tag              = "[gc-missions]";

    public readonly record struct Mission(uint ItemId, int Requested);

    // the window's other text (headings, counts) names no mission item, so only names from the game's mission table count
    public static List<Mission> FromNames(IEnumerable<string> texts, IReadOnlyDictionary<string, Mission> known, IReadOnlyList<Mission>? exact = null)
    {
        var found = new List<Mission>();
        foreach (var text in texts)
        {
            if (!known.TryGetValue(text.Trim(), out var mission) || found.Any(m => m.ItemId == mission.ItemId))
                continue;

            var counted = exact?.FirstOrDefault(m => m.ItemId == mission.ItemId) ?? default;
            found.Add(counted.ItemId != 0 && counted.Requested > 0 ? counted : mission);
        }

        return found;
    }

    // waiting for a window is the only way left when no always-up node, no gil vendor and not the bags supply the item
    public static bool Waits(int? nodeLocation, int? fishLocation, bool diademRaw, bool soldForGil, int held, int needed)
        => (nodeLocation != null || fishLocation != null)
         && !PurchaseRules.GatheredWithoutWaiting(nodeLocation, fishLocation, diademRaw)
         && !soldForGil
         && held < needed;

    public static int Crafts(int requested, int perCraft)
        => (Math.Max(1, requested) + Math.Max(1, perCraft) - 1) / Math.Max(1, perCraft);

    public static string Description(DateTime day, int count)
        => $"{Tag} {day:yyyy-MM-dd}, {count} item(s)";
}
