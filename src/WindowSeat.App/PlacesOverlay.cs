using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;
using WindowSeat.Core.Sky;

namespace WindowSeat.App;

/// <summary>Kreslí popisky míst přes náhled kamery podle aktuální pózy a polohy.</summary>
public sealed class PlacesOverlay(IReadOnlyList<Place> places) : IDrawable
{
    private const float FontSize = 15;
    private const float LabelHeight = 24;
    private const float LabelPadding = 16;
    private const float MaxLabelWidth = 300;

    private readonly List<DrawnLabel> _drawn = [];
    private readonly Dictionary<string, float> _textWidths = [];

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

    public double HorizontalFieldOfViewDegrees { get; set; } = 50;

    /// <summary>Místo vybrané pro kalibraci; zvýrazní se.</summary>
    public Place? Highlighted { get; set; }

    /// <summary>Promítání a rozměry z posledního vykreslení, aby šlo klepnutí převést zpět na směr.</summary>
    public (ScreenProjector Projector, double Width, double Height)? LastFrame { get; private set; }

    /// <summary>Popisky z posledního vykreslení: místo, jeho směr, bod cíle a střed popisku.</summary>
    public IReadOnlyList<DrawnLabel> Drawn => _drawn;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _drawn.Clear();
        if (Pose is not { } pose || dirtyRect.Width <= 0)
            return;

        // Svislý úhel dopočítáme z poměru stran, pixely náhledu jsou čtvercové.
        var halfHorizontal = HorizontalFieldOfViewDegrees * Math.PI / 360;
        var verticalDegrees = 2 * Math.Atan(Math.Tan(halfHorizontal) * dirtyRect.Height / dirtyRect.Width) * 180 / Math.PI;
        var projector = new ScreenProjector(HorizontalFieldOfViewDegrees, verticalDegrees);
        LastFrame = (projector, dirtyRect.Width, dirtyRect.Height);

        canvas.FontSize = FontSize;
        canvas.FontColor = Colors.White;

        var requests = new List<LabelLayout.Request<(Place Place, LineOfSight Los, string Text)>>();
        foreach (var (place, los) in _sightings.Concat(SkySightings()))
        {
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is not { } point)
                continue;

            var text = place.Kind == PlaceKind.CelestialBody ? place.Name : $"{place.Name} · {Distance(los)}";
            var width = Math.Min(MaxLabelWidth, TextWidth(canvas, text) + LabelPadding);

            // Vybraný cíl kalibrace musí zůstat vidět, i kdyby byl nejméně známý.
            var importance = place == Highlighted ? int.MaxValue : place.Importance;
            requests.Add(new((place, los, text), point, importance, width));
        }

        foreach (var label in LabelLayout.Arrange(requests, MaxLabelWidth, LabelHeight))
        {
            var (place, los, text) = label.Item;
            var box = LabelLayout.Box(label.Anchor, label.Level, label.Width, LabelHeight);
            var centre = new ScreenPoint((box.Left + box.Right) / 2, (box.Top + box.Bottom) / 2);

            _drawn.Add(new DrawnLabel(place, los, label.Anchor, centre));
            DrawLabel(canvas, place, text, label.Anchor, box, place == Highlighted);
        }
    }

    /// <summary>Slunce a Měsíc nad obzorem; přednost před vším ostatním, slouží jako kalibrační cíl.</summary>
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
        (new Place("Slunce", default, PlaceKind.CelestialBody, int.MaxValue - 1), CelestialBody.Sun),
        (new Place("Měsíc", default, PlaceKind.CelestialBody, int.MaxValue - 1), CelestialBody.Moon),
    ];

    private List<(Place, LineOfSight)> Sightings(GeoPoint? observer) =>
        observer is not { } from
            ? []
            : _places
                .Where(p => LineOfSight.IsAboveHorizon(from, p.Location))
                .Select(p => (p, LineOfSight.Between(from, p.Location)))
                .ToList();

    private float TextWidth(ICanvas canvas, string text)
    {
        if (!_textWidths.TryGetValue(text, out var width))
        {
            width = canvas.GetStringSize(text, Microsoft.Maui.Graphics.Font.Default, FontSize).Width;
            _textWidths[text] = width;
        }

        return width;
    }

    private static string Distance(LineOfSight los) => los.DistanceMeters < 10_000
        ? $"{los.DistanceMeters / 1000:0.0} km"
        : $"{los.DistanceMeters / 1000:0} km";

    private static void DrawLabel(
        ICanvas canvas,
        Place place,
        string text,
        ScreenPoint anchor,
        (double Left, double Top, double Right, double Bottom) box,
        bool highlighted)
    {
        var color = highlighted
            ? Color.FromArgb("#F0997B")
            : place.Kind switch
            {
                PlaceKind.Peak => Color.FromArgb("#EF9F27"),
                PlaceKind.Landmark => Color.FromArgb("#5DCAA5"),
                PlaceKind.Bridge => Color.FromArgb("#85B7EB"),
                PlaceKind.CelestialBody => Color.FromArgb("#FAC775"),
                _ => Colors.White,
            };

        var x = (float)anchor.X;
        var y = (float)anchor.Y;

        // Čárka z cíle k popisku; u popisku posunutého o patro výš je delší.
        canvas.StrokeColor = color;
        canvas.StrokeSize = highlighted ? 3 : 2;
        canvas.DrawLine(x, y, x, (float)box.Bottom);
        canvas.FillColor = color;
        canvas.FillCircle(x, y, highlighted ? 5 : 3);

        var left = (float)box.Left;
        var top = (float)box.Top;
        var width = (float)(box.Right - box.Left);
        canvas.FillColor = highlighted ? Color.FromArgb("#B3712B13") : Color.FromRgba(0, 0, 0, 0.55);
        canvas.FillRoundedRectangle(left, top, width, LabelHeight, 6);
        canvas.DrawString(text, left, top, width, LabelHeight, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}

public sealed record DrawnLabel(Place Place, LineOfSight LineOfSight, ScreenPoint Point, ScreenPoint LabelCentre);
