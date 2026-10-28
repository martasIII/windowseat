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

    /// <summary>Zorný úhel objektivu ze systému; z něj a rozměrů obrazovky se počítá úhel přes šířku.</summary>
    public LensFieldOfView? Lens { get; set; }

    /// <summary>Ručně nastavený úhel přes šířku obrazovky; má přednost před objektivem.</summary>
    public double? ManualHorizontalFieldOfViewDegrees { get; set; }

    /// <summary>Úhel přes šířku obrazovky pro dané rozměry: ruční, z objektivu, nebo odhad.</summary>
    public double HorizontalFieldOfViewDegrees(double width, double height) =>
        ManualHorizontalFieldOfViewDegrees
        ?? Lens?.VisibleHorizontalDegrees(width, height)
        ?? 50;

    /// <summary>Přišpendlené místo (klepnutím nebo kalibrací); drží zaměření místo kroužku.</summary>
    public Place? Highlighted { get; set; }

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
        var markers = new List<Marker>();
        foreach (var (place, los) in _sightings.Concat(SkySightings()))
        {
            if (!Layers.IsVisible(place.Kind) && place != Highlighted)
                continue;
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is { } point)
                markers.Add(new Marker(place, los, point, Layers.Score(place, los.DistanceMeters)));
        }

        var focused = UpdateFocus(markers, centre);

        canvas.Font = LabelFont;
        foreach (var marker in markers.OrderBy(m => m.Score))
            DrawMarker(canvas, marker, marker == focused);

        DrawPermanentLabels(canvas, markers, focused, centre, dirtyRect.Width);
        DrawRing(canvas, centre, focused);
        if (focused is not null)
            DrawFocusLabel(canvas, focused, centre, dirtyRect.Width);
    }

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
        // Známější a bližší místa mají větší ikonku; vzdálená jsou průhlednější.
        var size = (float)Math.Clamp(10 + 2.5 * Math.Log2(1 + marker.Place.Importance), 10, 22) * (focused ? 1.4f : 1);
        canvas.Alpha = focused ? 1 : (float)Math.Clamp(1.2 - marker.Los.DistanceMeters / 25_000, 0.55, 1);

        var color = focused ? Color.FromArgb("#D85A30") : ColorOf(marker.Place.Kind);
        MarkerIcons.Draw(canvas, marker.Place.Kind, (float)marker.Point.X, (float)marker.Point.Y, size, color);
        canvas.Alpha = 1;

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

            DrawOutlinedText(canvas, placed.Item.Place.Name, x, (float)box.Bottom - 5, HorizontalAlignment.Center, Colors.Black);
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
        DrawOutlinedText(canvas, place.Name, centre.X, centre.Y + RingRadius + 28, HorizontalAlignment.Center, Color.FromArgb("#712B13"));
        canvas.FontSize = FontSize;
        DrawOutlinedText(canvas, meta, centre.X, centre.Y + RingRadius + 50, HorizontalAlignment.Center, Colors.Black);

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

    private static void DrawOutlinedText(ICanvas canvas, string text, float x, float baseline, HorizontalAlignment alignment, Color color)
    {
        // Popisek je vždy jeden řádek; bez nezlomitelných mezer by ho Android mohl zalomit.
        text = text.Replace(' ', ' ');

        canvas.FontColor = Colors.White;
        foreach (var (dx, dy) in Halo)
            canvas.DrawString(text, x + dx, baseline + dy, alignment);

        canvas.FontColor = color;
        canvas.DrawString(text, x, baseline, alignment);
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
                .ToList();

    // Šířku měříme stejným písmem a velikostí, jakými pak kreslíme.
    private float TextWidth(ICanvas canvas, string text, float fontSize)
    {
        var key = $"{fontSize}|{text}";
        if (!_textWidths.TryGetValue(key, out var width))
        {
            width = canvas.GetStringSize(text, LabelFont, fontSize).Width;
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

    private sealed record Marker(Place Place, LineOfSight Los, ScreenPoint Point, double Score);
}

/// <summary>Ikonka nebo popisek z posledního snímku: místo, směr k němu, bod cíle na obrazovce a plocha pro klepnutí.</summary>
public sealed record DrawnLabel(Place Place, LineOfSight LineOfSight, ScreenPoint Point, RectF Bounds);
