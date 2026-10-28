namespace WindowSeat.Core.Geo;

/// <summary>Zjednodušení lomené čáry (Douglas–Peucker): vynechá body, které od tvaru neodchylují víc než tolerance.</summary>
public static class Polyline
{
    public static IReadOnlyList<GeoPoint> Simplify(IReadOnlyList<GeoPoint> points, double toleranceMeters)
    {
        if (points.Count < 3)
            return points;

        // Na pár kilometrech stačí rovinná aproximace kolem prvního bodu.
        var origin = points[0];
        var metersPerDegreeLat = 111_320.0;
        var metersPerDegreeLon = 111_320.0 * Math.Cos(origin.LatitudeDegrees * Math.PI / 180);
        var xy = points
            .Select(p => ((p.LongitudeDegrees - origin.LongitudeDegrees) * metersPerDegreeLon, (p.LatitudeDegrees - origin.LatitudeDegrees) * metersPerDegreeLat))
            .ToArray();

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        Mark(xy, 0, points.Count - 1, toleranceMeters, keep);

        return points.Where((_, i) => keep[i]).ToList();
    }

    private static void Mark((double X, double Y)[] xy, int first, int last, double tolerance, bool[] keep)
    {
        var farthest = -1;
        var maxDistance = tolerance;
        for (var i = first + 1; i < last; i++)
        {
            var distance = DistanceToSegment(xy[i], xy[first], xy[last]);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthest = i;
            }
        }

        if (farthest < 0)
            return;

        keep[farthest] = true;
        Mark(xy, first, farthest, tolerance, keep);
        Mark(xy, farthest, last, tolerance, keep);
    }

    private static double DistanceToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var (dx, dy) = (b.X - a.X, b.Y - a.Y);
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        var (cx, cy) = (a.X + t * dx, a.Y + t * dy);
        return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
    }
}
