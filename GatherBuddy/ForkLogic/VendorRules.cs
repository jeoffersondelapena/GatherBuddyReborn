#nullable enable
using System.Collections.Generic;
using System.Numerics;

namespace GatherBuddy.ForkLogic;

// gil prices are the item's own, the same at every vendor, so only the trip differs; Cost is null when the character cannot teleport there
public static class VendorRules
{
    public readonly record struct Place(uint Territory, Vector3 Position, int? Cost);

    public static int Pick(IReadOnlyList<Place?> places, uint territory, Vector3 position)
    {
        int here = -1, cheapest = -1, known = -1;
        var nearest = float.MaxValue;
        var lowest  = int.MaxValue;
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
            else if (place.Cost is { } gil && gil < lowest)
            {
                lowest   = gil;
                cheapest = i;
            }
        }

        return here >= 0 ? here : cheapest >= 0 ? cheapest : known;
    }
}
