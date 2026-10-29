using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Projection;

/// <summary>
/// Ruční oprava pózy z kalibrace na známý cíl. Pokrývá chybu kompasu, telefon natočený
/// vůči oknu i nepřesnou výšku pozorovatele; k póze se jen přičte.
/// </summary>
public readonly record struct PoseCorrection
{
    /// <summary>
    /// Největší rozumná oprava výšky pohledu. Senzor náklonu se mýlí o jednotky stupňů, ne o desítky; víc znamená
    /// omylem tažený prst. Bez stropu se oprava nasčítala až na −97° a appka „koukala“ do země.
    /// </summary>
    public const double MaxElevationDegrees = 10;

    public PoseCorrection(double azimuthDegrees, double elevationDegrees)
    {
        AzimuthDegrees = ((azimuthDegrees + 180) % 360 + 360) % 360 - 180;
        ElevationDegrees = Math.Clamp(elevationDegrees, -MaxElevationDegrees, MaxElevationDegrees);
    }

    /// <summary>Oprava směru, −180° až 180°.</summary>
    public double AzimuthDegrees { get; }

    /// <summary>Oprava výšky pohledu, nejvýš ±<see cref="MaxElevationDegrees"/>.</summary>
    public double ElevationDegrees { get; }

    public static PoseCorrection None => default;

    public CameraPose ApplyTo(CameraPose pose) => pose with
    {
        AzimuthDegrees = ((pose.AzimuthDegrees + AzimuthDegrees) % 360 + 360) % 360,
        ElevationDegrees = Math.Clamp(pose.ElevationDegrees + ElevationDegrees, -90, 90),
    };

    /// <summary>
    /// Oprava z tažení prstem: uživatel posunul popisky o kus obrazovky na skutečná místa.
    /// Posun popisků doprava znamená, že kamera ve skutečnosti míří víc doleva, než si appka myslí.
    /// </summary>
    /// <param name="focalLengthPixels">Ohnisko v bodech obrazovky: polovina šířky / tan(polovina zorného úhlu).</param>
    public PoseCorrection Dragged(double dxPixels, double dyPixels, double focalLengthPixels) => new(
        AzimuthDegrees - Math.Atan(dxPixels / focalLengthPixels) * 180 / Math.PI,
        ElevationDegrees + Math.Atan(dyPixels / focalLengthPixels) * 180 / Math.PI);

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
