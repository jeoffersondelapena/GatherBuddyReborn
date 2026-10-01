using System;
using System.Linq;
using GatherBuddy.AutoGather.Lists;
using GatherBuddy.Crafting;
using GatherBuddy.ForkLogic;

namespace GatherBuddy.Helpers;

// fork: two windows run different lists at once
public static class RunLabel
{
    public static string? Current
    {
        get
        {
            try
            {
                if (CraftingGatherBridge.RunningListName is { } crafting)
                    return crafting;

                if (GatherBuddy.AutoGather?.Enabled == true)
                    return GatherLists();

                return GatherBuddy.VendorBuyListManager?.RunningListName;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static string? GatherLists()
        => CraftingGatherBridge.ListsManager is { } manager
            ? TextRules.RunLabel(manager.Lists
                .Where(l => l.Enabled && l.Name != AutoGatherListsManager.TemporaryListName)
                .Select(l => l.Name)
                .ToList())
            : null;
}
