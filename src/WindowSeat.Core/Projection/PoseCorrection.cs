using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Projection;

/// <summary>
/// Ruční oprava pózy z kalibrace na známý cíl. Pokrývá chybu kompasu, telefon natočený
/// vůči oknu i nepřesnou výšku pozorovatele; k póze se jen přičte.
/// </summary>
public readonly record struct PoseCorrection(double AzimuthDegrees, double ElevationDegrees)
{
    public static PoseCorrection None => default;

    public CameraPose ApplyTo(CameraPose pose) => pose with
    {
        AzimuthDegrees = ((pose.AzimuthDegrees + AzimuthDegrees) % 360 + 360) % 360,
        ElevationDegrees = Math.Clamp(pose.ElevationDegrees + ElevationDegrees, -90, 90),
    };

    /// <summary>
    /// Nová oprava z klepnutí: uživatel ukázal, kde na obrazovce cíl skutečně je.
    /// Pootočíme pózu tak, aby tam ukazoval i popisek.
    /// </summary>
    /// <param name="correctedPose">Póza, podle které se kreslilo, tedy už s touto opravou.</param>
    public PoseCorrection CalibrateWith(
        CameraPose correctedPose,
        ScreenProjector projector,
        ScreenPoint tapped,
        LineOfSight target,
        double screenWidth,
        double screenHeight)
    {
        var seen = projector.Unproject(correctedPose, tapped, screenWidth, screenHeight);

        // Rozdíl azimutů vždy kratší cestou kolem severu.
        var azimuthDelta = ((target.AzimuthDegrees - seen.AzimuthDegrees) % 360 + 540) % 360 - 180;
        var elevationDelta = target.ElevationDegrees - seen.ElevationDegrees;

        return new PoseCorrection(AzimuthDegrees + azimuthDelta, ElevationDegrees + elevationDelta);
    }
}
