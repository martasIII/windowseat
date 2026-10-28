namespace WindowSeat.Core.Projection;

/// <summary>
/// Zorné úhly objektivu přes celý snímek: podél delší a kratší strany snímače.
/// </summary>
public readonly record struct LensFieldOfView(double LongSideDegrees, double ShortSideDegrees)
{
    /// <summary>Z ohniskové vzdálenosti a fyzické velikosti snímače (obojí v mm), jak je hlásí systém.</summary>
    public static LensFieldOfView FromSensor(double focalLengthMm, double sensorWidthMm, double sensorHeightMm)
    {
        var longSide = Math.Max(sensorWidthMm, sensorHeightMm);
        var shortSide = Math.Min(sensorWidthMm, sensorHeightMm);
        return new LensFieldOfView(Angle(longSide, focalLengthMm), Angle(shortSide, focalLengthMm));
    }

    /// <summary>
    /// Vodorovný zorný úhel přes šířku obrazovky na výšku, když náhled vyplní celou plochu a přečnívající okraje ořízne.
    /// Delší strana snímače jde na výšku obrazovky.
    /// </summary>
    public double VisibleHorizontalDegrees(double viewWidth, double viewHeight)
    {
        var halfShort = Math.Tan(ToRadians(ShortSideDegrees) / 2);
        var halfLong = Math.Tan(ToRadians(LongSideDegrees) / 2);

        // Užší obrazovka než snímek: výška se vejde celá a po stranách se ořízne.
        var halfVisible = Math.Min(halfShort, halfLong * viewWidth / viewHeight);
        return 2 * Math.Atan(halfVisible) * 180 / Math.PI;
    }

    /// <summary>
    /// Zorný úhel po přiblížení: zoom zmenší tangens polovičního úhlu, ne úhel samotný
    /// (2× zoom z 34° dá 17,3°, ne 17°).
    /// </summary>
    public static double Zoomed(double degrees, double zoom) =>
        2 * Math.Atan(Math.Tan(ToRadians(degrees) / 2) / Math.Max(1, zoom)) * 180 / Math.PI;

    private static double Angle(double sizeMm, double focalLengthMm) => 2 * Math.Atan(sizeMm / (2 * focalLengthMm)) * 180 / Math.PI;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
