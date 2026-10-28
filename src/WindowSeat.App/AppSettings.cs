using WindowSeat.Core.Orientation;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

/// <summary>Nastavení pro ladění ze země, uložené v telefonu mezi spuštěními.</summary>
public static class AppSettings
{
    public static bool ManualPosition
    {
        get => Preferences.Default.Get(nameof(ManualPosition), false);
        set => Preferences.Default.Set(nameof(ManualPosition), value);
    }

    public static double ManualLatitude
    {
        get => Preferences.Default.Get(nameof(ManualLatitude), 50.0786);
        set => Preferences.Default.Set(nameof(ManualLatitude), value);
    }

    public static double ManualLongitude
    {
        get => Preferences.Default.Get(nameof(ManualLongitude), 14.3194);
        set => Preferences.Default.Set(nameof(ManualLongitude), value);
    }

    public static bool ManualAltitude
    {
        get => Preferences.Default.Get(nameof(ManualAltitude), false);
        set => Preferences.Default.Set(nameof(ManualAltitude), value);
    }

    public static double ManualAltitudeMeters
    {
        get => Preferences.Default.Get(nameof(ManualAltitudeMeters), 350.0);
        set => Preferences.Default.Set(nameof(ManualAltitudeMeters), value);
    }

    public static HeadingMode HeadingMode
    {
        get => (HeadingMode)Preferences.Default.Get(nameof(HeadingMode), (int)HeadingMode.Compass);
        set => Preferences.Default.Set(nameof(HeadingMode), (int)value);
    }

    /// <summary>Oprava pózy z poslední kalibrace na známý cíl.</summary>
    public static PoseCorrection Correction
    {
        get => new(
            Preferences.Default.Get("CorrectionAzimuth", 0.0),
            Preferences.Default.Get("CorrectionElevation", 0.0));
        set
        {
            Preferences.Default.Set("CorrectionAzimuth", value.AzimuthDegrees);
            Preferences.Default.Set("CorrectionElevation", value.ElevationDegrees);
        }
    }

    /// <summary>Zorný úhel mezi levým a pravým okrajem obrazovky; ladí se podle známého cíle.</summary>
    public static double HorizontalFieldOfViewDegrees
    {
        get => Preferences.Default.Get(nameof(HorizontalFieldOfViewDegrees), 50.0);
        set => Preferences.Default.Set(nameof(HorizontalFieldOfViewDegrees), value);
    }
}
