using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Places;

// Vyrobí offline balík míst pro jednu oblast:
//   1. z Wikidat vybere stavby, mosty a vrcholy v okruhu kolem středu, s aspoň N články na Wikipediích,
//   2. doplní výšku terénu z EU-DEM (OpenTopoData) a k ní výšku stavby, kam má popisek mířit,
//   3. uloží JSON, který appka nosí v Resources/Raw/places.
//
// Použití: dotnet run --project tools/WindowSeat.DataBuilder -- <region> <šířka> <délka> <poloměr km> <výstup.json> [min. článků]

if (args.Length < 5)
{
    Console.Error.WriteLine("Použití: <region> <šířka> <délka> <poloměr km> <výstup.json> [min. článků = 3]");
    return 1;
}

var region = args[0];
var center = new GeoPoint(Parse(args[1]), Parse(args[2]));
var radiusKm = Parse(args[3]);
var output = args[4];
var minLinks = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 3;

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("WindowSeat-DataBuilder/0.1 (https://martinhajek.dev)");

Console.WriteLine($"Wikidata: {region}, {radiusKm} km kolem {center.LatitudeDegrees}, {center.LongitudeDegrees}…");
var candidates = await Wikidata.QueryAsync(http, center, radiusKm, minLinks);
Console.WriteLine($"  {candidates.Count} míst");

Console.WriteLine("Výška terénu z EU-DEM…");
var ground = await Terrain.ElevationsAsync(http, candidates.Select(c => c.Location).ToList());

var places = candidates
    .Select((c, i) => c.ToPlace(ground[i]))
    .OrderByDescending(p => p.Importance)
    .ToList();

var catalog = new PlaceCatalog(
    region,
    DateTimeOffset.UtcNow,
    "Místa: Wikidata (CC0). Terén: EU-DEM v1.1 přes OpenTopoData (Copernicus).",
    places);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await using (var file = File.Create(output))
    await catalog.WriteAsync(file);

Console.WriteLine($"Uloženo {places.Count} míst do {output} ({new FileInfo(output).Length / 1024} kB)");
return 0;

static double Parse(string value) => double.Parse(value, CultureInfo.InvariantCulture);

/// <summary>Místo z Wikidat, zatím bez výšky terénu.</summary>
internal sealed record Candidate(string Name, GeoPoint Location, PlaceKind Kind, int Importance, double? HeightMeters, double? ElevationMeters)
{
    public Place ToPlace(double? groundMeters)
    {
        // Vrcholy mají nadmořskou výšku přímo; u staveb míříme kousek pod vršek, kde je stavba nejvíc vidět.
        var altitude = Kind == PlaceKind.Peak && ElevationMeters is { } summit
            ? summit
            : (groundMeters ?? ElevationMeters ?? 0) + AimHeight();

        return new Place(Name, Location with { AltitudeMeters = Math.Round(altitude) }, Kind, Importance);
    }

    private double AimHeight() => HeightMeters is { } height
        ? 0.8 * height
        : Kind switch
        {
            PlaceKind.Bridge => 8,
            PlaceKind.Peak => 0,
            _ => 15,
        };
}

internal static class Wikidata
{
    // Druhy, které z dálky nejsou vidět nebo nemají jasný bod, kam mířit.
    // Náměstí, hřbitovy, hrobky, zdi, ulice, zahrady, parky, sochy, pamětní desky, letiště, stanice,
    // dálnice a silnice, kanály, lanovky.
    private const string Excluded = "wd:Q174782 wd:Q39614 wd:Q381885 wd:Q42948 wd:Q79007 wd:Q1107656 wd:Q22698 wd:Q179700 wd:Q860861 wd:Q721747 wd:Q1248784 wd:Q644371 wd:Q928830 wd:Q2175765 wd:Q46622 wd:Q34442 wd:Q12284 wd:Q142031";

    public static async Task<List<Candidate>> QueryAsync(HttpClient http, GeoPoint center, double radiusKm, int minLinks)
    {
        var point = FormattableString.Invariant($"Point({center.LongitudeDegrees} {center.LatitudeDegrees})");
        var radius = radiusKm.ToString(CultureInfo.InvariantCulture);

        var sparql = $$"""
            SELECT ?item ?cs ?en ?coord ?links ?kind ?height ?elevation WHERE {
              SERVICE wikibase:around {
                ?item wdt:P625 ?coord .
                bd:serviceParam wikibase:center "{{point}}"^^geo:wktLiteral ; wikibase:radius "{{radius}}" .
              }
              ?item wikibase:sitelinks ?links . FILTER(?links >= {{minLinks}})
              VALUES (?class ?kind) { (wd:Q12280 "Bridge") (wd:Q8502 "Peak") (wd:Q54050 "Peak") (wd:Q811979 "Landmark") }
              ?item wdt:P31/wdt:P279* ?class .
              # Zbořené a zaniklé stavby (Juditin most…) mají souřadnice taky, ale vidět nejsou.
              FILTER NOT EXISTS { ?item wdt:P576 ?demolished . }
              FILTER NOT EXISTS { ?item wdt:P31/wdt:P279* wd:Q15893266 . }
              FILTER NOT EXISTS { ?item wdt:P31 wd:Q11486300 . }
              # Mosty bývají zároveň „ulice“; vyřazujeme proto jen to, co mostem není.
              FILTER(?kind = "Bridge" || NOT EXISTS { VALUES ?excluded { {{Excluded}} } ?item wdt:P31 ?excluded . })
              OPTIONAL { ?item rdfs:label ?cs FILTER(lang(?cs) = "cs") }
              OPTIONAL { ?item rdfs:label ?en FILTER(lang(?en) = "en") }
              OPTIONAL { ?item wdt:P2048 ?height }
              OPTIONAL { ?item wdt:P2044 ?elevation }
            }
            """;

        var url = "https://query.wikidata.org/sparql?format=json&query=" + Uri.EscapeDataString(sparql);
        using var response = await http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        // Jedna položka přijde v několika řádcích (víc tříd, víc výšek); bereme nejkonkrétnější druh.
        return json.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray()
            .GroupBy(row => Value(row, "item")!)
            .Select(rows =>
            {
                var first = rows.First();
                var kinds = rows.Select(r => Enum.Parse<PlaceKind>(Value(r, "kind")!)).ToHashSet();
                var kind = kinds.Contains(PlaceKind.Bridge) ? PlaceKind.Bridge
                    : kinds.Contains(PlaceKind.Peak) ? PlaceKind.Peak
                    : PlaceKind.Landmark;

                var name = Value(first, "cs") ?? Value(first, "en") ?? rows.Key[(rows.Key.LastIndexOf('/') + 1)..];
                return new Candidate(
                    char.ToUpperInvariant(name[0]) + name[1..],
                    ParsePoint(Value(first, "coord")!),
                    kind,
                    int.Parse(Value(first, "links")!, CultureInfo.InvariantCulture),
                    rows.Select(r => Number(r, "height")).Max(),
                    rows.Select(r => Number(r, "elevation")).Max());
            })
            .ToList();
    }

    private static string? Value(JsonElement row, string name) =>
        row.TryGetProperty(name, out var cell) ? cell.GetProperty("value").GetString() : null;

    private static double? Number(JsonElement row, string name) =>
        Value(row, name) is { } text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    // WKT „Point(délka šířka)“.
    private static GeoPoint ParsePoint(string wkt)
    {
        var parts = wkt["Point(".Length..^1].Split(' ');
        return new GeoPoint(double.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[0], CultureInfo.InvariantCulture));
    }
}

internal static class Terrain
{
    // Veřejné API dovoluje 100 bodů na dotaz a jeden dotaz za sekundu.
    private const int BatchSize = 100;

    public static async Task<List<double?>> ElevationsAsync(HttpClient http, IReadOnlyList<GeoPoint> points)
    {
        var result = new List<double?>(points.Count);
        for (var start = 0; start < points.Count; start += BatchSize)
        {
            var batch = points.Skip(start).Take(BatchSize);
            var locations = string.Join('|', batch.Select(p => FormattableString.Invariant($"{p.LatitudeDegrees:0.######},{p.LongitudeDegrees:0.######}")));

            var response = await http.GetFromJsonAsync<TerrainResponse>(
                "https://api.opentopodata.org/v1/eudem25m?locations=" + Uri.EscapeDataString(locations));
            result.AddRange(response!.Results.Select(r => r.Elevation));

            Console.WriteLine($"  {result.Count}/{points.Count}");
            await Task.Delay(TimeSpan.FromSeconds(1.1));
        }

        return result;
    }

    private sealed record TerrainResponse(List<TerrainResult> Results);

    private sealed record TerrainResult(double? Elevation);
}
