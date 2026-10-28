using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Tests.Geo;

public class PolylineTests
{
    [Fact]
    public void Points_on_a_straight_line_are_dropped()
    {
        GeoPoint[] line = [new(50, 14), new(50, 14.001), new(50, 14.002), new(50, 14.003)];

        Assert.Equal(2, Polyline.Simplify(line, toleranceMeters: 1).Count);
    }

    [Fact]
    public void A_bend_bigger_than_the_tolerance_is_kept()
    {
        // Prostřední bod je ~111 m mimo přímku.
        GeoPoint[] line = [new(50, 14), new(50.001, 14.001), new(50, 14.002)];

        Assert.Equal(3, Polyline.Simplify(line, toleranceMeters: 5).Count);
        Assert.Equal(2, Polyline.Simplify(line, toleranceMeters: 200).Count);
    }
}
