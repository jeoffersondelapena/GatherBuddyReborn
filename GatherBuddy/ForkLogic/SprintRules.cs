#nullable enable
using System.Collections.Generic;
using System.Numerics;

namespace GatherBuddy.ForkLogic;

public static class SprintRules
{
    public const uint GeneralActionId = 4;

    // a walk this short gains under a second, while the 60-second recast may leave the next long walk without it
    public const float ShortestWalk = 30f;

    public static readonly uint[] AlreadyFaster = [50, 1199, 4209];

    public static float Left(Vector3 from, IReadOnlyList<Vector3> waypoints)
    {
        var left = 0f;
        foreach (var point in waypoints)
        {
            left += Vector3.Distance(from, point);
            from =  point;
        }

        return left;
    }

    public static bool Now(bool walking, bool mounted, bool faster, bool ready, float left)
        => walking && !mounted && !faster && ready && left >= ShortestWalk;
}
