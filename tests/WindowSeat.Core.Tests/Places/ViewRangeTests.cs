using WindowSeat.Core.Places;

namespace WindowSeat.Core.Tests.Places;

public class ViewRangeTests
{
    private static readonly Place Church = new("Kostel", default, PlaceKind.Church);
    private static readonly Place Mountain = new("Milešovka", default, PlaceKind.Peak);
    private static readonly Place Moon = new("Měsíc", default, PlaceKind.CelestialBody);

    [Theory]
    [InlineData(9_000, true)]
    [InlineData(11_000, false)]
    public void Buildings_are_shown_within_the_range(double distance, bool expected)
    {
        Assert.Equal(expected, ViewRange.IsWithin(Church, distance, rangeKm: 10));
    }

    [Theory]
    [InlineData(19_000, true)]
    [InlineData(60_000, false)]
    public void Peaks_reach_twice_as_far(double distance, bool expected)
    {
        Assert.Equal(expected, ViewRange.IsWithin(Mountain, distance, rangeKm: 10));
    }

    [Fact]
    public void Sky_has_no_range()
    {
        Assert.True(ViewRange.IsWithin(Moon, 384_000_000, rangeKm: 5));
    }
}
