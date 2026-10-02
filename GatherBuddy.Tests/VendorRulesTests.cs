using System.Numerics;
using GatherBuddy.ForkLogic;
using Xunit;
using Place = GatherBuddy.ForkLogic.VendorRules.Place;

public class VendorRulesTests
{
    private const uint Limsa = 129, Gridania = 133, Kugane = 628;

    [Fact]
    public void A_vendor_in_the_players_zone_wins_and_the_nearest_of_them()
        => Assert.Equal(2, VendorRules.Pick([new Place(Kugane, Vector3.Zero, 90), new Place(Limsa, new Vector3(80, 0, 0), 999), new Place(Limsa, new Vector3(5, 0, 0), 999)],
            Limsa, Vector3.Zero));

    [Fact]
    public void Elsewhere_the_cheapest_teleport_wins_not_the_order_the_game_lists_them_in()
        => Assert.Equal(1, VendorRules.Pick([new Place(Kugane, Vector3.Zero, 1200), new Place(Gridania, Vector3.Zero, 300), new Place(Limsa, Vector3.Zero, null)],
            999u, Vector3.Zero));

    [Fact]
    public void With_no_known_cost_the_first_vendor_with_a_known_place_stays_the_choice()
    {
        Assert.Equal(1, VendorRules.Pick([null, new Place(Kugane, Vector3.Zero, null), new Place(Limsa, Vector3.Zero, null)], 999u, Vector3.Zero));
        Assert.Equal(-1, VendorRules.Pick([null, null], 999u, Vector3.Zero));
    }
}
