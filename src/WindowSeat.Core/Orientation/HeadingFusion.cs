namespace WindowSeat.Core.Orientation;

/// <summary>
/// Spojuje gyroskop a kompas. Gyroskop přesně měří otáčení, ale neví, kde je sever, a pomalu ujíždí;
/// kompas sever zná, ale skáče u kovu a elektroniky. Směr proto sleduje gyroskop a ke kompasu se jen
/// pomalu dotahuje; velké odchylky kompasu bere jako rušení a skoro nevěří. Po kalibraci na známý cíl
/// se kompas odpojí úplně.
/// </summary>
public sealed class HeadingFusion
{
    /// <summary>Za jak dlouho se směr dotáhne ke kompasu (zhruba ze dvou třetin), když kompas sedí.</summary>
    public static readonly TimeSpan TimeConstant = TimeSpan.FromSeconds(10);

    /// <summary>Odchylka kompasu, nad kterou ho bereme jako rušení.</summary>
    public const double DisturbanceThresholdDegrees = 25;

    // Rušenému kompasu věříme desetkrát méně; když odchylka trvá, směr se k němu stejně časem dotáhne.
    private const double DisturbedWeight = 0.1;

    private double? _offset;
    private DateTimeOffset? _lastUpdate;

    /// <summary>Kompas odpojený kalibrací; směr drží jen gyroskop.</summary>
    public bool IsLocked { get; private set; }

    /// <summary>Poslední odchylka kompasu od výsledného směru; velká hodnota znamená rušení.</summary>
    public double CompassDeviationDegrees { get; private set; }

    /// <summary>
    /// Zpracuje nové měření a vrátí směr kamery od zeměpisného severu.
    /// </summary>
    /// <param name="gyroAzimuthDegrees">Azimut z gyroskopu; jeho nula je libovolná, důležité jsou změny.</param>
    /// <param name="compassAzimuthDegrees">Azimut z kompasu vůči zeměpisnému severu (už s deklinací), nebo <c>null</c>.</param>
    public double Update(double gyroAzimuthDegrees, double? compassAzimuthDegrees, DateTimeOffset now)
    {
        if (_offset is not { } offset)
        {
            // Bez kompasu začneme s nulou gyroskopu, kalibrace to pak opraví.
            _offset = compassAzimuthDegrees is { } first ? Wrap(first - gyroAzimuthDegrees) : 0;
            _lastUpdate = now;
            return Wrap(gyroAzimuthDegrees + _offset.Value);
        }

        var elapsed = _lastUpdate is { } last ? Math.Max(0, (now - last).TotalSeconds) : 0;
        _lastUpdate = now;

        if (compassAzimuthDegrees is { } compass)
        {
            var deviation = SignedDifference(compass, gyroAzimuthDegrees + offset);
            CompassDeviationDegrees = deviation;

            if (!IsLocked)
            {
                var weight = Math.Abs(deviation) > DisturbanceThresholdDegrees ? DisturbedWeight : 1;
                var gain = Math.Min(1, weight * elapsed / TimeConstant.TotalSeconds);
                _offset = Wrap(offset + deviation * gain);
            }
        }

        return Wrap(gyroAzimuthDegrees + _offset.Value);
    }

    /// <summary>Kalibrace: od teď platí zadaný směr a kompas se ignoruje.</summary>
    public void LockTo(double gyroAzimuthDegrees, double trueAzimuthDegrees)
    {
        _offset = Wrap(trueAzimuthDegrees - gyroAzimuthDegrees);
        IsLocked = true;
    }

    /// <summary>Zruší kalibraci; směr se znovu pomalu dotahuje ke kompasu.</summary>
    public void Unlock() => IsLocked = false;

    private static double SignedDifference(double to, double from) => ((to - from) % 360 + 540) % 360 - 180;

    private static double Wrap(double degrees) => (degrees % 360 + 360) % 360;
}
