using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

/// <summary>Druh místa; zároveň vrstva, kterou si uživatel může vypnout nebo upřednostnit.</summary>
public enum PlaceKind
{
    Peak,
    Tower,
    Church,
    Castle,
    Bridge,
    Building,
    City,

    /// <summary>Slunce nebo Měsíc; polohu nemá na Zemi, ale na obloze podle času.</summary>
    CelestialBody,
}

/// <summary>Místo, které umíme popsat: název, poloha s výškou bodu, na který se míří, a druh.</summary>
/// <param name="Importance">Jak známé místo je; bereme počet článků na Wikipediích.</param>
/// <param name="Description">Krátký popis pro kartičku s detailem (z Wikidat), nebo <c>null</c>.</param>
/// <param name="WikipediaUrl">Článek na Wikipedii (česky, jinak anglicky), nebo <c>null</c>.</param>
public sealed record Place(
    string Name,
    GeoPoint Location,
    PlaceKind Kind,
    int Importance = 0,
    string? Description = null,
    string? WikipediaUrl = null);
