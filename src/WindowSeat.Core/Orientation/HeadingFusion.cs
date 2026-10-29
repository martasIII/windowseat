namespace WindowSeat.Core.Orientation;

/// <summary>
/// Spojuje gyroskop a kompas. Gyroskop přesně měří otáčení, ale neví, kde je sever, a pomalu ujíždí;
/// kompas sever zná, ale skáče u kovu a elektroniky. Směr proto sleduje gyroskop a ke kompasu se jen
/// pomalu dotahuje; velké odchylky kompasu bere jako rušení a skoro nevěří. Po kalibraci na známý cíl
/// se kompas odpojí.
/// </summary>
/// <remarks>
/// Bez zámku platí výjimka: když kompas nesouhlasí dlouho, hodně a přitom stabilně, chyba není v kompasu, ale v nás
/// (start doma u počítače s rozhozeným kompasem a pak cesta ven). Rušení u kovu kompasem cuká, tohle ne; směr se pak
/// srovná podle kompasu hned, ne až za minuty. Zámek z kalibrace tahle výjimka neruší: na ocelové rozhledně je kompas
/// vedle stabilně a kalibrace má platit. Zámek uvolní appka, když se člověk od místa kalibrace vzdálí.
/// </remarks>
public sealed class HeadingFusion
{
    /// <summary>Za jak dlouho se směr dotáhne ke kompasu (zhruba ze dvou třetin), když kompas sedí.</summary>
    public static readonly TimeSpan TimeConstant = TimeSpan.FromSeconds(10);

    /// <summary>Odchylka kompasu, nad kterou ho bereme jako rušení.</summary>
    public const double DisturbanceThresholdDegrees = 25;

    // Rušenému kompasu věříme desetkrát méně; když odchylka trvá, směr se k němu stejně časem dotáhne.
    private const double DisturbedWeight = 0.1;

    /// <summary>Jak dlouho musí kompas stabilně nesouhlasit, než mu dáme přednost.</summary>
    public static readonly TimeSpan ResyncAfter = TimeSpan.FromSeconds(5);

    // Rozptyl odchylky, pod kterým kompas považujeme za stabilní. Při otáčení telefonem kompas zaostává za gyroskopem
    // a odchylka kolísá o víc stupňů (na Letné 29. 10. o ~15°); rušení u počítače ale skáče o desítky.
    private const double StableSpreadDegrees = 20;

    private readonly Queue<(DateTimeOffset Time, double Deviation)> _recent = new();
    private double? _offset;
    private DateTimeOffset? _lastUpdate;

    /// <summary>Kolikrát se směr srovnal podle kompasu kvůli stabilnímu nesouhlasu; appka podle změny zruší starou kalibraci.</summary>
    public int Resyncs { get; private set; }

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

            if (IsStableDisagreement(deviation, now) && !IsLocked)
            {
                _offset = Wrap(offset + deviation);
                Resyncs++;
                _recent.Clear();
                CompassDeviationDegrees = 0;
                return Wrap(gyroAzimuthDegrees + _offset.Value);
            }

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
        _recent.Clear();
    }

    private bool IsStableDisagreement(double deviation, DateTimeOffset now)
    {
        _recent.Enqueue((now, deviation));
        while (_recent.Count > 0 && now - _recent.Peek().Time > ResyncAfter)
            _recent.Dequeue();

        // Okno musí pokrýt skoro celou dobu, odchylka v něm být pořád velká a jedním směrem, a málo kolísat.
        var span = now - _recent.Peek().Time;
        if (span < ResyncAfter * 0.9 || _recent.Any(r => Math.Abs(r.Deviation) <= DisturbanceThresholdDegrees))
            return false;

        var min = _recent.Min(r => r.Deviation);
        var max = _recent.Max(r => r.Deviation);
        return max - min <= StableSpreadDegrees && Math.Sign(min) == Math.Sign(max);
    }

    /// <summary>Zruší kalibraci; směr se znovu řídí kompasem (při stabilním nesouhlasu hned, jinak pomalu).</summary>
    public void Unlock()
    {
        IsLocked = false;
        _recent.Clear();
    }

    private static double SignedDifference(double to, double from) => ((to - from) % 360 + 540) % 360 - 180;

    private static double Wrap(double degrees) => (degrees % 360 + 360) % 360;
}
