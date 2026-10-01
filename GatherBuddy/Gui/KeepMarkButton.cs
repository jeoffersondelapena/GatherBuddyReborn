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
            : $"Remove the Keep marks from the {KeepMarks.Count} marked item(s): orange (what a paused run still needs, which also clears when the run goes on)\n"
          + "and green (what a run crafted, gathered or bought, which stays until you press this).";
        if (ImGuiUtil.DrawDisabledButton("Clear Keep Marks (fork)", Vector2.Zero, tooltip, KeepMarks.Count == 0))
            KeepMarks.Clear();
    }
}
