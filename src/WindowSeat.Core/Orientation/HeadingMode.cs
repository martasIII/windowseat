namespace WindowSeat.Core.Orientation;

/// <summary>Odkud bereme směr, kam míří kamera.</summary>
public enum HeadingMode
{
    /// <summary>Z kompasu telefonu; spolehlivé jen venku, ne v autě ani v letadle.</summary>
    Compass,

    /// <summary>Z kurzu GPS; telefon míří kolmo z levého okna, tedy 90° doleva od směru jízdy.</summary>
    CourseLeftWindow,

    /// <summary>Z kurzu GPS; telefon míří kolmo z pravého okna, tedy 90° doprava od směru jízdy.</summary>
    CourseRightWindow,
}

public static class Heading
{
    /// <summary>
    /// Směr kamery u bočního okna: kurz vozidla otočený o 90° na stranu okna.
    /// V kovovém trupu kompas nefunguje, kurz z GPS ano; náklon a výška pohledu zůstávají ze senzoru.
    /// </summary>
    public static double CameraAzimuthFromCourse(double courseDegrees, HeadingMode mode) => mode switch
    {
        HeadingMode.CourseLeftWindow => Normalize(courseDegrees - 90),
        HeadingMode.CourseRightWindow => Normalize(courseDegrees + 90),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Kurz z GPS se používá jen u bočního okna."),
    };

    private static double Normalize(double degrees) => (degrees % 360 + 360) % 360;
}
