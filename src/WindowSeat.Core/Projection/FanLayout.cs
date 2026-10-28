namespace WindowSeat.Core.Projection;

/// <summary>
/// Rozbalení shluku do vějíře: schované ikonky se rozestoupí po kružnici kolem hlavní ikonky,
/// první nahoře a dál po směru hodin. Poloměr roste s počtem, aby mezi nimi zůstalo místo na zaměření.
/// </summary>
public static class FanLayout
{
    /// <param name="count">Kolik ikonek rozestoupit.</param>
    /// <param name="spacing">Nejmenší vzdálenost sousedních ikonek na kružnici.</param>
    /// <param name="minRadius">Nejmenší poloměr; musí být větší než okruh, kde zaměřovač pustí hlavní ikonku.</param>
    public static IReadOnlyList<(double Dx, double Dy)> Offsets(int count, double spacing, double minRadius)
    {
        if (count <= 0)
            return [];

        // Obvod musí pojmout všechny ikonky s danými rozestupy: 2πr ≥ n · rozestup.
        var radius = Math.Max(minRadius, count * spacing / (2 * Math.PI));

        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var angle = -Math.PI / 2 + i * 2 * Math.PI / count;
                return (radius * Math.Cos(angle), radius * Math.Sin(angle));
            })
            .ToList();
    }
}
