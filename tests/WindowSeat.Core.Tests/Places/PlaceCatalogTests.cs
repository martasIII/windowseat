using WindowSeat.Core.Geo;
using WindowSeat.Core.Places;

namespace WindowSeat.Core.Tests.Places;

public class PlaceCatalogTests
{
    private static readonly PlaceCatalog Prague = new(
        "Praha",
        new DateTimeOffset(2026, 10, 28, 20, 0, 0, TimeSpan.Zero),
        "Wikidata (CC0)",
        [
            new Place("Karlův most", new GeoPoint(50.086389, 14.411944, 195), PlaceKind.Bridge, 62),
            new Place("Petřín", new GeoPoint(50.0814, 14.3953, 327), PlaceKind.Peak, 28),
        ]);

    [Fact]
    public async Task Catalog_survives_a_round_trip_through_json()
    {
        using var stream = new MemoryStream();
        await Prague.WriteAsync(stream);
        stream.Position = 0;

        var read = await PlaceCatalog.ReadAsync(stream);

        Assert.Equal("Praha", read.Region);
        Assert.Equal(Prague.Places, read.Places);
    }

    [Fact]
    public async Task Json_uses_readable_kind_names()
    {
        using var stream = new MemoryStream();
        await Prague.WriteAsync(stream);

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"kind\":\"Bridge\"", json);
    }

    [Fact]
    public void Merge_skips_places_the_catalog_already_has()
    {
        Place[] extra =
        [
            new("Karlův most (ručně)", new GeoPoint(50.0865, 14.4120, 195), PlaceKind.Bridge),
            new("Sněžka", new GeoPoint(50.7360, 15.7398, 1603), PlaceKind.Peak),
        ];

        var merged = Prague.MergedWith(extra);

        Assert.Equal(["Karlův most", "Petřín", "Sněžka"], merged.Select(p => p.Name));
    }
}
