using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Tests.Geo;

public class LineOfSightTests
{
    private static readonly GeoPoint Prague = new(50.0755, 14.4378, 200);

    [Fact]
    public void Target_due_north_has_azimuth_zero()
    {
        var los = LineOfSight.Between(Prague, Prague with { LatitudeDegrees = 50.2 });

        Assert.Equal(0, los.AzimuthDegrees, 0.01);
    }

    [Fact]
    public void Target_due_east_has_azimuth_ninety()
    {
        var los = LineOfSight.Between(Prague, Prague with { LongitudeDegrees = 14.5 });

        Assert.Equal(90, los.AzimuthDegrees, 0.1);
    }

    [Fact]
    public void Hill_a_kilometre_away_and_hundred_metres_higher_is_about_six_degrees_up()
    {
        // 1 km na sever ≈ 0,008993° šířky.
        var hill = new GeoPoint(Prague.LatitudeDegrees + 0.008993, Prague.LongitudeDegrees, Prague.AltitudeMeters + 100);

        var los = LineOfSight.Between(Prague, hill);

        Assert.Equal(5.71, los.ElevationDegrees, 0.05);
        Assert.Equal(1005, los.DistanceMeters, 5.0);
    }

    [Fact]
    public void Curvature_lowers_a_distant_target_at_the_same_altitude()
    {
        // Jeden stupeň šířky ≈ 111 km; zakřivení sníží cíl o d / 2R ≈ 0,5°.
        var los = LineOfSight.Between(Prague, Prague with { LatitudeDegrees = 51.0755 });

        Assert.Equal(-0.5, los.ElevationDegrees, 0.02);
    }

    [Fact]
    public void From_cruising_altitude_the_horizon_is_about_357_km_away()
    {
        Assert.Equal(357_000, LineOfSight.HorizonDistance(10_000), 1_000.0);
    }

    [Theory]
    [InlineData(2.7, true)]   // ~300 km
    [InlineData(3.6, false)]  // ~400 km
    public void Sea_level_target_is_visible_from_a_plane_only_within_the_horizon(double latitudeOffset, bool expected)
    {
        var plane = new GeoPoint(45, 14, 10_000);
        var target = new GeoPoint(45 + latitudeOffset, 14, 0);

        Assert.Equal(expected, LineOfSight.IsAboveHorizon(plane, target));
    }

    [Fact]
    public void Ground_distance_prague_to_brno_is_about_185_km()
    {
        var brno = new GeoPoint(49.1951, 16.6068);

        Assert.Equal(185_000, LineOfSight.GroundDistance(Prague, brno), 3_000.0);
    }
}
