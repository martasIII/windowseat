using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Projection;

/// <summary>
/// Promítá směry na obrazovku jako dírková komora: střed obrazovky leží na ose kamery,
/// okraje odpovídají polovině zorného úhlu.
/// </summary>
/// <param name="HorizontalFieldOfViewDegrees">Zorný úhel mezi levým a pravým okrajem obrazovky.</param>
/// <param name="VerticalFieldOfViewDegrees">Zorný úhel mezi horním a dolním okrajem obrazovky.</param>
public sealed record ScreenProjector(double HorizontalFieldOfViewDegrees, double VerticalFieldOfViewDegrees)
{
    /// <summary>
    /// Bod na obrazovce o rozměrech <paramref name="screenWidth"/> × <paramref name="screenHeight"/> (počátek vlevo nahoře),
    /// nebo <c>null</c>, když je cíl za kamerou nebo mimo záběr.
    /// </summary>
    public ScreenPoint? Project(CameraPose camera, LineOfSight target, double screenWidth, double screenHeight)
    {
        var direction = Direction(target.AzimuthDegrees, target.ElevationDegrees);
        var (right, up, forward) = CameraAxes(camera);

        var depth = Dot(direction, forward);
        if (depth <= 0)
            return null;

        var x = Dot(direction, right) / depth / Math.Tan(ToRadians(HorizontalFieldOfViewDegrees) / 2);
        var y = Dot(direction, up) / depth / Math.Tan(ToRadians(VerticalFieldOfViewDegrees) / 2);
        if (Math.Abs(x) > 1 || Math.Abs(y) > 1)
            return null;

        return new ScreenPoint(screenWidth / 2 * (1 + x), screenHeight / 2 * (1 - y));
    }

    /// <summary>Opačný směr: kam ve světě míří daný bod obrazovky.</summary>
    public SkyDirection Unproject(CameraPose camera, ScreenPoint point, double screenWidth, double screenHeight)
    {
        var (right, up, forward) = CameraAxes(camera);

        var x = (2 * point.X / screenWidth - 1) * Math.Tan(ToRadians(HorizontalFieldOfViewDegrees) / 2);
        var y = (1 - 2 * point.Y / screenHeight) * Math.Tan(ToRadians(VerticalFieldOfViewDegrees) / 2);
        var direction = forward + right * x + up * y;
        var length = Math.Sqrt(Dot(direction, direction));

        var azimuth = (Math.Atan2(direction.X, direction.Y) * 180 / Math.PI + 360) % 360;
        var elevation = Math.Asin(direction.Z / length) * 180 / Math.PI;
        return new SkyDirection(azimuth, elevation);
    }

    /// <summary>
    /// Promítne mnohoúhelník (třeba plochu řeky) na obrazovku. Body můžou ležet i mimo obrazovku; část za kamerou
    /// se ořízne těsně před ní, jinak by se promítla převráceně. Prázdný výsledek = celé je za zády.
    /// </summary>
    public IReadOnlyList<ScreenPoint> ProjectPolygon(CameraPose camera, IReadOnlyList<LineOfSight> ring, double screenWidth, double screenHeight)
    {
        var (right, up, forward) = CameraAxes(camera);

        // Souřadnice kamery v metrech: doprava, nahoru, dopředu. Ořez se dělá na skutečných polohách, ne na směrech.
        var points = ring
            .Select(los =>
            {
                var d = Direction(los.AzimuthDegrees, los.ElevationDegrees) * los.DistanceMeters;
                return new Vector(Dot(d, right), Dot(d, up), Dot(d, forward));
            })
            .ToList();

        const double near = 1;
        var clipped = new List<Vector>();
        for (var i = 0; i < points.Count; i++)
        {
            var current = points[i];
            var next = points[(i + 1) % points.Count];
            var currentInside = current.Z >= near;
            var nextInside = next.Z >= near;

            if (currentInside)
                clipped.Add(current);
            if (currentInside != nextInside)
            {
                var t = (near - current.Z) / (next.Z - current.Z);
                clipped.Add(current + (next + current * -1) * t);
            }
        }

        var scaleX = screenWidth / 2 / Math.Tan(ToRadians(HorizontalFieldOfViewDegrees) / 2);
        var scaleY = screenHeight / 2 / Math.Tan(ToRadians(VerticalFieldOfViewDegrees) / 2);
        return clipped
            .Select(p => new ScreenPoint(screenWidth / 2 + p.X / p.Z * scaleX, screenHeight / 2 - p.Y / p.Z * scaleY))
            .ToList();
    }

    private static (Vector Right, Vector Up, Vector Forward) CameraAxes(CameraPose camera)
    {
        var forward = Direction(camera.AzimuthDegrees, camera.ElevationDegrees);

        // Bez náklonu míří pravá hrana vodorovně doprava od směru pohledu a horní hrana kolmo na obě.
        var (sinAz, cosAz) = Math.SinCos(ToRadians(camera.AzimuthDegrees));
        var levelRight = new Vector(cosAz, -sinAz, 0);
        var levelUp = Cross(levelRight, forward);

        // Náklon otočí obě osy kolem osy kamery; kladný zvedá pravou hranu.
        var (sinRoll, cosRoll) = Math.SinCos(ToRadians(camera.RollDegrees));
        var right = levelRight * cosRoll + levelUp * sinRoll;
        var up = levelUp * cosRoll + levelRight * -sinRoll;

        return (right, up, forward);
    }

    // Jednotkový vektor ve světových souřadnicích východ, sever, nahoru.
    private static Vector Direction(double azimuthDegrees, double elevationDegrees)
    {
        var (sinAz, cosAz) = Math.SinCos(ToRadians(azimuthDegrees));
        var (sinEl, cosEl) = Math.SinCos(ToRadians(elevationDegrees));
        return new Vector(cosEl * sinAz, cosEl * cosAz, sinEl);
    }

    private static double Dot(Vector a, Vector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static Vector Cross(Vector a, Vector b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private readonly record struct Vector(double X, double Y, double Z)
    {
        public static Vector operator *(Vector v, double k) => new(v.X * k, v.Y * k, v.Z * k);

        public static Vector operator +(Vector a, Vector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
}

/// <summary>Bod na obrazovce, počátek vlevo nahoře.</summary>
public readonly record struct ScreenPoint(double X, double Y);

/// <summary>Směr ve světě: azimut od severu a úhel nad obzorem, ve stupních.</summary>
public readonly record struct SkyDirection(double AzimuthDegrees, double ElevationDegrees);
