using WindowSeat.Core.Geo;
using WindowSeat.Core.Places;

namespace WindowSeat.Core.Tests.Places;

public class TestPlacesTests
{
    [Fact]
    public void Star_summer_palace_lies_north_east_of_white_mountain()
    {
        var whiteMountain = TestPlaces.All.Single(p => p.Name == "Bílá hora").Location;
        var star = TestPlaces.All.Single(p => p.Name == "Letohrádek Hvězda").Location;

        var los = LineOfSight.Between(whiteMountain, star);

        // ~534 m na sever a ~494 m na východ.
        Assert.Equal(42.8, los.AzimuthDegrees, 1.0);
        Assert.Equal(728, los.DistanceMeters, 15.0);
    }

    [Fact]
    public void Charles_bridge_is_downstream_of_bridge_of_legions_and_both_cross_near_the_national_theatre()
    {
        var charles = TestPlaces.All.Single(p => p.Name == "Karlův most").Location;
        var legions = TestPlaces.All.Single(p => p.Name == "Most Legií").Location;
        var theatre = TestPlaces.All.Single(p => p.Name == "Národní divadlo").Location;

        Assert.True(charles.LatitudeDegrees > legions.LatitudeDegrees);
        Assert.InRange(LineOfSight.GroundDistance(legions, theatre), 150, 400);
    }

    [Fact]
    public void All_places_have_distinct_names_and_lie_in_czechia()
    {
        Assert.Equal(TestPlaces.All.Count, TestPlaces.All.Select(p => p.Name).Distinct().Count());
        Assert.All(TestPlaces.All, p =>
        {
            Assert.InRange(p.Location.LatitudeDegrees, 48.5, 51.1);
            Assert.InRange(p.Location.LongitudeDegrees, 12.0, 18.9);
        });
    }
}
