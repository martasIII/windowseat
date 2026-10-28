namespace WindowSeat.Core.Places;

/// <summary>
/// Priorita vrstev: 0 = vypnuto, 1 = běžná, víc = přednost, když není místo pro všechny popisky.
/// Výchozí hodnoty jsou kompromis pro město i přírodu, takže uživatel nic přepínat nemusí.
/// </summary>
public sealed record LayerWeights(IReadOnlyDictionary<PlaceKind, double> Weights)
{
    /// <summary>Úrovně priority, jak je nabízí nastavení: vypnuto, nízká, běžná, vysoká.</summary>
    public static IReadOnlyList<double> Levels { get; } = [0, 0.6, 1, 1.5];

    public static LayerWeights Default { get; } = new(new Dictionary<PlaceKind, double>
    {
        [PlaceKind.Peak] = 1.5,
        [PlaceKind.Tower] = 1.5,
        [PlaceKind.Castle] = 1,
        [PlaceKind.Church] = 1,
        [PlaceKind.Bridge] = 1,
        [PlaceKind.City] = 1,
        [PlaceKind.Building] = 0.6,
    });

    /// <summary>Druhy, které si uživatel může nastavit; Slunce a Měsíc jsou vždy zapnuté.</summary>
    public static IReadOnlyList<PlaceKind> Configurable { get; } =
        [PlaceKind.Peak, PlaceKind.Tower, PlaceKind.Castle, PlaceKind.Church, PlaceKind.Bridge, PlaceKind.City, PlaceKind.Building];

    public double this[PlaceKind kind] => kind == PlaceKind.CelestialBody ? 1 : Weights.GetValueOrDefault(kind, 1);

    public bool IsVisible(PlaceKind kind) => this[kind] > 0;

    public LayerWeights With(PlaceKind kind, double weight) =>
        new(new Dictionary<PlaceKind, double>(Weights) { [kind] = Math.Max(0, weight) });

    /// <summary>
    /// Skóre pro výběr popisků: známost × priorita vrstvy, blízká místa navrch. Slunce a Měsíc mají vždy přednost.
    /// </summary>
    public double Score(Place place, double distanceMeters)
    {
        if (place.Kind == PlaceKind.CelestialBody)
            return double.MaxValue;

        // Do 3 km skoro plné skóre, v 15 km zhruba šestina; známé daleké místo tak porazí neznámé blízké, ale ne naopak.
        var nearness = 1 / (1 + distanceMeters / 3_000);
        return (1 + place.Importance) * this[place.Kind] * nearness;
    }
}
