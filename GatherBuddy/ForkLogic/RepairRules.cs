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

    // same rule as summoning bells, with the preferred mender in place of the chosen bell town
    public static Mender? Choose(IReadOnlyList<Mender> menders, uint preferredId, uint territory, Vector3 position, Func<Mender, TripRules.Trip?> trip)
    {
        var places    = menders.Select(m => new BellRules.Bell(m.Territory, m.Position)).ToList();
        var preferred = preferredId != 0 ? menders.FirstOrDefault(m => m.Id == preferredId) : default;
        if (BellRules.Pick(territory, position, places, place => trip(menders[places.IndexOf(place)]), preferred.Territory) is not { } picked)
            return null;

        var mender = menders[places.IndexOf(picked)];
        return preferred.Territory != BellRules.Automatic && mender.Territory == preferred.Territory && mender.Territory != territory ? preferred : mender;
    }
}
