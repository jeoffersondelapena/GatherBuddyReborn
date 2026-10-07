#nullable enable

namespace GatherBuddy.ForkLogic;

public static class TripRules
{
    public readonly record struct Trip(int Gil, bool Teleport);

    // on foot this close to a vendor, mounting and a landing point only get in the way
    public const float WalkToVendorDistance = 40f;

    public static bool WalksToVendor(bool onFoot, float distance)
        => onFoot && distance <= WalkToVendorDistance;

    // a vendor under a canopy stands off the mesh; the last stretch is walked in a straight line
    public const float StraightWalkDistance = 12f;
    public const float StraightWalkHeight   = 2.5f;
    public const int   StraightWalkTries    = 3;

    public static bool WalksStraight(bool onFoot, float horizontal, float vertical)
        => onFoot && horizontal <= StraightWalkDistance && vertical <= StraightWalkHeight;

    public static bool StandsAbove(float horizontal, float heightAboveNpc, float interactionDistance, float maxVertical)
        => horizontal <= interactionDistance && heightAboveNpc > maxVertical;

    public static readonly Trip Aethernet = new(0, false);

    public static bool Cheaper(Trip trip, Trip than)
        => trip.Gil != than.Gil ? trip.Gil < than.Gil : !trip.Teleport && than.Teleport;

    public static bool WalkInstead(float walk, float? toShard, float shardToTarget, float hopCost)
        => toShard is not { } shard || walk <= shard + shardToTarget + hopCost;
}
