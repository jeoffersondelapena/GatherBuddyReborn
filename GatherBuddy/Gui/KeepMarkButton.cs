using System.Numerics;
using ElliLib;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

internal static class KeepMarkButton
{
    public static void Draw()
    {
        var tooltip = KeepMarks.Count == 0
            ? "Nothing in your bags is marked Keep."
            : $"Remove the Keep mark (tooltip line and green frame) from the {KeepMarks.Count} item(s) marked when a run stopped with full bags.\n"
          + "A crafting run that finishes removes its own marks; the others stay until you press this.";
        if (ImGuiUtil.DrawDisabledButton("Clear Keep Marks (fork)", Vector2.Zero, tooltip, KeepMarks.Count == 0))
            KeepMarks.Clear();
    }
}
