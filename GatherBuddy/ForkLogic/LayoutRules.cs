#nullable enable
using System;

namespace GatherBuddy.ForkLogic;

public static class LayoutRules
{
    // the buttons keep their height; the options give way, then scroll, before the list falls under its minimum
    public static (float List, float Options) SplitPane(float available, float options, float buttons, float gaps, float minList, float minOptions)
    {
        var room    = available - buttons - gaps;
        var fitting = Math.Min(options, Math.Max(room - minList, minOptions));
        return (Math.Max(room - fitting, minList), fitting);
    }
}
