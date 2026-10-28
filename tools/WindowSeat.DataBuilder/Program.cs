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
// Použití: dotnet run --project tools/WindowSeat.DataBuilder -- <region> <šířka> <délka> <poloměr km> <výstup.json> [min. článků] [vrstvy]
//   vrstvy: čárkami oddělené druhy (Peak,Tower,Castle…); bez nich všechny. Pro „okolí“ s velkým poloměrem
//   se hodí jen to, co je vidět zdaleka.

if (args is ["water", var waterOutput, ..])
{
    // Vodní plochy z OpenStreetMap: water <výstup.json> [jih západ sever východ], výchozí Praha.
    var box = args.Length >= 6 ? args[2..6].Select(Parse).ToArray() : [49.96, 14.30, 50.16, 14.48];
    using var waterHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
    waterHttp.DefaultRequestHeaders.UserAgent.ParseAdd("WindowSeat-DataBuilder/0.1 (https://martinhajek.dev)");
    await Water.BuildAsync(waterHttp, waterOutput, box);
    return 0;
}

if (args.Length < 5)
{
    Console.Error.WriteLine("Použití: <region> <šířka> <délka> <poloměr km> <výstup.json> [min. článků = 3] [vrstvy = všechny]");
    return 1;
}

var region = args[0];
var center = new GeoPoint(Parse(args[1]), Parse(args[2]));
var radiusKm = Parse(args[3]);
var output = args[4];
var minLinks = args.Length > 5 ? int.Parse(args[5], CultureInfo.InvariantCulture) : 3;
var kinds = args.Length > 6
    ? args[6].Split(',').Select(Enum.Parse<PlaceKind>).ToHashSet()
    : Enum.GetValues<PlaceKind>().ToHashSet();

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("WindowSeat-DataBuilder/0.1 (https://martinhajek.dev)");

Console.WriteLine($"Wikidata: {region}, {radiusKm} km kolem {center.LatitudeDegrees}, {center.LongitudeDegrees}…");
var candidates = await Wikidata.QueryAsync(http, center, radiusKm, minLinks, kinds);
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
internal sealed record Candidate(
    string Name,
    GeoPoint Location,
    PlaceKind Kind,
    int Importance,
    double? HeightMeters,
    double? ElevationMeters,
    string? Description,
    string? WikipediaUrl)
{
    public Place ToPlace(double? groundMeters)
    {
        // Vrcholy mají nadmořskou výšku přímo; u staveb míříme kousek pod vršek, kde je stavba nejvíc vidět.
        var altitude = Kind == PlaceKind.Peak && ElevationMeters is { } summit
            ? summit
            : (groundMeters ?? ElevationMeters ?? 0) + AimHeight();

        return new Place(Name, Location with { AltitudeMeters = Math.Round(altitude) }, Kind, Importance, Description, WikipediaUrl);
    }

    // Bez známé výšky stavby odhad podle druhu: věže a kostely jsou vidět hlavně nahoře.
    private double AimHeight() => HeightMeters is { } height
        ? 0.8 * height
        : Kind switch
        {
            PlaceKind.Peak => 0,
            PlaceKind.Bridge => 8,
            PlaceKind.Tower => 25,
            PlaceKind.Church => 25,
            _ => 15,
        };
}

internal static class Wikidata
{
    // Druhy, které z dálky nejsou vidět nebo nemají jasný bod, kam mířit.
    // Náměstí, hřbitovy, hrobky, zdi, ulice, zahrady, parky, sochy, pamětní desky, letiště, stanice,
    // dálnice a silnice, kanály, lanovky.
    private const string Excluded = "wd:Q174782 wd:Q39614 wd:Q381885 wd:Q42948 wd:Q79007 wd:Q1107656 wd:Q22698 wd:Q179700 wd:Q860861 wd:Q721747 wd:Q1248784 wd:Q644371 wd:Q928830 wd:Q2175765 wd:Q46622 wd:Q34442 wd:Q12284 wd:Q142031";

    // Vrstvy podle toho, co je z dálky vidět; pořadí řádků = přednost, když položka patří do víc tříd
    // (rozhledna je i budova, katedrála i kostel). Obecná „budova“ jen pro hodně známé stavby.
    private static readonly (string Class, PlaceKind Kind, int Rank)[] Layers =
    [
        ("Q12280", PlaceKind.Bridge, 1),
        ("Q8502", PlaceKind.Peak, 2), ("Q54050", PlaceKind.Peak, 2),
        ("Q12518", PlaceKind.Tower, 3), ("Q1440300", PlaceKind.Tower, 3), ("Q11166728", PlaceKind.Tower, 3), ("Q18142", PlaceKind.Tower, 3),
        ("Q16970", PlaceKind.Church, 4), ("Q2977", PlaceKind.Church, 4), ("Q34627", PlaceKind.Church, 4), ("Q44613", PlaceKind.Church, 4), ("Q32815", PlaceKind.Church, 4),
        ("Q23413", PlaceKind.Castle, 5), ("Q16560", PlaceKind.Castle, 5), ("Q751876", PlaceKind.Castle, 5), ("Q57821", PlaceKind.Castle, 5),
        ("Q41176", PlaceKind.Building, 9),
    ];

    private const int MinBuildingLinks = 8;

    /// <summary>
    /// Místa vybraných vrstev v okruhu. Každá vrstva má vlastní dotaz; jeden dotaz přes všechny typy a jejich
    /// podtypy služba nestihne. Velký okruh nejde přes službu „around“ (vrátila by statisíce položek),
    /// ale přes známá místa v Česku, vzdálenost se dofiltruje až tady.
    /// </summary>
    /// <param name="kinds">Jen tyto vrstvy.</param>
    public static async Task<List<Candidate>> QueryAsync(HttpClient http, GeoPoint center, double radiusKm, int minLinks, IReadOnlySet<PlaceKind> kinds)
    {
        // Krok A: jen položka, souřadnice, počet článků a vrstva. Každá vrstva zvlášť, jinak to služba nestihne.
        var found = new Dictionary<string, (GeoPoint Location, int Links, PlaceKind Kind, int Rank)>();
        foreach (var layer in Layers.Where(l => kinds.Contains(l.Kind)).GroupBy(l => l.Kind))
        {
            var values = string.Join(' ', layer.Select(l => $"(wd:{l.Class} \"{l.Kind}\" {l.Rank})"));
            using var json = await SparqlAsync(http, FindQuery(center, radiusKm, values, minLinks));
            var rows = Bindings(json).ToList();

            foreach (var row in rows)
            {
                var item = Value(row, "item")!;
                var rank = (int)Number(row, "rank")!.Value;
                if (found.TryGetValue(item, out var existing) && existing.Rank <= rank)
                    continue;

                found[item] = (ParsePoint(Value(row, "coord")!), int.Parse(Value(row, "links")!, CultureInfo.InvariantCulture),
                    Enum.Parse<PlaceKind>(Value(row, "kind")!), rank);
            }

            Console.WriteLine($"  {layer.Key}: {rows.Count} řádků");
        }

        var inRange = found
            .Where(f => LineOfSight.GroundDistance(center, f.Value.Location) <= radiusKm * 1000)
            .Where(f => f.Value.Kind != PlaceKind.Building || f.Value.Links >= MinBuildingLinks)
            .ToList();

        // Krok B: podrobnosti a vyřazení jen pro nalezená místa (stovky, ne statisíce).
        var candidates = new List<Candidate>();
        foreach (var chunk in inRange.Chunk(150))
        {
            using var json = await SparqlAsync(http, DetailsQuery(chunk.Select(c => c.Key)));
            foreach (var rows in Bindings(json).GroupBy(row => Value(row, "item")!))
            {
                var (location, links, kind, _) = found[rows.Key];

                // Zbořené a zaniklé stavby (Juditin most…) mají souřadnice taky, ale vidět nejsou.
                // Mosty bývají zároveň „ulice“; vyřazený druh proto neplatí pro most.
                if (rows.Any(r => Value(r, "gone") is not null)
                    || (kind != PlaceKind.Bridge && rows.Any(r => Value(r, "excluded") is not null)))
                    continue;

                var first = rows.First();
                var name = Value(first, "cs") ?? Value(first, "en") ?? rows.Key[(rows.Key.LastIndexOf('/') + 1)..];
                candidates.Add(new Candidate(
                    PlaceNames.Shorten(name),
                    location,
                    kind,
                    links,
                    rows.Select(r => Number(r, "height")).Max(),
                    rows.Select(r => Number(r, "elevation")).Max(),
                    Value(first, "description"),
                    Value(first, "csWiki") ?? Value(first, "enWiki")));
            }
        }

        return candidates;
    }

    private static string FindQuery(GeoPoint center, double radiusKm, string values, int minLinks)
    {
        var point = FormattableString.Invariant($"Point({center.LongitudeDegrees} {center.LatitudeDegrees})");
        var radius = radiusKm.ToString(CultureInfo.InvariantCulture);

        // Malý okruh přes službu „around“. Velký okruh přes známá místa v Česku; „around“ by vrátil statisíce
        // položek. Pořadí: nejdřív podtypy, pak místa toho typu, jinak je to několikrát pomalejší.
        return radiusKm <= 30
            ? $$"""
                SELECT ?item ?coord ?links ?kind ?rank WHERE {
                  SERVICE wikibase:around {
                    ?item wdt:P625 ?coord .
                    bd:serviceParam wikibase:center "{{point}}"^^geo:wktLiteral ; wikibase:radius "{{radius}}" .
                  }
                  ?item wikibase:sitelinks ?links . FILTER(?links >= {{minLinks}})
                  VALUES (?class ?kind ?rank) { {{values}} }
                  ?item wdt:P31/wdt:P279* ?class .
                }
                """
            : $$"""
                SELECT ?item ?coord ?links ?kind ?rank WHERE {
                  VALUES (?class ?kind ?rank) { {{values}} }
                  ?type wdt:P279* ?class .
                  ?item wdt:P31 ?type ; wdt:P17 wd:Q213 ; wdt:P625 ?coord ; wikibase:sitelinks ?links .
                  FILTER(?links >= {{minLinks}})
                }
                """;
    }

    private static string DetailsQuery(IEnumerable<string> items) => $$"""
        SELECT ?item ?cs ?en ?description ?csWiki ?enWiki ?height ?elevation ?gone ?excluded WHERE {
          VALUES ?item { {{string.Join(' ', items.Select(i => $"wd:{i[(i.LastIndexOf('/') + 1)..]}"))}} }
          OPTIONAL { ?item rdfs:label ?cs FILTER(lang(?cs) = "cs") }
          OPTIONAL { ?item rdfs:label ?en FILTER(lang(?en) = "en") }
          OPTIONAL { ?item schema:description ?description FILTER(lang(?description) = "cs") }
          OPTIONAL { ?csWiki schema:about ?item ; schema:isPartOf <https://cs.wikipedia.org/> . }
          OPTIONAL { ?enWiki schema:about ?item ; schema:isPartOf <https://en.wikipedia.org/> . }
          OPTIONAL { ?item wdt:P2048 ?height }
          OPTIONAL { ?item wdt:P2044 ?elevation }
          OPTIONAL { ?item wdt:P576 ?gone }
          OPTIONAL { ?item wdt:P31 ?gone . ?gone wdt:P279* wd:Q15893266 . }
          OPTIONAL { ?item wdt:P31 ?gone . FILTER(?gone = wd:Q11486300) }
          OPTIONAL { VALUES ?excluded { {{Excluded}} } ?item wdt:P31 ?excluded . }
        }
        """;

    private static IEnumerable<JsonElement> Bindings(JsonDocument json) =>
        json.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray();

    // Dlouhé dotazy se do adresy nemusí vejít, proto POST.
    private static async Task<JsonDocument> SparqlAsync(HttpClient http, string sparql)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://query.wikidata.org/sparql")
        {
            Content = new FormUrlEncodedContent([new("query", sparql), new("format", "json")]),
        };
        request.Headers.Accept.ParseAdd("application/sparql-results+json");

        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
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

/// <summary>
/// Plochy řek z OpenStreetMap (natural=water + water=river a starší waterway=riverbank). Mnohoúhelníky
/// přicházejí po kouscích cest, které je potřeba sešít do kruhů; pak se zjednoduší a doplní výška hladiny.
/// </summary>
internal static class Water
{
    private const double SimplifyMeters = 5;

    public static async Task BuildAsync(HttpClient http, string output, double[] box)
    {
        var bbox = FormattableString.Invariant($"{box[0]},{box[1]},{box[2]},{box[3]}");
        var query = $"""
            [out:json][timeout:120];
            ( relation["natural"="water"]["water"="river"]({bbox});
              way["natural"="water"]["water"="river"]({bbox});
              way["waterway"="riverbank"]({bbox}); );
            out geom;
            """;

        Console.WriteLine("OpenStreetMap: plochy řek…");
        using var json = await OverpassAsync(http, query);

        var areas = new List<(string? Name, List<List<GeoPoint>> Rings)>();
        foreach (var element in json.RootElement.GetProperty("elements").EnumerateArray())
        {
            var name = element.TryGetProperty("tags", out var tags) && tags.TryGetProperty("name", out var n) ? n.GetString() : null;
            var pieces = element.GetProperty("type").GetString() == "way"
                ? [Geometry(element)]
                : element.GetProperty("members").EnumerateArray()
                    .Where(m => m.GetProperty("type").GetString() == "way" && m.TryGetProperty("geometry", out _))
                    .Select(Geometry)
                    .ToList();

            var rings = Stitch(pieces)
                .Select(r => Polyline.Simplify(r, SimplifyMeters).ToList())
                .Where(r => r.Count >= 3)
                .ToList();
            if (rings.Count > 0)
                areas.Add((name, rings));
        }

        var points = areas.SelectMany(a => a.Rings).Sum(r => r.Count);
        Console.WriteLine($"  {areas.Count} ploch, {points} bodů po zjednodušení na {SimplifyMeters} m");

        // Výška hladiny z terénu; u břehu a pod mosty je model zašuměný, a tak ji v kruhu vyhladíme mediánem.
        var all = areas.SelectMany(a => a.Rings).SelectMany(r => r).ToList();
        var heights = await Terrain.ElevationsAsync(http, all);
        var index = 0;
        var catalog = new WaterCatalog(
            "Vodní plochy: © přispěvatelé OpenStreetMap (ODbL). Výška hladiny: EU-DEM v1.1 (Copernicus).",
            areas.Select(a => new WaterArea(a.Name, a.Rings.Select(r =>
            {
                var raw = r.Select(_ => heights[index++] ?? 190).ToList();
                return (IReadOnlyList<GeoPoint>)r.Select((p, i) => p with { AltitudeMeters = Median(raw, i, 7) }).ToList();
            }).ToList())).ToList());

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await using (var file = File.Create(output))
            await catalog.WriteAsync(file);

        Console.WriteLine($"Uloženo do {output} ({new FileInfo(output).Length / 1024} kB)");
    }

    // Veřejné servery Overpass bývají přetížené; zkusíme hlavní a pak zrcadla, každý dvakrát.
    private static readonly string[] Servers =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
    ];

    private static async Task<JsonDocument> OverpassAsync(HttpClient http, string query)
    {
        foreach (var attempt in Enumerable.Range(0, 2))
        {
            foreach (var server in Servers)
            {
                using var response = await http.PostAsync(server, new FormUrlEncodedContent([new("data", query)]));
                if (response.IsSuccessStatusCode)
                    return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

                Console.WriteLine($"  {new Uri(server).Host}: {(int)response.StatusCode}, zkouším další");
            }

            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        throw new HttpRequestException("Žádný server Overpass neodpověděl.");
    }

    private static List<GeoPoint> Geometry(JsonElement way) => way.GetProperty("geometry").EnumerateArray()
        .Select(g => new GeoPoint(g.GetProperty("lat").GetDouble(), g.GetProperty("lon").GetDouble()))
        .ToList();

    /// <summary>Sešije kousky cest do uzavřených kruhů podle shodných koncových bodů (i obráceně orientovaných).</summary>
    private static List<List<GeoPoint>> Stitch(List<List<GeoPoint>> pieces)
    {
        var open = pieces.Where(p => p.Count > 1).Select(p => p.ToList()).ToList();
        var rings = new List<List<GeoPoint>>();

        while (open.Count > 0)
        {
            var ring = open[0];
            open.RemoveAt(0);

            while (!Same(ring[0], ring[^1]))
            {
                var nextIndex = open.FindIndex(p => Same(p[0], ring[^1]) || Same(p[^1], ring[^1]));
                if (nextIndex < 0)
                    break;

                var next = open[nextIndex];
                open.RemoveAt(nextIndex);
                if (!Same(next[0], ring[^1]))
                    next.Reverse();
                ring.AddRange(next.Skip(1));
            }

            if (Same(ring[0], ring[^1]))
                ring.RemoveAt(ring.Count - 1);
            rings.Add(ring);
        }

        return rings;
    }

    private static bool Same(GeoPoint a, GeoPoint b) =>
        Math.Abs(a.LatitudeDegrees - b.LatitudeDegrees) < 1e-7 && Math.Abs(a.LongitudeDegrees - b.LongitudeDegrees) < 1e-7;

    private static double Median(List<double> values, int at, int window)
    {
        var half = window / 2;
        var around = Enumerable.Range(at - half, window).Select(i => values[((i % values.Count) + values.Count) % values.Count]).Order().ToList();
        return around[around.Count / 2];
    }
}
