using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.Helpers;

// Fork only. A list reads better as one item per chat line than as one long sentence.
public static class ForkChat
{
    public static void List(string header, IReadOnlyList<string> items, int max = int.MaxValue, string? footer = null)
    {
        Dalamud.Chat.PrintError($"[GatherBuddy] {header} (fork)");
        foreach (var item in items.Take(max))
            Dalamud.Chat.PrintError($"    - {item}");
        if (items.Count > max)
            Dalamud.Chat.PrintError($"    - and {items.Count - max} more");
        if (footer != null)
            Dalamud.Chat.PrintError($"    {footer}");
    }
}
