#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GatherBuddy.ForkLogic;

// a far town never wins on a short walk alone: the zone the run is in comes first, then the cheapest teleport
public static class BellRules
{
    public readonly record struct Bell(uint Territory, Vector3 Position);

    public const uint Automatic = 0;

    // cost is null for a bell the character cannot teleport to
    public static Bell? Pick(uint territory, Vector3 position, IEnumerable<Bell> bells, Func<Bell, int?> cost, uint chosen = Automatic)
    {
        Bell? here = null, mine = null, town = null;
        var nearest = float.MaxValue;
        var cheapest = int.MaxValue;
        foreach (var bell in bells)
        {
            if (bell.Territory == territory)
            {
                var distance = Vector3.DistanceSquared(bell.Position, position);
                if (distance < nearest)
                {
                    nearest = distance;
                    here    = bell;
                }
                continue;
            }

            if (cost(bell) is not { } gil)
                continue;

            if (bell.Territory == chosen)
            {
                mine ??= bell;
                continue;
            }

            if (gil < cheapest || gil == cheapest && town is { } held && bell.Territory < held.Territory)
            {
                cheapest = gil;
                town     = bell;
            }
        }

        return here ?? mine ?? town;
    }

    public static Bell? NearestTo(Vector2 point, IEnumerable<Bell> bells, uint territory)
    {
        Bell? best = null;
        var nearest = float.MaxValue;
        foreach (var bell in bells)
        {
            if (bell.Territory != territory)
                continue;

            var distance = Vector2.DistanceSquared(new Vector2(bell.Position.X, bell.Position.Z), point);
            if (distance < nearest)
            {
                nearest = distance;
                best    = bell;
            }
        }

        return best;
    }
}
