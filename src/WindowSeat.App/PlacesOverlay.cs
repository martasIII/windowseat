using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;
using WindowSeat.Core.Sky;

namespace WindowSeat.App;

/// <summary>
/// Kreslí popisky míst přes náhled kamery. Vrcholy svisle v pruhu u horního okraje s čarou dolů (tvoří panorama),
/// ostatní vodorovně těsně nad cílem. Písmo bez podkladu, černé s bílým obrysem, aby bylo čitelné na obloze i na stínu.
/// </summary>
public sealed class PlacesOverlay(IReadOnlyList<Place> places) : IDrawable
{
    private const float FontSize = 15;
    private const float ColumnWidth = 24;
    private const float BandTop = 70;
    private const float BandLength = 230;
    private const float BandBottom = BandTop + BandLength;
    private const float LabelHeight = 20;

    // Bílý obrys: text se nakreslí bíle posunutý do všech stran a černě navrch.
    private static readonly (float X, float Y)[] Halo =
    [
        (-1.6f, 0), (1.6f, 0), (0, -1.6f), (0, 1.6f),
        (-1.2f, -1.2f), (1.2f, -1.2f), (-1.2f, 1.2f), (1.2f, 1.2f),
    ];

    private static readonly Microsoft.Maui.Graphics.Font LabelFont = Microsoft.Maui.Graphics.Font.DefaultBold;

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

    /// <summary>Popisky z posledního vykreslení: místo, jeho směr, bod cíle a plocha popisku.</summary>
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

        canvas.Font = LabelFont;
        canvas.FontSize = FontSize;

        var peaks = new List<SkylineLayout.Candidate<Label>>();
        var others = new List<LabelLayout.Request<Label>>();

        foreach (var (place, los) in _sightings.Concat(SkySightings()))
        {
            if (!Layers.IsVisible(place.Kind) && place != Highlighted)
                continue;
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is not { } point)
                continue;

            // Nezlomitelné mezery kolem tečky a před „km“: Android jinak zalamuje i krátký popisek.
            var text = place.Kind == PlaceKind.CelestialBody ? place.Name : $"{place.Name} · {Distance(los)}";
            var label = new Label(place, los, text, TextWidth(canvas, text));
            var score = place == Highlighted ? double.MaxValue : Layers.Score(place, los.DistanceMeters);

            if (place.Kind == PlaceKind.Peak)
                peaks.Add(new(label, point, score));
            else
                others.Add(new(label, point, score, label.TextWidth + 8));
        }

        var peakColumns = new List<(double Left, double Top, double Right, double Bottom)>();
        foreach (var slot in SkylineLayout.Arrange(peaks, dirtyRect.Width, ColumnWidth))
            peakColumns.Add(DrawVertical(canvas, slot));

        // Vodorovné popisky se svislým v pruhu vyhnou.
        foreach (var placed in LabelLayout.Arrange(others, 200, LabelHeight, obstacles: peakColumns))
            DrawHorizontal(canvas, placed, dirtyRect.Width);
    }

    /// <summary>Vrchol: svislý text v pruhu nahoře, čára dolů k vrcholu. Vrací plochu textu.</summary>
    private (double Left, double Top, double Right, double Bottom) DrawVertical(ICanvas canvas, SkylineLayout.Slot<Label> slot)
    {
        var label = slot.Item;
        var highlighted = label.Place == Highlighted;
        var length = Math.Min(BandLength, label.TextWidth);
        var x = (float)slot.LabelX;

        DrawLeader(canvas, label.Place, new PointF(x, BandBottom + 4), slot.Anchor, highlighted);

        // Text zdola nahoru, začíná u spodního okraje pruhu; účaří posunuté, aby osa písma ležela na sloupci.
        canvas.SaveState();
        canvas.Translate(x, BandBottom);
        canvas.Rotate(-90);
        DrawOutlinedText(canvas, label.Text, 0, FontSize * 0.35f, HorizontalAlignment.Left, highlighted);
        canvas.RestoreState();

        var bounds = new RectF(x - ColumnWidth / 2, BandBottom - length, ColumnWidth, length);
        _drawn.Add(new DrawnLabel(label.Place, label.Los, slot.Anchor, bounds));
        return (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
    }

    /// <summary>Ostatní místa: vodorovný text těsně nad cílem, při nedostatku místa o patro výš.</summary>
    private void DrawHorizontal(ICanvas canvas, LabelLayout.Placed<Label> placed, float screenWidth)
    {
        var label = placed.Item;
        var highlighted = label.Place == Highlighted;
        var box = LabelLayout.Box(placed.Anchor, placed.Level, placed.Width, LabelHeight);

        // Popisek u okraje posuneme dovnitř obrazovky; čára pořád vede k cíli.
        var halfWidth = (float)placed.Width / 2;
        var centreX = Math.Clamp((float)placed.Anchor.X, halfWidth + 4, screenWidth - halfWidth - 4);

        DrawLeader(canvas, label.Place, new PointF((float)placed.Anchor.X, (float)box.Bottom + 2), placed.Anchor, highlighted);
        DrawOutlinedText(canvas, label.Text, centreX, (float)box.Bottom - 5, HorizontalAlignment.Center, highlighted);

        var bounds = new RectF(centreX - halfWidth, (float)box.Top, (float)placed.Width, LabelHeight);
        _drawn.Add(new DrawnLabel(label.Place, label.Los, placed.Anchor, bounds));
    }

    private static void DrawLeader(ICanvas canvas, Place place, PointF from, ScreenPoint anchor, bool highlighted)
    {
        var color = highlighted ? Color.FromArgb("#D85A30") : ColorOf(place.Kind);
        var to = new PointF((float)anchor.X, (float)anchor.Y);
        var width = highlighted ? 2.5f : 1.5f;

        // Bílý lem pod barevnou čárou, ať je vidět i na světlém pozadí.
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = width + 2;
        canvas.DrawLine(from, to);
        canvas.FillColor = Colors.White;
        canvas.FillCircle(to, (highlighted ? 5 : 3.5f) + 1.5f);

        canvas.StrokeColor = color;
        canvas.StrokeSize = width;
        canvas.DrawLine(from, to);
        canvas.FillColor = color;
        canvas.FillCircle(to, highlighted ? 5 : 3.5f);
    }

    private static void DrawOutlinedText(ICanvas canvas, string text, float x, float baseline, HorizontalAlignment alignment, bool highlighted)
    {
        // Popisek je vždy jeden řádek; bez nezlomitelných mezer by ho Android mohl zalomit.
        text = text.Replace(' ', ' ');

        canvas.FontColor = Colors.White;
        foreach (var (dx, dy) in Halo)
            canvas.DrawString(text, x + dx, baseline + dy, alignment);

        canvas.FontColor = highlighted ? Color.FromArgb("#993C1D") : Colors.Black;
        canvas.DrawString(text, x, baseline, alignment);
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

    // Šířku měříme stejným písmem a velikostí, jakými pak kreslíme.
    private float TextWidth(ICanvas canvas, string text)
    {
        if (!_textWidths.TryGetValue(text, out var width))
        {
            width = canvas.GetStringSize(text, LabelFont, FontSize).Width;
            _textWidths[text] = width;
        }

        return width;
    }

    private static string Distance(LineOfSight los) => los.DistanceMeters < 10_000
        ? $"{los.DistanceMeters / 1000:0.0} km"
        : $"{los.DistanceMeters / 1000:0} km";

    public static Color ColorOf(PlaceKind kind) => kind switch
    {
        PlaceKind.Peak => Color.FromArgb("#BA7517"),
        PlaceKind.Bridge => Color.FromArgb("#185FA5"),
        PlaceKind.CelestialBody => Color.FromArgb("#EF9F27"),
        PlaceKind.City => Color.FromArgb("#444441"),
        PlaceKind.Building => Color.FromArgb("#5F5E5A"),
        _ => Color.FromArgb("#0F6E56"),
    };

    private sealed record Label(Place Place, LineOfSight Los, string Text, float TextWidth);
}

/// <summary>Popisek z posledního snímku: místo, směr k němu, bod cíle na obrazovce a plocha textu pro klepnutí.</summary>
public sealed record DrawnLabel(Place Place, LineOfSight LineOfSight, ScreenPoint Point, RectF Bounds);
