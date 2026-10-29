using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Orientation;

public enum WalkStatus
{
    /// <summary>Sbírá se; ještě málo ušlé vzdálenosti.</summary>
    Walking,

    /// <summary>Hotovo; <see cref="WalkResult.CorrectionDegrees"/> je oprava směru.</summary>
    Done,

    /// <summary>Nevyšlo (křivá chůze, kymácení, slabé GPS); sbírá se znovu od teď.</summary>
    Restarted,
}

/// <param name="CorrectionDegrees">O kolik je skutečný směr napravo od toho, co appka ukazovala (jen u <see cref="WalkStatus.Done"/>).</param>
/// <param name="Reason">Proč se začíná znovu (jen u <see cref="WalkStatus.Restarted"/>).</param>
public sealed record WalkResult(WalkStatus Status, double DistanceMeters, double CorrectionDegrees = 0, string? Reason = null);

/// <summary>
/// Kalibrace chůzí: uživatel jde rovně s telefonem kamerou dopředu. Směr chůze z poloh GPS je přesný a nezávislý
/// na magnetismu; rozdíl proti tomu, kam podle appky míří kamera, je chyba kompasu. Funguje i v ulici mezi domy,
/// kde je kompas o desítky stupňů vedle (Celetná 29. 10.: −20°).
/// </summary>
public sealed class WalkCalibration
{
    /// <summary>Kolik metrů rovně je potřeba ujít.</summary>
    public const double RequiredDistanceMeters = 15;

    // Body chůze smí být nejvýš tolik od přímky začátek–konec; víc je zatáčka nebo nepřesné GPS.
    private const double MaxSideDeviationMeters = 5;

    // Telefon se smí kymácet nejvýš o tolik (kruhová směrodatná odchylka směru kamery).
    private const double MaxHeadingSpreadDegrees = 12;

    // Poloha s horší přesností se nebere.
    private const double MaxAccuracyMeters = 15;

    // Kamera zhruba vodorovně; při pohledu do země nebo do nebe směr kamery nic neříká.
    private const double MaxPitchDegrees = 40;

    private readonly List<GeoPoint> _fixes = [];
    private readonly List<double> _headings = [];

    /// <summary>Kam podle appky míří kamera (bez opravy z kalibrace), průběžně.</summary>
    public void AddHeading(double cameraAzimuthDegrees, double elevationDegrees)
    {
        if (Math.Abs(elevationDegrees) <= MaxPitchDegrees)
            _headings.Add(cameraAzimuthDegrees);
    }

    /// <summary>Nová poloha z GPS; vrací, jak je kalibrace daleko.</summary>
    public WalkResult AddFix(GeoPoint position, double? accuracyMeters)
    {
        if (accuracyMeters is not { } accuracy || accuracy > MaxAccuracyMeters)
            return Restart("GPS je nepřesná, počkej na lepší signál.");

        _fixes.Add(position);
        var start = _fixes[0];
        var distance = LineOfSight.GroundDistance(start, position);
        if (distance < RequiredDistanceMeters)
            return new WalkResult(WalkStatus.Walking, distance);

        if (MaxSideDeviation(start, position) > MaxSideDeviationMeters)
            return Restart("Chůze nebyla rovná, zkus to znovu rovně.");

        var (mean, spread) = CircularStatistics(_headings);
        if (_headings.Count < 10 || spread > MaxHeadingSpreadDegrees)
            return Restart("Telefon se moc kýval, drž ho klidně kamerou dopředu.");

        var course = LineOfSight.Between(start with { AltitudeMeters = 0 }, position with { AltitudeMeters = 0 }).AzimuthDegrees;
        var correction = ((course - mean) % 360 + 540) % 360 - 180;
        Clear();
        return new WalkResult(WalkStatus.Done, distance, correction);
    }

    public void Clear()
    {
        _fixes.Clear();
        _headings.Clear();
    }

    private WalkResult Restart(string reason)
    {
        Clear();
        return new WalkResult(WalkStatus.Restarted, 0, Reason: reason);
    }

    /// <summary>Největší vzdálenost bodu chůze od přímky mezi prvním a posledním bodem (v metrech).</summary>
    private double MaxSideDeviation(GeoPoint start, GeoPoint end)
    {
        var metersPerDegreeLon = 111_320 * Math.Cos(start.LatitudeDegrees * Math.PI / 180);
        (double X, double Y) Local(GeoPoint p) => ((p.LongitudeDegrees - start.LongitudeDegrees) * metersPerDegreeLon, (p.LatitudeDegrees - start.LatitudeDegrees) * 111_320);

        var (ex, ey) = Local(end);
        var length = Math.Sqrt(ex * ex + ey * ey);
        return _fixes.Select(Local).Max(p => Math.Abs(p.X * ey - p.Y * ex) / length);
    }

    /// <summary>Průměrný směr a jeho rozptyl přes sever správně (359° a 1° dají 0°, ne 180°).</summary>
    private static (double Mean, double Spread) CircularStatistics(IReadOnlyList<double> degrees)
    {
        if (degrees.Count == 0)
            return (0, double.MaxValue);

        var sin = degrees.Average(d => Math.Sin(d * Math.PI / 180));
        var cos = degrees.Average(d => Math.Cos(d * Math.PI / 180));
        var mean = (Math.Atan2(sin, cos) * 180 / Math.PI + 360) % 360;
        var resultant = Math.Min(1, Math.Sqrt(sin * sin + cos * cos));
        var spread = Math.Sqrt(-2 * Math.Log(Math.Max(resultant, 1e-9))) * 180 / Math.PI;
        return (mean, spread);
    }
}
