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
