using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Sky;

public enum CelestialBody
{
    Sun,
    Moon,
}

/// <summary>Rektascenze a deklinace ve stupních, vzdálenost v poloměrech Země.</summary>
public readonly record struct EquatorialPosition(double RightAscensionDegrees, double DeclinationDegrees, double DistanceEarthRadii);

/// <summary>
/// Poloha Slunce a Měsíce na obloze pro kalibraci směru. Algoritmus Paula Schlytera („How to compute
/// planetary positions“) s hlavními poruchami Měsíce; přesnost kolem 1–2 obloukových minut, na kalibraci
/// telefonu víc než dost. Pro Měsíc počítá i paralaxu (z povrchu Země je až o 1° níž než ze středu)
/// a pro oba atmosférickou refrakci, která je u obzoru zvedá o půl stupně.
/// </summary>
public static class Ephemeris
{
    /// <summary>Kde pozorovatel vidí těleso: azimut, výška nad obzorem (se zdánlivým zvednutím refrakcí) a vzdálenost.</summary>
    public static LineOfSight Observe(CelestialBody body, GeoPoint observer, DateTimeOffset time)
    {
        var d = DayNumber(time);
        var position = Equatorial(body, time);

        // Místní hvězdný čas: střední délka Slunce + 180° + světový čas + zeměpisná délka.
        var sun = SunOrbit(d);
        var utHours = time.UtcDateTime.TimeOfDay.TotalHours;
        var siderealDegrees = Normalize(sun.MeanLongitude + 180 + utHours * 15 + observer.LongitudeDegrees);
        var hourAngle = Rad(siderealDegrees - position.RightAscensionDegrees);

        var dec = Rad(position.DeclinationDegrees);
        var lat = Rad(observer.LatitudeDegrees);
        var x = Math.Cos(hourAngle) * Math.Cos(dec);
        var y = Math.Sin(hourAngle) * Math.Cos(dec);
        var z = Math.Sin(dec);

        var xHorizon = x * Math.Sin(lat) - z * Math.Cos(lat);
        var zHorizon = x * Math.Cos(lat) + z * Math.Sin(lat);

        var azimuth = Normalize(Deg(Math.Atan2(y, xHorizon)) + 180);
        var altitude = Deg(Math.Asin(Math.Clamp(zHorizon, -1, 1)));

        if (body == CelestialBody.Moon)
            altitude -= Deg(Math.Asin(1 / position.DistanceEarthRadii)) * Math.Cos(Rad(altitude));

        return new LineOfSight(azimuth, altitude + Refraction(altitude), position.DistanceEarthRadii * 6_378_137);
    }

    /// <summary>Geocentrická rovníková poloha (bez paralaxy a refrakce).</summary>
    public static EquatorialPosition Equatorial(CelestialBody body, DateTimeOffset time)
    {
        var d = DayNumber(time);
        var (longitude, latitude, distance) = body == CelestialBody.Sun ? SunEcliptic(d) : MoonEcliptic(d);

        var obliquity = Rad(23.4393 - 3.563e-7 * d);
        var x = Math.Cos(Rad(longitude)) * Math.Cos(Rad(latitude));
        var y = Math.Sin(Rad(longitude)) * Math.Cos(Rad(latitude));
        var z = Math.Sin(Rad(latitude));

        var yEquatorial = y * Math.Cos(obliquity) - z * Math.Sin(obliquity);
        var zEquatorial = y * Math.Sin(obliquity) + z * Math.Cos(obliquity);

        return new EquatorialPosition(
            Normalize(Deg(Math.Atan2(yEquatorial, x))),
            Deg(Math.Atan2(zEquatorial, Math.Sqrt(x * x + yEquatorial * yEquatorial))),
            distance);
    }

    // Dny od 31. 12. 1999 0:00 UT, jak je používá Schlyter.
    private static double DayNumber(DateTimeOffset time) =>
        (time.UtcDateTime - new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc)).TotalDays;

    private static (double MeanAnomaly, double Perihelion, double MeanLongitude) SunOrbit(double d)
    {
        var perihelion = 282.9404 + 4.70935e-5 * d;
        var meanAnomaly = Normalize(356.0470 + 0.9856002585 * d);
        return (meanAnomaly, perihelion, Normalize(meanAnomaly + perihelion));
    }

    private static (double Longitude, double Latitude, double DistanceEarthRadii) SunEcliptic(double d)
    {
        var (m, w, _) = SunOrbit(d);
        var e = 0.016709 - 1.151e-9 * d;

        var eccentricAnomaly = m + Deg(e * Math.Sin(Rad(m)) * (1 + e * Math.Cos(Rad(m))));
        var xv = Math.Cos(Rad(eccentricAnomaly)) - e;
        var yv = Math.Sqrt(1 - e * e) * Math.Sin(Rad(eccentricAnomaly));

        var trueAnomaly = Deg(Math.Atan2(yv, xv));
        var distanceAu = Math.Sqrt(xv * xv + yv * yv);
        return (Normalize(trueAnomaly + w), 0, distanceAu * 23_455);
    }

    private static (double Longitude, double Latitude, double DistanceEarthRadii) MoonEcliptic(double d)
    {
        var node = Rad(Normalize(125.1228 - 0.0529538083 * d));
        var inclination = Rad(5.1454);
        var perigee = Normalize(318.0634 + 0.1643573223 * d);
        const double a = 60.2666;
        const double e = 0.054900;
        var m = Normalize(115.3654 + 13.0649929509 * d);

        // Keplerova rovnice iterací; pro výstřednost Měsíce stačí pár kroků.
        var eccentricAnomaly = m + Deg(e * Math.Sin(Rad(m)) * (1 + e * Math.Cos(Rad(m))));
        for (var i = 0; i < 5; i++)
            eccentricAnomaly -= (eccentricAnomaly - Deg(e * Math.Sin(Rad(eccentricAnomaly))) - m) / (1 - e * Math.Cos(Rad(eccentricAnomaly)));

        var xv = a * (Math.Cos(Rad(eccentricAnomaly)) - e);
        var yv = a * Math.Sqrt(1 - e * e) * Math.Sin(Rad(eccentricAnomaly));
        var v = Rad(Deg(Math.Atan2(yv, xv)) + perigee);
        var r = Math.Sqrt(xv * xv + yv * yv);

        var xh = r * (Math.Cos(node) * Math.Cos(v) - Math.Sin(node) * Math.Sin(v) * Math.Cos(inclination));
        var yh = r * (Math.Sin(node) * Math.Cos(v) + Math.Cos(node) * Math.Sin(v) * Math.Cos(inclination));
        var zh = r * Math.Sin(v) * Math.Sin(inclination);

        var longitude = Deg(Math.Atan2(yh, xh));
        var latitude = Deg(Math.Atan2(zh, Math.Sqrt(xh * xh + yh * yh)));

        // Hlavní poruchy od Slunce.
        var (sunAnomaly, sunPerihelion, _) = SunOrbit(d);
        var sunLongitude = sunAnomaly + sunPerihelion;
        var moonLongitude = m + perigee + Deg(node);
        var elongation = Rad(moonLongitude - sunLongitude);
        var latitudeArgument = Rad(moonLongitude - Deg(node));
        var mm = Rad(m);
        var ms = Rad(sunAnomaly);

        longitude += -1.274 * Math.Sin(mm - 2 * elongation)
            + 0.658 * Math.Sin(2 * elongation)
            - 0.186 * Math.Sin(ms)
            - 0.059 * Math.Sin(2 * mm - 2 * elongation)
            - 0.057 * Math.Sin(mm - 2 * elongation + ms)
            + 0.053 * Math.Sin(mm + 2 * elongation)
            + 0.046 * Math.Sin(2 * elongation - ms)
            + 0.041 * Math.Sin(mm - ms)
            - 0.035 * Math.Sin(elongation)
            - 0.031 * Math.Sin(mm + ms)
            - 0.015 * Math.Sin(2 * latitudeArgument - 2 * elongation)
            + 0.011 * Math.Sin(mm - 4 * elongation);

        latitude += -0.173 * Math.Sin(latitudeArgument - 2 * elongation)
            - 0.055 * Math.Sin(mm - latitudeArgument - 2 * elongation)
            - 0.046 * Math.Sin(mm + latitudeArgument - 2 * elongation)
            + 0.033 * Math.Sin(latitudeArgument + 2 * elongation)
            + 0.017 * Math.Sin(2 * mm + latitudeArgument);

        r += -0.58 * Math.Cos(mm - 2 * elongation) - 0.46 * Math.Cos(2 * elongation);

        return (Normalize(longitude), latitude, r);
    }

    /// <summary>Zdánlivé zvednutí refrakcí (Saemundsson) ve stupních; pod obzorem už nepočítáme.</summary>
    private static double Refraction(double altitudeDegrees)
    {
        if (altitudeDegrees < -1)
            return 0;

        var arcMinutes = 1.02 / Math.Tan(Rad(altitudeDegrees + 10.3 / (altitudeDegrees + 5.11)));
        return arcMinutes / 60;
    }

    private static double Rad(double degrees) => degrees * Math.PI / 180;

    private static double Deg(double radians) => radians * 180 / Math.PI;

    private static double Normalize(double degrees) => (degrees % 360 + 360) % 360;
}
