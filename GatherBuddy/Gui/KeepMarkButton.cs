using System.Numerics;
using Dalamud.Bindings.ImGui;
using ElliLib;
using GatherBuddy.Helpers;

namespace GatherBuddy.Gui;

internal static class KeepMarkButton
{
    private const string Green = "Green: what a run's list counts toward its targets, including what you already had:\n"
      + "a crafting list's own recipes (not their materials or in-between crafts), a gathering list's items, a buy list's items.\n"
      + "A run that is still going counts them again whenever they change; once it has ended, a clear stays.";

    public static void DrawGreen()
    {
        var count = KeepMarks.MadeCount;
        if (ImGuiUtil.DrawDisabledButton("Clear Green Marks (fork)", Vector2.Zero,
                $"{Green}\n\n{(count == 0 ? "Nothing is marked green." : $"{count} item(s) marked green.")}", count == 0))
            KeepMarks.ClearMade();
    }
}
