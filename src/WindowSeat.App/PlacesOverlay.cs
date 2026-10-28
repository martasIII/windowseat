using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;
using WindowSeat.Core.Sky;

namespace WindowSeat.App;

/// <summary>
/// Kreslí místa přes náhled kamery jako ikonky podle vrstvy. Název ukáže jen u toho, na co míří zaměřovací kroužek
/// uprostřed, a u pár nejvýznamnějších míst pro orientaci. Písmo bez podkladu, černé s bílým obrysem.
/// </summary>
public sealed class PlacesOverlay(IReadOnlyList<Place> places) : IDrawable
{
    private const float FontSize = 15;
    private const float FocusFontSize = 20;
    private const float LabelHeight = 20;
    private const float RingRadius = 40;
    private const int PermanentLabels = 4;

    // Nejmenší vzdálenost dvou ikonek; bližší se schovají pod důležitější.
    private const float MinIconDistance = 26;

    // Vějíř: poloměr větší než okruh, kde zaměřovač pustí hlavní ikonku (1,8 × kroužek), aby šlo přejet na sousedy.
    private const double FanRadius = 85;
    private const double FanSpacing = 44;
    private const int MaxFanSize = 12;

    // Bílý obrys: text se nakreslí bíle posunutý do všech stran a černě navrch.
    private static readonly (float X, float Y)[] Halo =
    [
        (-1.6f, 0), (1.6f, 0), (0, -1.6f), (0, 1.6f),
        (-1.2f, -1.2f), (1.2f, -1.2f), (-1.2f, 1.2f), (1.2f, 1.2f),
    ];

    private static readonly Microsoft.Maui.Graphics.Font LabelFont = Microsoft.Maui.Graphics.Font.DefaultBold;

    private readonly List<DrawnLabel> _drawn = [];
    private readonly Dictionary<string, float> _textWidths = [];

    // Chytí cíl v kroužku, pustí ho až o kus dál; detail po 0,6 s.
    private readonly FocusTracker<Place> _focus = new(RingRadius, RingRadius * 1.8, TimeSpan.FromMilliseconds(600));

    private IReadOnlyList<Place> _places = places;
    private Place? _expanded;
    private double _rangeKm = ViewRange.DefaultKm;
    private GeoPoint? _observer;
    private List<(Place Place, LineOfSight Los)> _sightings = [];

    public IReadOnlyList<Place> Places
    {
        get => _places;
        set
        {
            _places = value;
            _sightings = Sightings(_observer);
        }
    }

    /// <summary>Poloha pozorovatele; směry k místům se přepočítají jen při její změně, ne v každém snímku.</summary>
    public GeoPoint? Observer
    {
        get => _observer;
        set
        {
            if (_observer == value)
                return;

            _observer = value;
            _sightings = Sightings(value);
        }
    }

    public CameraPose? Pose { get; set; }

    public LayerWeights Layers { get; set; } = LayerWeights.Default;

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

    /// <summary>Zorný úhel objektivu ze systému; z něj a rozměrů obrazovky se počítá úhel přes šířku.</summary>
    public LensFieldOfView? Lens { get; set; }

    /// <summary>Ručně nastavený úhel přes šířku obrazovky; má přednost před objektivem.</summary>
    public double? ManualHorizontalFieldOfViewDegrees { get; set; }

    /// <summary>Přiblížení fotoaparátu; zúží zorný úhel, takže se ikonky v hustém místě rozestoupí.</summary>
    public double Zoom { get; set; } = 1;

    /// <summary>Úhel přes šířku obrazovky pro dané rozměry: ruční, z objektivu, nebo odhad; vždy po přiblížení.</summary>
    public double HorizontalFieldOfViewDegrees(double width, double height) =>
        LensFieldOfView.Zoomed(
            ManualHorizontalFieldOfViewDegrees ?? Lens?.VisibleHorizontalDegrees(width, height) ?? 50,
            Zoom);

    /// <summary>Přišpendlené místo (klepnutím nebo kalibrací); drží zaměření místo kroužku.</summary>
    public Place? Highlighted { get; set; }

    /// <summary>Co je právě v kroužku (nebo přišpendlené), pro záznam relace.</summary>
    public Place? Focused { get; private set; }

    /// <summary>Cíl se chytil do kroužku (appka cvakne vibrací).</summary>
    public event Action<Place>? FocusAcquired;

    /// <summary>Cíl je v kroužku dost dlouho na detail.</summary>
    public event Action<DrawnLabel>? FocusDwelled;

    /// <summary>Kroužek je prázdný.</summary>
    public event Action? FocusLost;

    /// <summary>Promítání a rozměry z posledního vykreslení, aby šlo klepnutí převést zpět na směr.</summary>
    public (ScreenProjector Projector, double Width, double Height)? LastFrame { get; private set; }

    /// <summary>Ikonky a popisky z posledního vykreslení, pro klepnutí.</summary>
    public IReadOnlyList<DrawnLabel> Drawn => _drawn;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _drawn.Clear();
        if (Pose is not { } pose || dirtyRect.Width <= 0)
            return;

        // Svislý úhel dopočítáme z poměru stran, pixely náhledu jsou čtvercové.
        var horizontalDegrees = HorizontalFieldOfViewDegrees(dirtyRect.Width, dirtyRect.Height);
        var halfHorizontal = horizontalDegrees * Math.PI / 360;
        var verticalDegrees = 2 * Math.Atan(Math.Tan(halfHorizontal) * dirtyRect.Height / dirtyRect.Width) * 180 / Math.PI;
        var projector = new ScreenProjector(horizontalDegrees, verticalDegrees);
        LastFrame = (projector, dirtyRect.Width, dirtyRect.Height);

        var centre = new PointF(dirtyRect.Width / 2, dirtyRect.Height / 2);
        var candidates = new List<IconDeclutter.Item<(Place Place, LineOfSight Los)>>();
        foreach (var (place, los) in _sightings.Concat(SkySightings()))
        {
            if (!Layers.IsVisible(place.Kind) && place != Highlighted)
                continue;

            // Přišpendlené místo musí zůstat vidět, i když je v hustém shluku.
            var score = place == Highlighted ? double.MaxValue : Layers.Score(place, los.DistanceMeters);
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is { } point)
                candidates.Add(new((place, los), point, score));
        }

        var markers = IconDeclutter.Arrange(candidates, MinIconDistance)
            .Select(s => new Marker(s.Value.Place, s.Value.Los, s.Point, s.Score, s.Members))
            .ToList();
        ExpandFan(markers);

        var focused = UpdateFocus(markers, centre);
        Focused = focused?.Place;
        UpdateFan(markers, focused, centre);

        canvas.Font = LabelFont;
        DrawFanBackdrop(canvas, markers);
        foreach (var marker in markers.OrderBy(m => m.InFan).ThenBy(m => m.Score))
            DrawMarker(canvas, marker, marker == focused);

        DrawPermanentLabels(canvas, markers, focused, centre, dirtyRect.Width);
        DrawRing(canvas, centre, focused);
        if (focused is not null)
            DrawFocusLabel(canvas, focused, centre, dirtyRect.Width);
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
        var members = head.Members.Take(MaxFanSize).ToList();
        var offsets = FanLayout.Offsets(members.Count, FanSpacing, FanRadius);

        markers[index] = head with { Members = [] };
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var point = new ScreenPoint(head.Point.X + offsets[i].Dx, head.Point.Y + offsets[i].Dy);
            markers.Add(new Marker(member.Value.Place, member.Value.Los, point, member.Score, [], TruePoint: member.Point));
        }
    }

    private void UpdateFan(List<Marker> markers, Marker? focused, PointF centre)
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
        var fanReach = markers.Where(m => m.InFan).Select(m => DistanceTo(head.Point, m.Point)).DefaultIfEmpty(FanRadius).Max();
        var focusInFan = focused is not null && (focused.InFan || focused.Place == _expanded);
        if (!focusInFan && DistanceTo(head.Point, new ScreenPoint(centre.X, centre.Y)) > fanReach + 50)
            _expanded = null;
    }

    private static void DrawFanBackdrop(ICanvas canvas, List<Marker> markers)
    {
        // Tenké čáry od rozestoupených ikonek k jejich skutečné poloze ve shluku.
        canvas.StrokeColor = Color.FromRgba(255, 255, 255, 0.8);
        canvas.StrokeSize = 1;
        foreach (var marker in markers.Where(m => m.TruePoint is not null))
            canvas.DrawLine((float)marker.Point.X, (float)marker.Point.Y, (float)marker.TruePoint!.Value.X, (float)marker.TruePoint.Value.Y);
    }

    private static double DistanceTo(ScreenPoint a, ScreenPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private Marker? UpdateFocus(List<Marker> markers, PointF centre)
    {
        // Přišpendlené místo má přednost před kroužkem.
        if (Highlighted is { } pinned)
        {
            _focus.Reset();
            return markers.FirstOrDefault(m => m.Place == pinned);
        }

        var state = _focus.Update(
            markers.Select(m => (m.Place, Distance: Math.Sqrt(Math.Pow(m.Point.X - centre.X, 2) + Math.Pow(m.Point.Y - centre.Y, 2)))),
            DateTimeOffset.UtcNow);

        var focused = markers.FirstOrDefault(m => m.Place == state.Focused);
        if (focused is null)
        {
            FocusLost?.Invoke();
            return null;
        }

        if (state.JustAcquired)
            FocusAcquired?.Invoke(focused.Place);
        if (state.JustDwelled)
            FocusDwelled?.Invoke(new DrawnLabel(focused.Place, focused.Los, focused.Point, IconBounds(focused, true)));

        return focused;
    }

    private void DrawMarker(ICanvas canvas, Marker marker, bool focused)
    {
        // Velikost jako perspektiva: základ podle známosti, zmenšený se vzdáleností (logaritmicky).
        // Katedrála 6 km ~22 bodů, Okoř 15 km ~14, Milešovka 60 km ~12, kostel za rohem ~18.
        var basis = 10 + 2.5 * Math.Log2(1 + marker.Place.Importance);
        var distanceKm = Math.Max(0.1, marker.Los.DistanceMeters / 1000);
        var perspective = Math.Clamp(1.15 - 0.35 * Math.Log10(distanceKm), 0.55, 1.2);
        var size = (float)Math.Clamp(basis * perspective, 8, 26) * (focused ? 1.3f : 1);
        canvas.Alpha = focused ? 1 : (float)Math.Clamp(1.2 - marker.Los.DistanceMeters / 25_000, 0.55, 1);

        var x = (float)marker.Point.X;
        var y = (float)marker.Point.Y;
        var color = focused ? Color.FromArgb("#D85A30") : ColorOf(marker.Place.Kind);
        MarkerIcons.Draw(canvas, marker.Place.Kind, x, y, size, color);
        canvas.Alpha = 1;

        // Kolik dalších míst se pod ikonkou schovalo.
        if (marker.Hidden > 0)
        {
            var bx = x + size / 2 + 4;
            var by = y - size / 2 - 2;
            canvas.FillColor = Colors.White;
            canvas.FillRoundedRectangle(bx - 2, by - 8, 26, 16, 8);
            canvas.FontSize = 11;
            canvas.FontColor = Color.FromArgb("#444441");
            canvas.DrawString($"+{marker.Hidden}", bx + 11, by + 4, HorizontalAlignment.Center);
        }

        _drawn.Add(new DrawnLabel(marker.Place, marker.Los, marker.Point, IconBounds(marker, focused)));
    }

    private static RectF IconBounds(Marker marker, bool focused)
    {
        var half = focused ? 18f : 14f;
        return new RectF((float)marker.Point.X - half, (float)marker.Point.Y - half, half * 2, half * 2);
    }

    /// <summary>Pár nejvýznamnějších míst v záběru má název trvale, pro orientaci.</summary>
    private void DrawPermanentLabels(ICanvas canvas, List<Marker> markers, Marker? focused, PointF centre, float screenWidth)
    {
        canvas.FontSize = FontSize;

        // Kroužek a popisek pod ním jsou překážka, ať se trvalé popisky nepletou do zaměření.
        var ringArea = (Left: (double)centre.X - 110, Top: (double)centre.Y - RingRadius - 10, Right: (double)centre.X + 110, Bottom: (double)centre.Y + RingRadius + 60);

        var requests = markers
            .Where(m => m != focused && m.Place.Kind != PlaceKind.CelestialBody)
            .OrderByDescending(m => m.Score)
            .Take(PermanentLabels)
            .Select(m => new LabelLayout.Request<Marker>(m, m.Point, m.Score, TextWidth(canvas, m.Place.Name, FontSize) + 8));

        foreach (var placed in LabelLayout.Arrange(requests, 200, LabelHeight, obstacles: [ringArea]))
        {
            var box = LabelLayout.Box(placed.Anchor, placed.Level, placed.Width, LabelHeight);
            var halfWidth = (float)placed.Width / 2;
            var x = Math.Clamp((float)placed.Anchor.X, halfWidth + 4, screenWidth - halfWidth - 4);

            // Tenká čára od ikonky k názvu, aby bylo jasné, k čemu patří.
            canvas.StrokeColor = Colors.White;
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine((float)placed.Anchor.X, (float)placed.Anchor.Y - 12, (float)placed.Anchor.X, (float)box.Bottom + 2);

            DrawOutlinedText(canvas, placed.Item.Place.Name, x, (float)box.Bottom - 5, HorizontalAlignment.Center, Colors.Black, FontSize);
            _drawn.Add(new DrawnLabel(placed.Item.Place, placed.Item.Los, placed.Anchor, new RectF(x - halfWidth, (float)box.Top, (float)placed.Width, LabelHeight)));
        }
    }

    private static void DrawRing(ICanvas canvas, PointF centre, Marker? focused)
    {
        var color = focused is null ? Colors.White : Color.FromArgb("#D85A30");
        var width = focused is null ? 2f : 3f;

        canvas.StrokeColor = Color.FromRgba(0, 0, 0, 0.45);
        canvas.StrokeSize = width + 2;
        canvas.DrawCircle(centre, RingRadius);
        canvas.StrokeColor = color;
        canvas.StrokeSize = width;
        canvas.DrawCircle(centre, RingRadius);
    }

    /// <summary>Zaměřené místo: velký název a druh se vzdáleností pod kroužkem.</summary>
    private void DrawFocusLabel(ICanvas canvas, Marker focused, PointF centre, float screenWidth)
    {
        var place = focused.Place;
        var meta = place.Kind == PlaceKind.CelestialBody
            ? $"{focused.Los.ElevationDegrees:0}° nad obzorem"
            : Distance(focused.Los);

        canvas.FontSize = FocusFontSize;
        DrawOutlinedText(canvas, place.Name, centre.X, centre.Y + RingRadius + 28, HorizontalAlignment.Center, Color.FromArgb("#712B13"), FocusFontSize);
        canvas.FontSize = FontSize;
        DrawOutlinedText(canvas, meta, centre.X, centre.Y + RingRadius + 50, HorizontalAlignment.Center, Colors.Black, FontSize);

        // Čára od kroužku k zaměřené ikonce, když není přímo uprostřed.
        var dx = (float)focused.Point.X - centre.X;
        var dy = (float)focused.Point.Y - centre.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length > RingRadius)
        {
            canvas.StrokeColor = Color.FromArgb("#D85A30");
            canvas.StrokeSize = 2;
            canvas.DrawLine(centre.X + dx / length * RingRadius, centre.Y + dy / length * RingRadius, (float)focused.Point.X, (float)focused.Point.Y);
        }
    }

    private static void DrawOutlinedText(ICanvas canvas, string text, float x, float baseline, HorizontalAlignment alignment, Color color, float fontSize)
    {
        // Kreslíme do pole mnohem širšího než text: Android u kreslení z jednoho bodu omezuje šířku řádku
        // a láme i uprostřed slova. Spodek pole leží kousek pod účařím, aby tam text dosedl.
        const float wide = 2000;
        var left = alignment switch
        {
            HorizontalAlignment.Left => x,
            HorizontalAlignment.Right => x - wide,
            _ => x - wide / 2,
        };
        var top = baseline - fontSize * 1.3f;
        var height = fontSize * 1.6f;

        canvas.FontColor = Colors.White;
        foreach (var (dx, dy) in Halo)
            canvas.DrawString(text, left + dx, top + dy, wide, height, alignment, VerticalAlignment.Bottom);

        canvas.FontColor = color;
        canvas.DrawString(text, left, top, wide, height, alignment, VerticalAlignment.Bottom);
    }

    /// <summary>Slunce a Měsíc nad obzorem; slouží i jako kalibrační cíl.</summary>
    private IEnumerable<(Place, LineOfSight)> SkySightings()
    {
        if (_observer is not { } observer)
            yield break;

        var now = DateTimeOffset.UtcNow;
        foreach (var (place, body) in SkyPlaces)
        {
            var los = Ephemeris.Observe(body, observer, now);
            if (los.ElevationDegrees > -1)
                yield return (place, los);
        }
    }

    private static readonly (Place Place, CelestialBody Body)[] SkyPlaces =
    [
        (new Place("Slunce", default, PlaceKind.CelestialBody), CelestialBody.Sun),
        (new Place("Měsíc", default, PlaceKind.CelestialBody), CelestialBody.Moon),
    ];

    private List<(Place, LineOfSight)> Sightings(GeoPoint? observer) =>
        observer is not { } from
            ? []
            : _places
                .Where(p => LineOfSight.IsAboveHorizon(from, p.Location))
                .Select(p => (p, LineOfSight.Between(from, p.Location)))
                .Where(x => ViewRange.IsWithin(x.p, x.Item2.DistanceMeters, _rangeKm))
                .ToList();

    // Šířku měříme stejným písmem a velikostí, jakými pak kreslíme.
    private float TextWidth(ICanvas canvas, string text, float fontSize)
    {
        var key = $"{fontSize}|{text}";
        if (!_textWidths.TryGetValue(key, out var width))
        {
            // S rezervou: měření písma na Androidu vychází o kousek užší, než pak kreslení.
            width = canvas.GetStringSize(text, LabelFont, fontSize).Width * 1.12f;
            _textWidths[key] = width;
        }

        return width;
    }

    private static string Distance(LineOfSight los) => los.DistanceMeters < 10_000
        ? $"{los.DistanceMeters / 1000:0.0} km"
        : $"{los.DistanceMeters / 1000:0} km";

    public static Color ColorOf(PlaceKind kind) => kind switch
    {
        PlaceKind.Peak => Color.FromArgb("#BA7517"),
        PlaceKind.Bridge => Color.FromArgb("#185FA5"),
        PlaceKind.CelestialBody => Color.FromArgb("#EF9F27"),
        PlaceKind.City => Color.FromArgb("#444441"),
        PlaceKind.Building => Color.FromArgb("#5F5E5A"),
        PlaceKind.Castle => Color.FromArgb("#993556"),
        PlaceKind.Tower => Color.FromArgb("#534AB7"),
        _ => Color.FromArgb("#0F6E56"),
    };

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

/// <summary>Ikonka nebo popisek z posledního snímku: místo, směr k němu, bod cíle na obrazovce a plocha pro klepnutí.</summary>
public sealed record DrawnLabel(Place Place, LineOfSight LineOfSight, ScreenPoint Point, RectF Bounds);
