#nullable enable

namespace GatherBuddy.ForkLogic;

public static class TripRules
{
    public readonly record struct Trip(int Gil, bool Teleport);

    // on foot this close to a vendor, mounting and a landing point only get in the way
    public const float WalkToVendorDistance = 40f;

    public static bool WalksToVendor(bool onFoot, float distance)
        => onFoot && distance <= WalkToVendorDistance;

    public static readonly Trip Aethernet = new(0, false);

    public static bool Cheaper(Trip trip, Trip than)
        => trip.Gil != than.Gil ? trip.Gil < than.Gil : !trip.Teleport && than.Teleport;

    public static bool WalkInstead(float walk, float? toShard, float shardToTarget, float hopCost)
        => toShard is not { } shard || walk <= shard + shardToTarget + hopCost;
}
