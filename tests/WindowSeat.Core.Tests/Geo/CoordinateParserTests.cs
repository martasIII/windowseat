using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Tests.Geo;

public class CoordinateParserTests
{
    [Theory]
    [InlineData("50.0786189N, 14.3194294E", 50.0786189, 14.3194294)]  // Mapy.cz
    [InlineData("50.0786, 14.3194", 50.0786, 14.3194)]                 // Google
    [InlineData("  50.0786   14.3194 ", 50.0786, 14.3194)]
    [InlineData("50.0786° N, 14.3194° E", 50.0786, 14.3194)]
    [InlineData("33.8688S, 151.2093E", -33.8688, 151.2093)]
    [InlineData("40.7128N, 74.0060W", 40.7128, -74.006)]
    [InlineData("-33.8688, 151.2093", -33.8688, 151.2093)]
    public void Reads_coordinates_copied_from_maps(string text, double latitude, double longitude)
    {
        Assert.True(CoordinateParser.TryParse(text, out var lat, out var lon));
        Assert.Equal(latitude, lat, 1e-9);
        Assert.Equal(longitude, lon, 1e-9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Praha 6")]
    [InlineData("50.0786")]
    [InlineData("50.07, 14.31, 300")]
    [InlineData("95.0, 14.3")]
    public void Rejects_anything_else(string text)
    {
        Assert.False(CoordinateParser.TryParse(text, out _, out _));
    }
}
