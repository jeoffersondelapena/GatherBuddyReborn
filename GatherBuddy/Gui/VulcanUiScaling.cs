using System.Numerics;
using Dalamud.Interface.Utility;

namespace GatherBuddy.Gui;

internal static class VulcanUiScaling
{
    // fork: the list panels of the crafting lists tab and the buy-list window share one width, enough for the generated names three folders deep
    internal const float ListPanelWidth = 360f;

    internal static float Scale
        => ImGuiHelpers.GlobalScale;

    internal static float Scaled(float value)
        => value > 0f ? value * Scale : value;

    internal static Vector2 Scaled(float x, float y)
        => new(Scaled(x), Scaled(y));

    internal static Vector2 Scaled(Vector2 value)
        => new(Scaled(value.X), Scaled(value.Y));

    internal static Vector2 Unscaled(Vector2 value)
        => Scale > 0f ? value / Scale : value;
}
