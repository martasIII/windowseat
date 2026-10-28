using WindowSeat.Core.Places;

namespace WindowSeat.App;

/// <summary>
/// Jednoduché piktogramy podle vrstvy: vrchol trojúhelník, kostel kříž, věž štíhlá věžička, hrad cimbuří,
/// most oblouk, ostatní stavby čtverec. Vždy s bílým lemem, aby byly vidět na obloze i ve stínu.
/// </summary>
public static class MarkerIcons
{
    public static void Draw(ICanvas canvas, PlaceKind kind, float x, float y, float size, Color color)
    {
        var s = size / 2;
        switch (kind)
        {
            case PlaceKind.Peak:
                FillWithHalo(canvas, Polygon((x, y - s), (x + s, y + s * 0.8f), (x - s, y + s * 0.8f)), color);
                break;

            case PlaceKind.Church:
                StrokeWithHalo(canvas, color, s * 0.45f,
                    (new PointF(x, y - s), new PointF(x, y + s)),
                    (new PointF(x - s * 0.6f, y - s * 0.35f), new PointF(x + s * 0.6f, y - s * 0.35f)));
                break;

            case PlaceKind.Tower:
                FillWithHalo(canvas, Polygon(
                    (x, y - s * 1.2f), (x + s * 0.4f, y - s * 0.6f), (x + s * 0.4f, y + s),
                    (x - s * 0.4f, y + s), (x - s * 0.4f, y - s * 0.6f)), color);
                break;

            case PlaceKind.Castle:
                FillWithHalo(canvas, Polygon(
                    (x - s, y + s * 0.8f), (x - s, y - s * 0.8f), (x - s * 0.5f, y - s * 0.8f), (x - s * 0.5f, y - s * 0.3f),
                    (x - s * 0.2f, y - s * 0.3f), (x - s * 0.2f, y - s * 0.8f), (x + s * 0.2f, y - s * 0.8f), (x + s * 0.2f, y - s * 0.3f),
                    (x + s * 0.5f, y - s * 0.3f), (x + s * 0.5f, y - s * 0.8f), (x + s, y - s * 0.8f), (x + s, y + s * 0.8f)), color);
                break;

            case PlaceKind.Bridge:
                var arc = new PathF();
                arc.MoveTo(x - s, y + s * 0.5f);
                arc.QuadTo(x, y - s * 1.1f, x + s, y + s * 0.5f);
                StrokePathWithHalo(canvas, arc, color, s * 0.4f);
                break;

            case PlaceKind.CelestialBody:
                canvas.FillColor = Colors.White;
                canvas.FillCircle(x, y, s + 2);
                canvas.FillColor = color;
                canvas.FillCircle(x, y, s);
                break;

            default:
                FillWithHalo(canvas, Polygon((x - s * 0.8f, y - s * 0.8f), (x + s * 0.8f, y - s * 0.8f), (x + s * 0.8f, y + s * 0.8f), (x - s * 0.8f, y + s * 0.8f)), color);
                break;
        }
    }

    private static PathF Polygon(params (float X, float Y)[] points)
    {
        var path = new PathF();
        path.MoveTo(points[0].X, points[0].Y);
        foreach (var (px, py) in points.Skip(1))
            path.LineTo(px, py);
        path.Close();
        return path;
    }

    private static void FillWithHalo(ICanvas canvas, PathF path, Color color)
    {
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = 4;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(path);
        canvas.FillColor = color;
        canvas.FillPath(path);
    }

    private static void StrokeWithHalo(ICanvas canvas, Color color, float width, params (PointF From, PointF To)[] lines)
    {
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = width + 4;
        foreach (var (from, to) in lines)
            canvas.DrawLine(from, to);

        canvas.StrokeColor = color;
        canvas.StrokeSize = width;
        foreach (var (from, to) in lines)
            canvas.DrawLine(from, to);
    }

    private static void StrokePathWithHalo(ICanvas canvas, PathF path, Color color, float width)
    {
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeColor = Colors.White;
        canvas.StrokeSize = width + 4;
        canvas.DrawPath(path);
        canvas.StrokeColor = color;
        canvas.StrokeSize = width;
        canvas.DrawPath(path);
    }
}
