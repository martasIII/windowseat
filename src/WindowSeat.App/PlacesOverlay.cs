using WindowSeat.Core.Composition;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

/// <summary>
/// Kreslí scénu popisků přes náhled kamery. Co a kam nakreslit, rozhoduje <see cref="SceneComposer"/> v knihovně
/// (stejně jako ve webové verzi); tady jsou jen tvary ikonek, barvy a písmo s bílým obrysem.
/// </summary>
public sealed class PlacesOverlay : IDrawable
{
    // Bílý obrys: text se nakreslí bíle posunutý do všech stran a barevně navrch.
    private static readonly (float X, float Y)[] Halo =
    [
        (-1.6f, 0), (1.6f, 0), (0, -1.6f), (0, 1.6f),
        (-1.2f, -1.2f), (1.2f, -1.2f), (-1.2f, 1.2f), (1.2f, 1.2f),
    ];

    private static readonly Microsoft.Maui.Graphics.Font LabelFont = Microsoft.Maui.Graphics.Font.DefaultBold;

    private readonly SceneComposer _composer = new();
    private readonly Dictionary<string, float> _textWidths = [];
    private ICanvas? _measuringCanvas;

    public PlacesOverlay(IReadOnlyList<Place> places) => _composer.Places = places;

    public IReadOnlyList<Place> Places
    {
        get => _composer.Places;
        set => _composer.Places = value;
    }

    public IReadOnlyList<WaterArea> Water
    {
        get => _composer.Water;
        set => _composer.Water = value;
    }

    public IReadOnlyList<WaterArea> Bridges
    {
        get => _composer.Bridges;
        set => _composer.Bridges = value;
    }

    public GeoPoint? Observer
    {
        get => _composer.Observer;
        set => _composer.Observer = value;
    }

    public bool ShowWater
    {
        get => _composer.ShowWater;
        set => _composer.ShowWater = value;
    }

    public LayerWeights Layers
    {
        get => _composer.Layers;
        set => _composer.Layers = value;
    }

    public double RangeKm
    {
        get => _composer.RangeKm;
        set => _composer.RangeKm = value;
    }

    /// <summary>Přišpendlené místo (klepnutím nebo kalibrací); drží zaměření místo kroužku.</summary>
    public Place? Highlighted
    {
        get => _composer.Highlighted;
        set => _composer.Highlighted = value;
    }

    public CameraPose? Pose { get; set; }

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

    /// <summary>Poslední vykreslená scéna: pro klepnutí, kalibraci a záznam.</summary>
    public Scene? LastScene { get; private set; }

    /// <summary>Co je právě v kroužku (nebo přišpendlené).</summary>
    public Place? Focused => LastScene?.Focus?.Place;

    /// <summary>Cíl se chytil do kroužku (appka cvakne vibrací).</summary>
    public event Action<Place>? FocusAcquired;

    /// <summary>Cíl je v kroužku dost dlouho na detail.</summary>
    public event Action<SceneHit>? FocusDwelled;

    /// <summary>Kroužek je prázdný.</summary>
    public event Action? FocusLost;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Pose is not { } pose || dirtyRect.Width <= 0)
        {
            LastScene = null;
            return;
        }

        _measuringCanvas = canvas;
        var scene = _composer.Compose(
            pose,
            dirtyRect.Width,
            dirtyRect.Height,
            HorizontalFieldOfViewDegrees(dirtyRect.Width, dirtyRect.Height),
            DateTimeOffset.UtcNow,
            TextWidth);
        LastScene = scene;

        RaiseFocusEvents(scene);

        DrawPolygons(canvas, scene.Water, fill: Color.FromRgba(55, 138, 221, 90), stroke: Color.FromRgba(255, 255, 255, 140), highlightFill: null);
        DrawPolygons(canvas, scene.Bridges, fill: Color.FromRgba(255, 255, 255, 150), stroke: Color.FromRgba(68, 68, 65, 200), highlightFill: Color.FromRgba(216, 90, 48, 200));

        canvas.Font = LabelFont;

        // Tenké čáry od rozestoupených ikonek vějíře k jejich skutečné poloze ve shluku.
        canvas.StrokeColor = Color.FromRgba(255, 255, 255, 0.8);
        canvas.StrokeSize = 1;
        foreach (var marker in scene.Markers.Where(m => m.TruePoint is not null))
            canvas.DrawLine((float)marker.Point.X, (float)marker.Point.Y, (float)marker.TruePoint!.Value.X, (float)marker.TruePoint.Value.Y);

        foreach (var marker in scene.Markers)
            DrawMarker(canvas, marker);

        foreach (var label in scene.Labels)
            DrawLabel(canvas, label);

        DrawRing(canvas, scene);
        if (scene.Focus is { } focus)
            DrawFocus(canvas, scene, focus);
    }

    private void RaiseFocusEvents(Scene scene)
    {
        if (scene.Focus is not { } focus)
        {
            FocusLost?.Invoke();
            return;
        }

        if (scene.FocusAcquired)
            FocusAcquired?.Invoke(focus.Place);
        if (scene.FocusDwelled)
            FocusDwelled?.Invoke(new SceneHit(focus.Place, focus.Los, focus.Point));
    }

    private static void DrawPolygons(ICanvas canvas, IReadOnlyList<ScenePolygon> polygons, Color fill, Color stroke, Color? highlightFill)
    {
        foreach (var polygon in polygons)
        {
            var path = new PathF();
            foreach (var ring in polygon.Rings)
            {
                path.MoveTo((float)ring[0].X, (float)ring[0].Y);
                foreach (var point in ring.Skip(1))
                    path.LineTo((float)point.X, (float)point.Y);
                path.Close();
            }

            var highlighted = polygon.Highlighted && highlightFill is not null;
            canvas.FillColor = highlighted ? highlightFill! : fill;
            canvas.StrokeColor = highlighted ? Color.FromArgb(SceneStyle.FocusTextColor) : stroke;
            canvas.StrokeSize = highlighted ? 2 : 1;
            canvas.FillPath(path, WindingMode.EvenOdd);
            canvas.DrawPath(path);
        }
    }

    private static void DrawMarker(ICanvas canvas, SceneMarker marker)
    {
        var x = (float)marker.Point.X;
        var y = (float)marker.Point.Y;
        var size = (float)marker.Size;

        canvas.Alpha = (float)marker.Opacity;
        var color = Color.FromArgb(marker.Focused ? SceneStyle.FocusColor : SceneStyle.ColorOf(marker.Place.Kind));
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
    }

    private static void DrawLabel(ICanvas canvas, SceneLabel label)
    {
        // Tenká čára od ikonky k názvu, aby bylo jasné, k čemu patří.
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine((float)label.Anchor.X, (float)label.Anchor.Y - 12, (float)label.Anchor.X, (float)label.Bounds.Bottom + 2);

        canvas.FontSize = (float)SceneStyle.FontSize;
        DrawOutlinedText(canvas, label.Text, (float)label.CentreX, (float)label.Baseline, Colors.Black, (float)SceneStyle.FontSize);
    }

    private static void DrawRing(ICanvas canvas, Scene scene)
    {
        var focused = scene.Focus is not null;
        var centre = new PointF((float)scene.Centre.X, (float)scene.Centre.Y);
        var width = focused ? 3f : 2f;

        canvas.StrokeColor = Color.FromRgba(0, 0, 0, 0.45);
        canvas.StrokeSize = width + 2;
        canvas.DrawCircle(centre, (float)SceneStyle.RingRadius);
        canvas.StrokeColor = focused ? Color.FromArgb(SceneStyle.FocusColor) : Colors.White;
        canvas.StrokeSize = width;
        canvas.DrawCircle(centre, (float)SceneStyle.RingRadius);
    }

    /// <summary>Zaměřené místo: velký název a podrobnost pod kroužkem, čára ke vzdálenější ikonce.</summary>
    private static void DrawFocus(ICanvas canvas, Scene scene, SceneFocus focus)
    {
        var cx = (float)scene.Centre.X;
        var cy = (float)scene.Centre.Y;
        var ring = (float)SceneStyle.RingRadius;

        canvas.FontSize = (float)focus.NameFontSize;
        DrawOutlinedText(canvas, focus.Name, cx, cy + ring + 28, Color.FromArgb(SceneStyle.FocusTextColor), (float)focus.NameFontSize);
        canvas.FontSize = (float)SceneStyle.FontSize;
        DrawOutlinedText(canvas, focus.Detail, cx, cy + ring + 50, Colors.Black, (float)SceneStyle.FontSize);

        var dx = (float)focus.Point.X - cx;
        var dy = (float)focus.Point.Y - cy;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length > ring)
        {
            canvas.StrokeColor = Color.FromArgb(SceneStyle.FocusColor);
            canvas.StrokeSize = 2;
            canvas.DrawLine(cx + dx / length * ring, cy + dy / length * ring, (float)focus.Point.X, (float)focus.Point.Y);
        }
    }

    private static void DrawOutlinedText(ICanvas canvas, string text, float centreX, float baseline, Color color, float fontSize)
    {
        // Kreslíme do pole mnohem širšího než text: Android u kreslení z jednoho bodu omezuje šířku řádku
        // a láme i uprostřed slova. Spodek pole leží kousek pod účařím, aby tam text dosedl.
        const float wide = 2000;
        var left = centreX - wide / 2;
        var top = baseline - fontSize * 1.3f;
        var height = fontSize * 1.6f;

        canvas.FontColor = Colors.White;
        foreach (var (dx, dy) in Halo)
            canvas.DrawString(text, left + dx, top + dy, wide, height, HorizontalAlignment.Center, VerticalAlignment.Bottom);

        canvas.FontColor = color;
        canvas.DrawString(text, left, top, wide, height, HorizontalAlignment.Center, VerticalAlignment.Bottom);
    }

    // Šířku měříme stejným písmem a velikostí, jakými pak kreslíme.
    private double TextWidth(string text, double fontSize)
    {
        var key = $"{fontSize}|{text}";
        if (!_textWidths.TryGetValue(key, out var width) && _measuringCanvas is { } canvas)
        {
            // S rezervou: měření písma na Androidu vychází o kousek užší, než pak kreslení.
            width = canvas.GetStringSize(text, LabelFont, (float)fontSize).Width * 1.12f;
            _textWidths[key] = width;
        }

        return width;
    }

    public static Color ColorOf(PlaceKind kind) => Color.FromArgb(SceneStyle.ColorOf(kind));
}
