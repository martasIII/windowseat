using System.Text.Json;
using System.Text.Json.Serialization;
using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

/// <summary>
/// Offline balík míst pro jednu oblast, jak ho vyrobí <c>WindowSeat.DataBuilder</c> a appka nosí s sebou.
/// </summary>
public sealed record PlaceCatalog(string Region, DateTimeOffset Generated, string Attribution, IReadOnlyList<Place> Places)
{
    public static async Task<PlaceCatalog> ReadAsync(Stream json, CancellationToken token = default)
    {
        var file = await JsonSerializer.DeserializeAsync(json, PlaceCatalogJson.Default.CatalogFile, token)
            ?? throw new InvalidDataException("Prázdný katalog míst.");

        var places = file.Places
            .Select(p => new Place(p.Name, new GeoPoint(p.Lat, p.Lon, p.Alt), p.Kind, p.Importance, p.Description))
            .ToList();

        return new PlaceCatalog(file.Region, file.Generated, file.Attribution, places);
    }

    public async Task WriteAsync(Stream json, CancellationToken token = default)
    {
        var file = new CatalogFile(
            Region,
            Generated,
            Attribution,
            Places.Select(p => new PlaceEntry(
                p.Name,
                Math.Round(p.Location.LatitudeDegrees, 6),
                Math.Round(p.Location.LongitudeDegrees, 6),
                Math.Round(p.Location.AltitudeMeters),
                p.Kind,
                p.Importance,
                p.Description)).ToList());

        await JsonSerializer.SerializeAsync(json, file, PlaceCatalogJson.Default.CatalogFile, token);
    }

    /// <summary>
    /// Spojí katalog s dalšími místy; to, co v katalogu už je (stejné místo do 150 m), se nepřidá znovu.
    /// </summary>
    public IReadOnlyList<Place> MergedWith(IEnumerable<Place> extra) =>
    [
        .. Places,
        .. extra.Where(e => !Places.Any(p => LineOfSight.GroundDistance(p.Location, e.Location) < 150)),
    ];

    internal sealed record CatalogFile(string Region, DateTimeOffset Generated, string Attribution, List<PlaceEntry> Places);

    internal sealed record PlaceEntry(string Name, double Lat, double Lon, double Alt, PlaceKind Kind, int Importance, string? Description);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(PlaceCatalog.CatalogFile))]
internal sealed partial class PlaceCatalogJson : JsonSerializerContext;
