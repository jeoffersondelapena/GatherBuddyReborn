using System.Collections.Generic;
using System.Numerics;
using GatherBuddy.ForkLogic;
using Xunit;
using Bell = GatherBuddy.ForkLogic.BellRules.Bell;

public class BellRulesTests
{
    private const uint Apartment = 655, Limsa = 129, Kugane = 628, Doma = 759;

    private static readonly Dictionary<uint, int> Fees = new() { [Limsa] = 999, [Kugane] = 120, [Doma] = 400 };

    private static int? Fee(Bell bell)
        => Fees.TryGetValue(bell.Territory, out var gil) ? gil : null;

    [Fact]
    public void A_bell_in_the_zone_the_run_is_in_wins_over_any_teleport()
    {
        var bells = new[] { new Bell(Kugane, new Vector3(0, 0, 0)), new Bell(Limsa, new Vector3(50, 0, 0)), new Bell(Limsa, new Vector3(10, 0, 0)) };
        Assert.Equal(new Bell(Limsa, new Vector3(10, 0, 0)), BellRules.Pick(Limsa, Vector3.Zero, bells, Fee));
    }

    [Fact]
    public void With_no_bell_in_the_zone_the_cheapest_teleport_wins_not_the_shortest_walk()
    {
        var bells = new[] { new Bell(Doma, new Vector3(1, 0, 0)), new Bell(Kugane, new Vector3(95, 0, 0)), new Bell(Limsa, new Vector3(45, 0, 0)) };
        Assert.Equal(Kugane, BellRules.Pick(Apartment, Vector3.Zero, bells, Fee)!.Value.Territory);
    }

    [Fact]
    public void A_town_the_character_cannot_teleport_to_is_never_picked()
    {
        var bells = new[] { new Bell(999u, new Vector3(1, 0, 0)), new Bell(Limsa, new Vector3(45, 0, 0)) };
        Assert.Equal(Limsa, BellRules.Pick(Apartment, Vector3.Zero, bells, Fee)!.Value.Territory);
        Assert.Null(BellRules.Pick(Apartment, Vector3.Zero, [new Bell(999u, Vector3.Zero)], Fee));
    }

    [Fact]
    public void Within_the_chosen_town_the_bell_nearest_the_arrival_point_is_the_target()
    {
        var bells = new[] { new Bell(Limsa, new Vector3(-266, 16, 41)), new Bell(Limsa, new Vector3(-124, 18, 21)), new Bell(Kugane, new Vector3(-90, 0, 10)) };
        Assert.Equal(new Bell(Limsa, new Vector3(-124, 18, 21)), BellRules.NearestTo(new Vector2(-84, 0), bells, Limsa));
    }

    [Fact]
    public void A_chosen_zone_replaces_only_the_cheapest_town()
    {
        var bells = new[] { new Bell(Kugane, new Vector3(95, 0, 0)), new Bell(Limsa, new Vector3(45, 0, 0)), new Bell(Apartment + 1, new Vector3(5, 0, 0)) };
        Assert.Equal(Limsa, BellRules.Pick(Apartment, Vector3.Zero, bells, Fee, chosen: Limsa)!.Value.Territory);
        Assert.Equal(Apartment + 1, BellRules.Pick(Apartment + 1, Vector3.Zero, bells, Fee, chosen: Limsa)!.Value.Territory);
        Assert.Equal(Kugane, BellRules.Pick(Apartment, Vector3.Zero, bells, Fee, chosen: 999u)!.Value.Territory);
    }
}
