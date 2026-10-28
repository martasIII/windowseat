using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;
using WindowSeat.Core.Sky;

namespace WindowSeat.App;

/// <summary>
/// Kreslí popisky míst přes náhled kamery: svisle v pruhu u horního okraje, s čarou dolů k cíli.
/// Nic se nekříží a krajina pod pruhem zůstává volná; co se nevejde, ustoupí známějším a bližším místům.
/// </summary>
public sealed class PlacesOverlay(IReadOnlyList<Place> places) : IDrawable
{
    private const float FontSize = 14;
    private const float PillThickness = 22;
    private const float ColumnWidth = 26;
    private const float BandTop = 70;
    private const float BandLength = 230;
    private const float BandBottom = BandTop + BandLength;

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

    /// <summary>Vybrané místo (kalibrace nebo detail); má přednost a zvýrazní se.</summary>
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
        var horizontalDegrees = HorizontalFieldOfViewDegrees(dirtyRect.Width, dirtyRect.Height);
        var halfHorizontal = horizontalDegrees * Math.PI / 360;
        var verticalDegrees = 2 * Math.Atan(Math.Tan(halfHorizontal) * dirtyRect.Height / dirtyRect.Width) * 180 / Math.PI;
        var projector = new ScreenProjector(horizontalDegrees, verticalDegrees);
        LastFrame = (projector, dirtyRect.Width, dirtyRect.Height);

        canvas.FontSize = FontSize;

        var candidates = new List<SkylineLayout.Candidate<(Place Place, LineOfSight Los, string Text)>>();
        foreach (var (place, los) in _sightings.Concat(SkySightings()))
        {
            if (!Layers.IsVisible(place.Kind) && place != Highlighted)
                continue;
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is not { } point)
                continue;

            var text = place.Kind == PlaceKind.CelestialBody ? place.Name : $"{place.Name} · {Distance(los)}";
            var score = place == Highlighted ? double.PositiveInfinity : Layers.Score(place, los.DistanceMeters);
            candidates.Add(new((place, los, text), point, score));
        }

        foreach (var slot in SkylineLayout.Arrange(candidates, dirtyRect.Width, ColumnWidth))
        {
            var (place, los, text) = slot.Item;
            var length = Math.Min(BandLength, TextWidth(canvas, text) + 14);
            var centre = new ScreenPoint(slot.LabelX, BandBottom - length / 2);

            _drawn.Add(new DrawnLabel(place, los, slot.Anchor, centre, length));
            DrawLabel(canvas, place, text, slot, length, place == Highlighted);
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

    public static Color ColorOf(PlaceKind kind) => kind switch
    {
        PlaceKind.Peak => Color.FromArgb("#EF9F27"),
        PlaceKind.Bridge => Color.FromArgb("#85B7EB"),
        PlaceKind.CelestialBody => Color.FromArgb("#FAC775"),
        PlaceKind.City => Colors.White,
        PlaceKind.Building => Color.FromArgb("#B4B2A9"),
        _ => Color.FromArgb("#5DCAA5"),
    };

    private static void DrawLabel(
        ICanvas canvas,
        Place place,
        string text,
        SkylineLayout.Slot<(Place, LineOfSight, string)> slot,
        float length,
        bool highlighted)
    {
        var color = highlighted ? Color.FromArgb("#F0997B") : ColorOf(place.Kind);
        var labelX = (float)slot.LabelX;
        var anchorX = (float)slot.Anchor.X;
        var anchorY = (float)slot.Anchor.Y;

        // Čára od spodku popisku k cíli; když popisek uhnul vedle, je šikmá.
        canvas.StrokeColor = color;
        canvas.StrokeSize = highlighted ? 2.5f : 1.5f;
        canvas.DrawLine(labelX, BandBottom + 3, anchorX, anchorY);
        canvas.FillColor = color;
        canvas.FillCircle(anchorX, anchorY, highlighted ? 5 : 3);

        // Text svisle zdola nahoru, začíná u spodního okraje pruhu.
        canvas.SaveState();
        canvas.Translate(labelX, BandBottom);
        canvas.Rotate(-90);
        canvas.FillColor = highlighted ? Color.FromArgb("#CC712B13") : Color.FromRgba(0, 0, 0, 0.55);
        canvas.FillRoundedRectangle(0, -PillThickness / 2, length, PillThickness, PillThickness / 2);
        canvas.FontColor = Colors.White;
        canvas.DrawString(text, 7, -PillThickness / 2, length - 14, PillThickness, HorizontalAlignment.Left, VerticalAlignment.Center);
        canvas.RestoreState();
    }
}

public sealed record DrawnLabel(Place Place, LineOfSight LineOfSight, ScreenPoint Point, ScreenPoint LabelCentre, double LabelLength);
