using System.Collections.Generic;
using GatherBuddy.ForkLogic;
using GatherBuddy.Plugin;

namespace GatherBuddy.Helpers;

// Fork only. A list reads better as one item per chat line than as one long sentence.
public static class ForkChat
{
    public static void List(string header, IReadOnlyList<string> items, int max = int.MaxValue, string? footer = null,
        Communicator.Tone tone = Communicator.Tone.Problem)
    {
        foreach (var line in TextRules.ListLines(TextRules.WithRunLabel(header, RunLabel.Current), items, max, footer))
            Communicator.PrintTinted(line, tone);
    }
}
