#nullable enable

namespace GatherBuddy.ForkLogic;

public static class TripRules
{
    public readonly record struct Trip(int Gil, bool Teleport);

    public static readonly Trip Aethernet = new(0, false);

    public static bool Cheaper(Trip trip, Trip than)
        => trip.Gil != than.Gil ? trip.Gil < than.Gil : !trip.Teleport && than.Teleport;

    public static bool WalkInstead(float walk, float? toShard, float shardToTarget, float hopCost)
        => toShard is not { } shard || walk <= shard + shardToTarget + hopCost;
}
