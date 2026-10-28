using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

/// <summary>Kreslí popisky míst přes náhled kamery podle aktuální pózy a polohy.</summary>
public sealed class PlacesOverlay(IReadOnlyList<Place> places) : IDrawable
{
    private const float LabelWidth = 220;
    private const float LabelHeight = 24;

    private readonly List<DrawnLabel> _drawn = [];

    public CameraPose? Pose { get; set; }

    public GeoPoint? Observer { get; set; }

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
        if (Pose is not { } pose || Observer is not { } observer || dirtyRect.Width <= 0)
            return;

        // Svislý úhel dopočítáme z poměru stran, pixely náhledu jsou čtvercové.
        var halfHorizontal = HorizontalFieldOfViewDegrees * Math.PI / 360;
        var verticalDegrees = 2 * Math.Atan(Math.Tan(halfHorizontal) * dirtyRect.Height / dirtyRect.Width) * 180 / Math.PI;
        var projector = new ScreenProjector(HorizontalFieldOfViewDegrees, verticalDegrees);
        LastFrame = (projector, dirtyRect.Width, dirtyRect.Height);

        var requests = new List<LabelLayout.Request<(Place Place, LineOfSight Los)>>();
        foreach (var place in places)
        {
            if (!LineOfSight.IsAboveHorizon(observer, place.Location))
                continue;

            var los = LineOfSight.Between(observer, place.Location);
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is { } point)
            {
                // Vybraný cíl kalibrace musí zůstat vidět, i kdyby byl nejméně známý.
                var importance = place == Highlighted ? int.MaxValue : place.Importance;
                requests.Add(new((place, los), point, importance));
            }
        }

        canvas.FontSize = 15;
        canvas.FontColor = Colors.White;

        foreach (var label in LabelLayout.Arrange(requests, LabelWidth, LabelHeight))
        {
            var (place, los) = label.Item;
            var box = LabelLayout.Box(label.Anchor, label.Level, LabelWidth, LabelHeight);
            var centre = new ScreenPoint((box.Left + box.Right) / 2, (box.Top + box.Bottom) / 2);

            _drawn.Add(new DrawnLabel(place, los, label.Anchor, centre));
            DrawLabel(canvas, place, los, label.Anchor, box, place == Highlighted);
        }
    }

    private static void DrawLabel(
        ICanvas canvas,
        Place place,
        LineOfSight los,
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

        var distance = los.DistanceMeters < 10_000
            ? $"{los.DistanceMeters / 1000:0.0} km"
            : $"{los.DistanceMeters / 1000:0} km";

        var left = (float)box.Left;
        var top = (float)box.Top;
        canvas.FillColor = highlighted ? Color.FromArgb("#B3712B13") : Color.FromRgba(0, 0, 0, 0.55);
        canvas.FillRoundedRectangle(left, top, LabelWidth, LabelHeight, 6);
        canvas.DrawString($"{place.Name} · {distance}", left, top, LabelWidth, LabelHeight,
            HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}

public sealed record DrawnLabel(Place Place, LineOfSight LineOfSight, ScreenPoint Point, ScreenPoint LabelCentre);
