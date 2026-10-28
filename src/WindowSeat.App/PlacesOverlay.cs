using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

/// <summary>Kreslí popisky míst přes náhled kamery podle aktuální pózy a polohy.</summary>
public sealed class PlacesOverlay(IReadOnlyList<Place> places) : IDrawable
{
    public CameraPose? Pose { get; set; }

    public GeoPoint? Observer { get; set; }

    public double HorizontalFieldOfViewDegrees { get; set; } = 50;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Pose is not { } pose || Observer is not { } observer || dirtyRect.Width <= 0)
            return;

        // Svislý úhel dopočítáme z poměru stran, pixely náhledu jsou čtvercové.
        var halfHorizontal = HorizontalFieldOfViewDegrees * Math.PI / 360;
        var verticalDegrees = 2 * Math.Atan(Math.Tan(halfHorizontal) * dirtyRect.Height / dirtyRect.Width) * 180 / Math.PI;
        var projector = new ScreenProjector(HorizontalFieldOfViewDegrees, verticalDegrees);

        canvas.FontSize = 15;
        canvas.FontColor = Colors.White;

        foreach (var place in places)
        {
            if (!LineOfSight.IsAboveHorizon(observer, place.Location))
                continue;

            var los = LineOfSight.Between(observer, place.Location);
            if (projector.Project(pose, los, dirtyRect.Width, dirtyRect.Height) is not { } point)
                continue;

            DrawLabel(canvas, place, los, (float)point.X, (float)point.Y);
        }
    }

    private static void DrawLabel(ICanvas canvas, Place place, LineOfSight los, float x, float y)
    {
        var color = place.Kind switch
        {
            PlaceKind.Peak => Color.FromArgb("#EF9F27"),
            PlaceKind.Landmark => Color.FromArgb("#5DCAA5"),
            _ => Colors.White,
        };

        // Svislá čárka přesně na cíl, popisek nad ní, ať nezakrývá to, na co ukazuje.
        canvas.StrokeColor = color;
        canvas.StrokeSize = 2;
        canvas.DrawLine(x, y, x, y - 28);
        canvas.FillColor = color;
        canvas.FillCircle(x, y, 3);

        var distance = los.DistanceMeters < 10_000
            ? $"{los.DistanceMeters / 1000:0.0} km"
            : $"{los.DistanceMeters / 1000:0} km";
        var text = $"{place.Name} · {distance}";

        canvas.FillColor = Color.FromRgba(0, 0, 0, 0.55);
        canvas.FillRoundedRectangle(x - 110, y - 56, 220, 24, 6);
        canvas.DrawString(text, x - 110, y - 56, 220, 24, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
