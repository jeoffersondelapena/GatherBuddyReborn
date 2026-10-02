#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GatherBuddy.ForkLogic;

// categories follow the repair window's dropdown (AgentRepair.ItemFilter), so a category doubles as the filter to select
public static class RepairRules
{
    public enum Category
    {
        Equipped,
        MainOffHand,
        HeadBodyHands,
        LegsFeet,
        NeckEars,
        WristsRings,
        Inventory,
    }

    public readonly record struct Piece(Category Category, int Percent, bool Repairable);

    public readonly record struct Mender(uint Id, uint Territory, Vector3 Position);

    public static int Percent(ushort condition)
        => condition / 300;

    public static string Label(Category category)
        => category switch
        {
            Category.Equipped      => "equipped gear",
            Category.MainOffHand   => "main/off hand",
            Category.HeadBodyHands => "head/body/hands",
            Category.LegsFeet      => "legs/feet",
            Category.NeckEars      => "neck/ears",
            Category.WristsRings   => "wrists/rings",
            _                      => "inventory",
        };

    public static IReadOnlyList<Category> Worn(IEnumerable<Piece> pieces, int threshold)
        => pieces.Where(p => p.Repairable && p.Percent < threshold)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

    public static bool AfterRun(bool enabled, bool partOfCraftingRun, bool boundByDuty)
        => enabled && !partOfCraftingRun && !boundByDuty;

    // the preferred mender in Vulcan's settings wins; otherwise a mender is picked as a summoning bell is
    public static Mender? Choose(IReadOnlyList<Mender> menders, uint preferredId, uint territory, Vector3 position, Func<Mender, TripRules.Trip?> trip)
    {
        if (preferredId != 0)
            foreach (var mender in menders.Where(m => m.Id == preferredId))
                return mender;

        var places = menders.Select(m => new BellRules.Bell(m.Territory, m.Position)).ToList();
        return BellRules.Pick(territory, position, places, place => trip(menders[places.IndexOf(place)])) is { } picked
            ? menders[places.IndexOf(picked)]
            : null;
    }
}
