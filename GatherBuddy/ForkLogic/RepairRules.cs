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

    // what repairing one worn piece by hand takes: a unit of its dark matter grade (a higher grade does too) and the repair class's level
    public readonly record struct Need(string Name, int Grade, string GradeName, string RepairClass, int Level);

    public const string IssuesHeader = "Repair paused this run:";

    public static string SlotName(int slot)
        => slot switch
        {
            0  => "main hand",
            1  => "off hand",
            2  => "head",
            3  => "body",
            4  => "hands",
            5  => "waist",
            6  => "legs",
            7  => "feet",
            8  => "ears",
            9  => "neck",
            10 => "wrists",
            11 => "right ring",
            12 => "left ring",
            13 => "soul crystal",
            _  => $"slot {slot}",
        };

    public static string Pieces(int count, int threshold)
        => $"{count} piece{(count == 1 ? "" : "s")} below {threshold}%";

    public static string StillWorn(int threshold)
        => $"gear is still below {threshold}% after a dark matter repair";

    public static string? SelfRepairBlocker(IReadOnlyList<Need> needs, IReadOnlyDictionary<int, int> darkMatter, IReadOnlyDictionary<string, int> classLevels,
        int threshold)
    {
        if (needs.Count == 0)
            return null;

        var parts = new List<string>();
        // a piece spends one unit of its grade or better, so the stock at or above a grade has to cover every piece needing at least that grade;
        // the highest grade that falls short is the one to name, since it repairs everything below it as well
        var shortGrade = needs.Select(n => n.Grade).Distinct().OrderByDescending(g => g)
            .FirstOrDefault(g => needs.Count(n => n.Grade >= g) > Units(darkMatter, g));
        if (shortGrade > 0)
        {
            var name  = needs.First(n => n.Grade == shortGrade).GradeName;
            var units = Units(darkMatter, shortGrade);
            parts.Add(units == 0
                ? $"{name} is missing, {Pieces(needs.Count, threshold)}"
                : $"{name} is short, {units} on hand for {Pieces(needs.Count, threshold)}");
        }

        foreach (var repairClass in needs.GroupBy(n => n.RepairClass).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var have = classLevels.TryGetValue(repairClass.Key, out var level) ? level : 0;
            var top  = repairClass.MaxBy(n => n.Level);
            if (top.Level <= have)
                continue;

            parts.Add(have == 0
                ? $"{repairClass.Key} is not unlocked, needs level {top.Level} for {top.Name}"
                : $"{repairClass.Key} is level {have}, needs {top.Level} for {top.Name}");
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static int Units(IReadOnlyDictionary<int, int> darkMatter, int grade)
        => darkMatter.Where(kv => kv.Key >= grade).Sum(kv => kv.Value);

    public static string PausedNote(string why)
        => $"paused: {why}";

    public static string PausedToast(string run, string why)
        => $"GatherBuddy: {TextRules.RunName(run)} run paused, {why}";

    public static string AfterRunNote(string why)
        => $"not repaired after the run: {why}";

    public static string AfterRunToast(string why)
        => $"GatherBuddy: gear not repaired after the run, {why}";

    public static string AfterRunLine(string why)
        => $"[GatherBuddy] After-run repair skipped: {why} (fork).";

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
