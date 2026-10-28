namespace WindowSeat.Core.Geo;

/// <summary>
/// Jak pozorovatel vidí cíl: směr od severu, úhel nad místním obzorem a přímá vzdálenost.
/// Zakřivení Země je v úhlu zahrnuté, protože počítáme přes kartézské souřadnice WGS84.
/// </summary>
public readonly record struct LineOfSight(double AzimuthDegrees, double ElevationDegrees, double DistanceMeters)
{
    // Elipsoid WGS84.
    private const double SemiMajorAxis = 6_378_137.0;
    private const double Flattening = 1 / 298.257223563;
    private const double EccentricitySquared = Flattening * (2 - Flattening);

    // Průměrný poloměr pro odhad obzoru; na pár procent přesnosti tu nezáleží.
    private const double MeanEarthRadius = 6_371_000.0;

    public static LineOfSight Between(GeoPoint observer, GeoPoint target)
    {
        var (ox, oy, oz) = ToEcef(observer);
        var (tx, ty, tz) = ToEcef(target);
        var (dx, dy, dz) = (tx - ox, ty - oy, tz - oz);

        // Převod rozdílu do místních souřadnic pozorovatele: východ, sever, nahoru.
        var (sinLat, cosLat) = Math.SinCos(ToRadians(observer.LatitudeDegrees));
        var (sinLon, cosLon) = Math.SinCos(ToRadians(observer.LongitudeDegrees));

        var east = -sinLon * dx + cosLon * dy;
        var north = -sinLat * cosLon * dx - sinLat * sinLon * dy + cosLat * dz;
        var up = cosLat * cosLon * dx + cosLat * sinLon * dy + sinLat * dz;

        var horizontal = Math.Sqrt(east * east + north * north);
        var azimuth = (ToDegrees(Math.Atan2(east, north)) + 360) % 360;
        var elevation = ToDegrees(Math.Atan2(up, horizontal));

        return new LineOfSight(azimuth, elevation, Math.Sqrt(horizontal * horizontal + up * up));
    }

    /// <summary>
    /// Zda cíl nezakrývá zakřivení Země (terén mezi nimi zatím nebereme v úvahu).
    /// Oba body vidí do vzdálenosti svého obzoru; cíl je vidět, když se jejich obzory překrývají.
    /// </summary>
    public static bool IsAboveHorizon(GeoPoint observer, GeoPoint target)
    {
        var reach = HorizonDistance(observer.AltitudeMeters) + HorizonDistance(target.AltitudeMeters);
        return GroundDistance(observer, target) <= reach;
    }

    /// <summary>Vzdálenost k obzoru z dané výšky, měřená po povrchu.</summary>
    public static double HorizonDistance(double altitudeMeters)
    {
        var h = Math.Max(0, altitudeMeters);
        return Math.Sqrt(2 * MeanEarthRadius * h + h * h);
    }

    /// <summary>Vzdálenost po povrchu (haversine na kouli).</summary>
    public static double GroundDistance(GeoPoint a, GeoPoint b)
    {
        var lat1 = ToRadians(a.LatitudeDegrees);
        var lat2 = ToRadians(b.LatitudeDegrees);
        var dLat = lat2 - lat1;
        var dLon = ToRadians(b.LongitudeDegrees - a.LongitudeDegrees);

        var h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * MeanEarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static (double X, double Y, double Z) ToEcef(GeoPoint point)
    {
        var (sinLat, cosLat) = Math.SinCos(ToRadians(point.LatitudeDegrees));
        var (sinLon, cosLon) = Math.SinCos(ToRadians(point.LongitudeDegrees));

        var primeVerticalRadius = SemiMajorAxis / Math.Sqrt(1 - EccentricitySquared * sinLat * sinLat);
        var r = (primeVerticalRadius + point.AltitudeMeters) * cosLat;

        return (r * cosLon, r * sinLon, (primeVerticalRadius * (1 - EccentricitySquared) + point.AltitudeMeters) * sinLat);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    private static double ToDegrees(double radians) => radians * 180 / Math.PI;
}
