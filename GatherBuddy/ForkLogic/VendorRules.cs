#nullable enable
using System.Collections.Generic;
using System.Numerics;

namespace GatherBuddy.ForkLogic;

// gil prices are the item's own, the same at every vendor, so only the trip differs; Trip is null when the character cannot get there
public static class VendorRules
{
    public readonly record struct Place(uint Territory, Vector3 Position, TripRules.Trip? Trip);

    public static int Pick(IReadOnlyList<Place?> places, uint territory, Vector3 position)
    {
        int here = -1, cheapest = -1, known = -1;
        var nearest = float.MaxValue;
        TripRules.Trip lowest = default;
        for (var i = 0; i < places.Count; i++)
        {
            if (places[i] is not { } place)
                continue;

            if (known < 0)
                known = i;
            if (place.Territory == territory)
            {
                var distance = Vector3.DistanceSquared(place.Position, position);
                if (distance < nearest)
                {
                    nearest = distance;
                    here    = i;
                }
            }
            else if (place.Trip is { } trip && (cheapest < 0 || TripRules.Cheaper(trip, lowest)))
            {
                lowest   = trip;
                cheapest = i;
            }
        }

        return here >= 0 ? here : cheapest >= 0 ? cheapest : known;
    }
}
