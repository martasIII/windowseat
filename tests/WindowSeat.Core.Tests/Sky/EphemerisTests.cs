using WindowSeat.Core.Geo;
using WindowSeat.Core.Sky;

namespace WindowSeat.Core.Tests.Sky;

public class EphemerisTests
{
    private static readonly GeoPoint Prague = new(50.0880, 14.4208, 250);

    [Fact]
    public void Sun_matches_meeus_example_25a()
    {
        // Meeus, Astronomical Algorithms, př. 25.a: 13. 10. 1992 0h TD → α = 198,380 83°, δ = −7,785 07°.
        var sun = Ephemeris.Equatorial(CelestialBody.Sun, new DateTimeOffset(1992, 10, 13, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(198.38083, sun.RightAscensionDegrees, 0.05);
        Assert.Equal(-7.78507, sun.DeclinationDegrees, 0.05);
    }

    [Fact]
    public void Moon_matches_meeus_example_47a()
    {
        // Meeus, př. 47.a: 12. 4. 1992 0h TD → α = 134,688 47°, δ = 13,768 37°, vzdálenost 368 409,7 km.
        var moon = Ephemeris.Equatorial(CelestialBody.Moon, new DateTimeOffset(1992, 4, 12, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(134.68847, moon.RightAscensionDegrees, 0.1);
        Assert.Equal(13.76837, moon.DeclinationDegrees, 0.1);
        Assert.Equal(368_409.7, moon.DistanceEarthRadii * 6378.137, 500.0);
    }

    [Fact]
    public void Midsummer_noon_sun_in_prague_is_due_south_and_high()
    {
        // Pravé poledne v Praze kolem 11:04 UT; výška = 90° − šířka + sklon ekliptiky ≈ 63,4°.
        var sun = Ephemeris.Observe(CelestialBody.Sun, Prague, new DateTimeOffset(2026, 6, 21, 11, 4, 0, TimeSpan.Zero));

        Assert.Equal(180, sun.AzimuthDegrees, 1.5);
        Assert.Equal(63.4, sun.ElevationDegrees, 0.3);
    }

    [Fact]
    public void Equinox_sun_rises_in_the_east_and_sets_in_the_west()
    {
        var morning = Ephemeris.Observe(CelestialBody.Sun, Prague, new DateTimeOffset(2026, 3, 20, 5, 30, 0, TimeSpan.Zero));
        var evening = Ephemeris.Observe(CelestialBody.Sun, Prague, new DateTimeOffset(2026, 3, 20, 16, 30, 0, TimeSpan.Zero));

        Assert.InRange(morning.AzimuthDegrees, 80, 100);
        Assert.InRange(evening.AzimuthDegrees, 260, 280);
        Assert.InRange(morning.ElevationDegrees, -3, 8);
        Assert.InRange(evening.ElevationDegrees, -3, 8);
    }
}
