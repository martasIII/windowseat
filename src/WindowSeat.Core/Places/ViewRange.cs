namespace WindowSeat.Core.Places;

/// <summary>
/// Jak daleko ukazovat místa. Dokud nepočítáme zakrytí terénem, je to hrubá náhrada: ze země je za pár
/// kilometry většinou kopec nebo dům, z rozhledny se dohlédne mnohem dál. Vrcholy jsou vysoké, a tak vidět
/// i z dvojnásobku; Slunce a Měsíc dosah nemají.
/// </summary>
public static class ViewRange
{
    /// <summary>Nabízené dosahy v kilometrech; výchozí je 10 km.</summary>
    public static IReadOnlyList<double> PresetsKm { get; } = [5, 10, 20, 50, 150];

    public const double DefaultKm = 10;

    private const double PeakFactor = 2;

    public static bool IsWithin(Place place, double distanceMeters, double rangeKm) => place.Kind switch
    {
        PlaceKind.CelestialBody => true,
        PlaceKind.Peak => distanceMeters <= rangeKm * PeakFactor * 1000,
        _ => distanceMeters <= rangeKm * 1000,
    };
}
