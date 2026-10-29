using System.Globalization;
using System.Text;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

/// <summary>Jeden okamžik relace: kde uživatel stál, kam mířil a co appka ukazovala.</summary>
public sealed record SessionSample(
    DateTimeOffset Time,
    GeoPoint? Observer,
    double? GpsAccuracyMeters,
    bool ManualPosition,
    CameraPose Pose,
    double CompassDeviationDegrees,
    bool HeadingLocked,
    PoseCorrection Correction,
    double Zoom,
    double HorizontalFieldOfViewDegrees,
    string? Focused);

/// <summary>
/// Záznam relace do souboru v telefonu (JSON po řádcích, pět vzorků za sekundu). Z vyhlídky se tak dá
/// doma přehrát přesně to, co telefon „viděl“, a ladit vzhled nad fotkami bez dalšího chození ven.
/// Soubory leží v datech aplikace ve složce <c>recordings</c>.
/// </summary>
public sealed class SessionRecorder
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);

    private StreamWriter? _writer;
    private DateTimeOffset _lastSample;

    public bool IsRecording => _writer is not null;

    public string? CurrentFile { get; private set; }

    public void Start()
    {
        if (IsRecording)
            return;

        var folder = Path.Combine(FileSystem.AppDataDirectory, "recordings");
        Directory.CreateDirectory(folder);
        CurrentFile = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        _writer = new StreamWriter(CurrentFile, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
    }

    public void Stop()
    {
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>Zapíše vzorek, nejvýš pětkrát za sekundu; častější volání se zahodí.</summary>
    public void Record(SessionSample sample)
    {
        if (_writer is null || sample.Time - _lastSample < Interval)
            return;

        _lastSample = sample.Time;
        _writer.WriteLine(ToJson(sample));
    }

    // Ruční JSON: pár čísel, ať záznam nezávisí na reflexi a ořezávání při sestavení.
    private static string ToJson(SessionSample s)
    {
        var i = CultureInfo.InvariantCulture;
        var observer = s.Observer is { } o
            ? string.Format(i, "\"lat\":{0:0.000000},\"lon\":{1:0.000000},\"alt\":{2:0.0}", o.LatitudeDegrees, o.LongitudeDegrees, o.AltitudeMeters)
            : "\"lat\":null,\"lon\":null,\"alt\":null";
        var focused = s.Focused is null ? "null" : "\"" + s.Focused.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        return string.Format(i,
            "{{\"t\":\"{0:O}\",{1},\"gpsAcc\":{2},\"manual\":{3},\"az\":{4:0.00},\"el\":{5:0.00},\"roll\":{6:0.00}," +
            "\"compassDev\":{7:0.0},\"locked\":{8},\"corrAz\":{9:0.00},\"corrEl\":{10:0.00},\"zoom\":{11:0.00},\"hfov\":{12:0.00},\"focused\":{13}}}",
            s.Time, observer, s.GpsAccuracyMeters is { } acc ? acc.ToString("0", i) : "null",
            s.ManualPosition ? "true" : "false",
            s.Pose.AzimuthDegrees, s.Pose.ElevationDegrees, s.Pose.RollDegrees,
            s.CompassDeviationDegrees, s.HeadingLocked ? "true" : "false",
            s.Correction.AzimuthDegrees, s.Correction.ElevationDegrees,
            s.Zoom, s.HorizontalFieldOfViewDegrees, focused);
    }
}
