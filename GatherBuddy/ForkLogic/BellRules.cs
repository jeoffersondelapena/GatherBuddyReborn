#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;

namespace GatherBuddy.ForkLogic;

// a far town never wins on a short walk alone: the zone the run is in comes first, then the cheapest trip (null: out of reach)
public static class BellRules
{
    public readonly record struct Bell(uint Territory, Vector3 Position);

    public const uint Automatic = 0;

    public static Bell? Pick(uint territory, Vector3 position, IEnumerable<Bell> bells, Func<Bell, TripRules.Trip?> trip, uint chosen = Automatic)
    {
        Bell? here = null, mine = null, town = null;
        var nearest = float.MaxValue;
        TripRules.Trip cheapest = default;
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

            if (trip(bell) is not { } way)
                continue;

            if (bell.Territory == chosen)
            {
                mine ??= bell;
                continue;
            }

            if (town is not { } held || TripRules.Cheaper(way, cheapest) || !TripRules.Cheaper(cheapest, way) && bell.Territory < held.Territory)
            {
                cheapest = way;
                town     = bell;
            }
        }

        return here ?? mine ?? town;
    }

    public static Bell? NearestTo(IReadOnlyCollection<Vector2> landing, IEnumerable<Bell> bells, uint territory)
    {
        Bell? best = null;
        var nearest = float.MaxValue;
        foreach (var bell in bells)
        {
            if (bell.Territory != territory)
                continue;

            foreach (var point in landing)
            {
                var distance = Vector2.DistanceSquared(new Vector2(bell.Position.X, bell.Position.Z), point);
                if (distance < nearest)
                {
                    nearest = distance;
                    best    = bell;
                }
            }
        }

        return best;
    }
}
