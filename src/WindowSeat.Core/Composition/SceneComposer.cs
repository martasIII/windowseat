using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;
using WindowSeat.Core.Sky;

namespace WindowSeat.Core.Composition;

/// <summary>
/// Skládá scénu popisků pro jeden snímek: které ikonky, kde a jak velké, co je v zaměřovacím kroužku, rozbalený
/// vějíř shluku, trvalé názvy, řeka a mosty. Nekreslí; výsledek (<see cref="Scene"/>) vykreslí appka i prohlížeč
/// každý po svém. Pamatuje si stav mezi snímky (zaměření s hysterezí, rozbalený vějíř).
/// </summary>
public sealed class SceneComposer
{
    // Chytí cíl v kroužku, pustí ho až o kus dál; detail po 0,6 s.
    private readonly FocusTracker<Place> _focus = new(SceneStyle.RingRadius, SceneStyle.RingRadius * 1.8, TimeSpan.FromMilliseconds(600));

    private IReadOnlyList<Place> _places = [];
    private IReadOnlyList<WaterArea> _water = [];
    private IReadOnlyList<WaterArea> _bridges = [];
    private GeoPoint? _observer;
    private double _rangeKm = ViewRange.DefaultKm;
    private Place? _expanded;

    // Směry k místům a vrcholům ploch se počítají jen při změně polohy, ne v každém snímku.
    private List<(Place Place, LineOfSight Los)> _sightings = [];
    private List<(string? Name, List<LineOfSight[]> Rings)> _waterSightings = [];
    private List<(string? Name, List<LineOfSight[]> Rings)> _bridgeSightings = [];

    private static readonly (Place Place, CelestialBody Body)[] SkyPlaces =
    [
        (new Place("Slunce", default, PlaceKind.CelestialBody), CelestialBody.Sun),
        (new Place("Měsíc", default, PlaceKind.CelestialBody), CelestialBody.Moon),
    ];

    public IReadOnlyList<Place> Places
    {
        get => _places;
        set
        {
            _places = value;
            _sightings = Sightings(_observer);
        }
    }

    public IReadOnlyList<WaterArea> Water
    {
        get => _water;
        set
        {
            _water = value;
            _waterSightings = AreaSightings(value, _observer);
        }
    }

    public IReadOnlyList<WaterArea> Bridges
    {
        get => _bridges;
        set
        {
            _bridges = value;
            _bridgeSightings = AreaSightings(value, _observer);
        }
    }

    public GeoPoint? Observer
    {
        get => _observer;
        set
        {
            if (_observer == value)
                return;

            _observer = value;
            _sightings = Sightings(value);
            _waterSightings = AreaSightings(_water, value);
            _bridgeSightings = AreaSightings(_bridges, value);
        }
    }

    /// <summary>Do jaké vzdálenosti ukazovat místa (km), vrcholy dvojnásobně.</summary>
    public double RangeKm
    {
        get => _rangeKm;
        set
        {
            _rangeKm = value;
            _sightings = Sightings(_observer);
        }
    }

    public LayerWeights Layers { get; set; } = LayerWeights.Default;

    public bool ShowWater { get; set; } = true;

    /// <summary>Přišpendlené místo (klepnutím nebo kalibrací); drží zaměření místo kroužku.</summary>
    public Place? Highlighted { get; set; }

    /// <summary>
    /// Složí scénu.
    /// </summary>
    /// <param name="horizontalFieldOfViewDegrees">Zorný úhel přes šířku obrazovky (už po přiblížení).</param>
    /// <param name="textWidth">Šířka textu v daném písmu a velikosti; skládání ji samo změřit neumí.</param>
    public Scene Compose(
        CameraPose pose,
        double width,
        double height,
        double horizontalFieldOfViewDegrees,
        DateTimeOffset now,
        Func<string, double, double> textWidth)
    {
        // Svislý úhel z poměru stran, pixely náhledu jsou čtvercové.
        var halfHorizontal = horizontalFieldOfViewDegrees * Math.PI / 360;
        var verticalDegrees = 2 * Math.Atan(Math.Tan(halfHorizontal) * height / width) * 180 / Math.PI;
        var projector = new ScreenProjector(horizontalFieldOfViewDegrees, verticalDegrees);
        var centre = new ScreenPoint(width / 2, height / 2);

        var candidates = new List<IconDeclutter.Item<(Place Place, LineOfSight Los)>>();
        foreach (var (place, los) in _sightings.Concat(SkySightings(now)))
        {
            if (!Layers.IsVisible(place.Kind) && place != Highlighted)
                continue;

            // Přišpendlené místo musí zůstat vidět, i když je v hustém shluku.
            var score = place == Highlighted ? double.MaxValue : Layers.Score(place, los.DistanceMeters);
            if (projector.Project(pose, los, width, height) is { } point)
                candidates.Add(new((place, los), point, score));
        }

        var markers = IconDeclutter.Arrange(candidates, SceneStyle.MinIconDistance, SceneStyle.MaxIcons)
            .Select(s => new Marker(s.Value.Place, s.Value.Los, s.Point, s.Score, s.Members))
            .ToList();
        ExpandFan(markers);

        var (focused, acquired, dwelled) = UpdateFocus(markers, centre, now);
        UpdateFan(markers, focused, centre);

        var focusedPlace = focused?.Place;
        var water = ShowWater ? Polygons(_waterSightings, pose, projector, width, height, highlightedName: null) : [];
        var bridges = ShowWater
            ? Polygons(_bridgeSightings, pose, projector, width, height, focusedPlace is { Kind: PlaceKind.Bridge } ? focusedPlace.Name : null)
            : [];

        var sceneMarkers = markers
            .OrderBy(m => m.InFan).ThenBy(m => m.Score)
            .Select(m => ToSceneMarker(m, m == focused))
            .ToList();

        return new Scene(
            projector,
            width,
            height,
            water,
            bridges,
            sceneMarkers,
            PermanentLabels(markers, focused, centre, width, textWidth),
            focused is null ? null : Focus(focused, width, textWidth),
            acquired,
            dwelled);
    }

    /// <summary>
    /// Rozbalený shluk: schované ikonky se rozestoupí do kruhu kolem hlavní, každá s tenkou čarou ke skutečné poloze.
    /// Rozbalení vydrží, dokud kroužek neodjede od shluku dál, než sahá vějíř.
    /// </summary>
    private void ExpandFan(List<Marker> markers)
    {
        if (_expanded is null)
            return;

        var index = markers.FindIndex(m => m.Place == _expanded);
        if (index < 0)
        {
            _expanded = null;
            return;
        }

        var head = markers[index];
        var members = head.Members.Take(SceneStyle.MaxFanSize).ToList();
        var offsets = FanLayout.Offsets(members.Count, SceneStyle.FanSpacing, SceneStyle.FanRadius);

        markers[index] = head with { Members = [] };
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var point = new ScreenPoint(head.Point.X + offsets[i].Dx, head.Point.Y + offsets[i].Dy);
            markers.Add(new Marker(member.Value.Place, member.Value.Los, point, member.Score, [], TruePoint: member.Point));
        }
    }

    private void UpdateFan(List<Marker> markers, Marker? focused, ScreenPoint centre)
    {
        // Zaměřený shluk se rozbalí v dalším snímku.
        if (focused is { Hidden: > 0 } && _expanded is null)
        {
            _expanded = focused.Place;
            return;
        }

        if (_expanded is null || markers.FirstOrDefault(m => m.Place == _expanded) is not { } head)
            return;

        // Sbalí se, až kroužek odjede za vějíř a nemíří na nic z něj.
        var fanReach = markers.Where(m => m.InFan).Select(m => Distance(head.Point, m.Point)).DefaultIfEmpty(SceneStyle.FanRadius).Max();
        var focusInFan = focused is not null && (focused.InFan || focused.Place == _expanded);
        if (!focusInFan && Distance(head.Point, centre) > fanReach + 50)
            _expanded = null;
    }

    private (Marker? Focused, bool Acquired, bool Dwelled) UpdateFocus(List<Marker> markers, ScreenPoint centre, DateTimeOffset now)
    {
        // Přišpendlené místo má přednost před kroužkem.
        if (Highlighted is { } pinned)
        {
            _focus.Reset();
            return (markers.FirstOrDefault(m => m.Place == pinned), false, false);
        }

        var state = _focus.Update(markers.Select(m => (m.Place, Distance: Distance(m.Point, centre))), now);
        var focused = markers.FirstOrDefault(m => m.Place == state.Focused);
        return focused is null ? (null, false, false) : (focused, state.JustAcquired, state.JustDwelled);
    }

    private static SceneMarker ToSceneMarker(Marker marker, bool focused)
    {
        // Velikost jako perspektiva: základ podle známosti, zmenšený se vzdáleností (logaritmicky).
        // Katedrála 6 km ~22 bodů, Okoř 15 km ~14, Milešovka 60 km ~12, kostel za rohem ~18.
        var basis = 10 + 2.5 * Math.Log2(1 + marker.Place.Importance);
        var distanceKm = Math.Max(0.1, marker.Los.DistanceMeters / 1000);
        var perspective = Math.Clamp(1.15 - 0.35 * Math.Log10(distanceKm), 0.55, 1.2);
        var size = Math.Clamp(basis * perspective, 8, 26) * (focused ? 1.3 : 1);
        var opacity = focused ? 1 : Math.Clamp(1.2 - marker.Los.DistanceMeters / 25_000, 0.55, 1);

        return new SceneMarker(marker.Place, marker.Los, marker.Point, size, opacity, focused, marker.Hidden, marker.TruePoint);
    }

    /// <summary>Pár nejvýznamnějších míst v záběru má název trvale, pro orientaci.</summary>
    private static List<SceneLabel> PermanentLabels(List<Marker> markers, Marker? focused, ScreenPoint centre, double screenWidth, Func<string, double, double> textWidth)
    {
        // Kroužek a popisek pod ním jsou překážka, ať se trvalé popisky nepletou do zaměření.
        var ringArea = (Left: centre.X - 110, Top: centre.Y - SceneStyle.RingRadius - 10, Right: centre.X + 110, Bottom: centre.Y + SceneStyle.RingRadius + 60);

        var requests = markers
            .Where(m => m != focused && m.Place.Kind != PlaceKind.CelestialBody)
            .OrderByDescending(m => m.Score)
            .Take(SceneStyle.PermanentLabels)
            .Select(m => new LabelLayout.Request<Marker>(m, m.Point, m.Score, textWidth(m.Place.Name, SceneStyle.FontSize) + 8));

        return LabelLayout.Arrange(requests, 200, SceneStyle.LabelHeight, obstacles: [ringArea])
            .Select(placed =>
            {
                var box = LabelLayout.Box(placed.Anchor, placed.Level, placed.Width, SceneStyle.LabelHeight);
                var x = InsideScreen(placed.Anchor.X, placed.Width / 2, screenWidth);
                return new SceneLabel(
                    placed.Item.Place,
                    placed.Item.Los,
                    placed.Anchor,
                    placed.Item.Place.Name,
                    x,
                    box.Bottom - 5,
                    new ScreenRect(x - placed.Width / 2, box.Top, placed.Width, SceneStyle.LabelHeight));
            })
            .ToList();
    }

    private static SceneFocus Focus(Marker focused, double screenWidth, Func<string, double, double> textWidth)
    {
        var place = focused.Place;
        var detail = place.Kind == PlaceKind.CelestialBody
            ? $"{focused.Los.ElevationDegrees:0}° nad obzorem"
            : Distance(focused.Los);

        // Dlouhý název („Kostel Panny Marie Vítězné na Bílé Hoře“) zmenšíme, aby se vešel na šířku obrazovky.
        var fitWidth = screenWidth - 32;
        var nameWidth = textWidth(place.Name, SceneStyle.FocusFontSize);
        var nameSize = nameWidth > fitWidth
            ? Math.Max(SceneStyle.MinFocusFontSize, SceneStyle.FocusFontSize * fitWidth / nameWidth)
            : SceneStyle.FocusFontSize;

        return new SceneFocus(place, focused.Los, focused.Point, place.Name, detail, nameSize);
    }

    /// <summary>
    /// Posune popisek dovnitř obrazovky. Popisek širší než obrazovka (dlouhý název na úzkém telefonu) se dá
    /// doprostřed; obyčejný Math.Clamp by tu spadl, protože dolní mez je větší než horní.
    /// </summary>
    private static double InsideScreen(double x, double halfWidth, double screenWidth)
    {
        var min = halfWidth + 4;
        var max = screenWidth - halfWidth - 4;
        return min > max ? screenWidth / 2 : Math.Clamp(x, min, max);
    }

    private static List<ScenePolygon> Polygons(
        List<(string? Name, List<LineOfSight[]> Rings)> sightings,
        CameraPose pose,
        ScreenProjector projector,
        double width,
        double height,
        string? highlightedName)
    {
        var polygons = new List<ScenePolygon>();
        foreach (var (name, rings) in sightings)
        {
            var projected = rings
                .Select(ring => projector.ProjectPolygon(pose, ring, width, height))
                .Where(points => points.Count >= 3)
                .ToList();
            if (projected.Count == 0)
                continue;

            var highlighted = highlightedName is not null && string.Equals(name, highlightedName, StringComparison.OrdinalIgnoreCase);
            polygons.Add(new ScenePolygon(name, projected, highlighted));
        }

        return polygons;
    }

    /// <summary>Slunce a Měsíc nad obzorem; slouží i jako kalibrační cíl.</summary>
    private IEnumerable<(Place, LineOfSight)> SkySightings(DateTimeOffset now)
    {
        if (_observer is not { } observer)
            yield break;

        foreach (var (place, body) in SkyPlaces)
        {
            var los = Ephemeris.Observe(body, observer, now);
            if (los.ElevationDegrees > -1)
                yield return (place, los);
        }
    }

    private List<(Place, LineOfSight)> Sightings(GeoPoint? observer) =>
        observer is not { } from
            ? []
            : _places
                .Where(p => LineOfSight.IsAboveHorizon(from, p.Location))
                .Select(p => (p, LineOfSight.Between(from, p.Location)))
                .Where(x => ViewRange.IsWithin(x.p, x.Item2.DistanceMeters, _rangeKm))
                .ToList();

    private static List<(string? Name, List<LineOfSight[]> Rings)> AreaSightings(IReadOnlyList<WaterArea> areas, GeoPoint? observer) =>
        observer is not { } from
            ? []
            : areas
                .Select(a => (a.Name, a.Rings.Select(r => r.Select(p => LineOfSight.Between(from, p)).ToArray()).ToList()))
                .ToList();

    private static string Distance(LineOfSight los) => los.DistanceMeters < 10_000
        ? $"{los.DistanceMeters / 1000:0.0} km"
        : $"{los.DistanceMeters / 1000:0} km";

    private static double Distance(ScreenPoint a, ScreenPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <param name="Members">Místa schovaná pod touto ikonkou (shluk).</param>
    /// <param name="TruePoint">U ikonky rozestoupené ve vějíři její skutečná poloha; jinak <c>null</c>.</param>
    private sealed record Marker(
        Place Place,
        LineOfSight Los,
        ScreenPoint Point,
        double Score,
        IReadOnlyList<IconDeclutter.Item<(Place Place, LineOfSight Los)>> Members,
        ScreenPoint? TruePoint = null)
    {
        public int Hidden => Members.Count;

        public bool InFan => TruePoint is not null;
    }
}
