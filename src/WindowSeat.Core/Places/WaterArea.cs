using System.Text.Json;
using System.Text.Json.Serialization;
using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

/// <summary>
/// Vodní plocha jako mnohoúhelník: vnější obrysy i ostrovy jako samostatné kruhy, kreslí se pravidlem sudé–liché
/// (ostrov uvnitř řeky zůstane prázdný). Body mají výšku hladiny.
/// </summary>
public sealed record WaterArea(string? Name, IReadOnlyList<IReadOnlyList<GeoPoint>> Rings);

/// <summary>Offline balík vodních ploch, jak ho vyrobí <c>WindowSeat.DataBuilder</c>.</summary>
public sealed record WaterCatalog(string Attribution, IReadOnlyList<WaterArea> Areas)
{
    public static async Task<WaterCatalog> ReadAsync(Stream json, CancellationToken token = default)
    {
        var file = await JsonSerializer.DeserializeAsync(json, WaterCatalogJson.Default.WaterFile, token)
            ?? throw new InvalidDataException("Prázdný katalog vod.");

        return new WaterCatalog(
            file.Attribution,
            file.Areas.Select(a => new WaterArea(
                a.Name,
                a.Rings.Select(r => (IReadOnlyList<GeoPoint>)r.Select(p => new GeoPoint(p[0], p[1], p[2])).ToList()).ToList())).ToList());
    }

    public async Task WriteAsync(Stream json, CancellationToken token = default)
    {
        var file = new WaterFile(
            Attribution,
            Areas.Select(a => new WaterEntry(
                a.Name,
                a.Rings.Select(r => r.Select(p => new[]
                {
                    Math.Round(p.LatitudeDegrees, 6),
                    Math.Round(p.LongitudeDegrees, 6),
                    Math.Round(p.AltitudeMeters, 1),
                }).ToList()).ToList())).ToList());

        await JsonSerializer.SerializeAsync(json, file, WaterCatalogJson.Default.WaterFile, token);
    }

    internal sealed record WaterFile(string Attribution, List<WaterEntry> Areas);

    /// <param name="Rings">Kruhy jako seznamy [šířka, délka, výška].</param>
    internal sealed record WaterEntry(string? Name, List<List<double[]>> Rings);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WaterCatalog.WaterFile))]
internal sealed partial class WaterCatalogJson : JsonSerializerContext;
