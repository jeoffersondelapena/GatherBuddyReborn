using System.Collections.Generic;
using GatherBuddy.ForkLogic;

namespace GatherBuddy.Helpers;

// Fork only. A list reads better as one item per chat line than as one long sentence.
public static class ForkChat
{
    public static void List(string header, IReadOnlyList<string> items, int max = int.MaxValue, string? footer = null)
    {
        foreach (var line in TextRules.ListLines(header, items, max, footer))
            Dalamud.Chat.PrintError(line);
    }
}
