#nullable enable

namespace GatherBuddy.ForkLogic;

// a list's buying and synthesis choices are radios of widening scope; aiming for HQ greys what would give NQ
public enum BuyCrafts
{
    Craft,
    WhenOutOfReach,
    InsteadOfCrafting,
}

public enum Materials
{
    GatherAll,
    BuyInsteadOfWaiting,
    BuyInsteadOfGathering,
}

public enum Synthesis
{
    Normal,
    QuickSynthPrecrafts,
    QuickSynthEverything,
}

public enum VendorRuns
{
    Off,
    AsListsSay,
    EverythingSold,
}

public static class ListRules
{
    public static BuyCrafts Precrafts(bool aimForHq, BuyCrafts chosen)
        => aimForHq && chosen == BuyCrafts.InsteadOfCrafting ? BuyCrafts.WhenOutOfReach : chosen;

    public static BuyCrafts Finals(bool aimForHq, BuyCrafts chosen)
        => aimForHq ? BuyCrafts.Craft : chosen;

    public static Synthesis SynthesisOf(bool quickSynthAll, bool precraftsOnly)
        => !quickSynthAll ? Synthesis.Normal : precraftsOnly ? Synthesis.QuickSynthPrecrafts : Synthesis.QuickSynthEverything;

    public static (bool QuickSynthAll, bool PrecraftsOnly) SynthesisFlags(Synthesis chosen)
        => (chosen != Synthesis.Normal, chosen == Synthesis.QuickSynthPrecrafts);

    public static Synthesis EffectiveSynthesis(bool aimForHq, Synthesis chosen)
        => aimForHq ? Synthesis.Normal : chosen;

    public static bool NqOnlyApplies(bool aimForHq, Synthesis chosen)
        => EffectiveSynthesis(aimForHq, chosen) != Synthesis.Normal;

    public static Materials MaterialsOf(bool insteadOfWaiting, bool insteadOfGathering)
        => !insteadOfWaiting ? Materials.GatherAll : insteadOfGathering ? Materials.BuyInsteadOfGathering : Materials.BuyInsteadOfWaiting;

    public static (bool InsteadOfWaiting, bool InsteadOfGathering) MaterialsFlags(Materials chosen)
        => (chosen != Materials.GatherAll, chosen == Materials.BuyInsteadOfGathering);

    public static bool TriesStartAfresh(bool aimForHq, bool oncePerDay)
        => aimForHq && !oncePerDay;

    public static VendorRuns Master(bool on, bool everythingSold)
        => !on ? VendorRuns.Off : everythingSold ? VendorRuns.EverythingSold : VendorRuns.AsListsSay;

    public static (bool On, bool EverythingSold) MasterFlags(VendorRuns master)
        => (master != VendorRuns.Off, master == VendorRuns.EverythingSold);

    public static Materials EffectiveMaterials(VendorRuns master, Materials chosen)
        => master switch
        {
            VendorRuns.Off            => Materials.GatherAll,
            VendorRuns.EverythingSold => Materials.BuyInsteadOfGathering,
            _                         => chosen,
        };

    // the override buys as much as a list aiming for HQ allows; final crafts are never bought on its account
    public static BuyCrafts EffectivePrecrafts(VendorRuns master, bool aimForHq, BuyCrafts chosen)
        => master switch
        {
            VendorRuns.Off            => BuyCrafts.Craft,
            VendorRuns.EverythingSold => Precrafts(aimForHq, BuyCrafts.InsteadOfCrafting),
            _                         => Precrafts(aimForHq, chosen),
        };

    public static BuyCrafts EffectiveFinals(VendorRuns master, bool aimForHq, BuyCrafts chosen)
        => master == VendorRuns.Off ? BuyCrafts.Craft : Finals(aimForHq, chosen);
}
