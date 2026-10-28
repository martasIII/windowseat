using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

public enum PlaceKind
{
    City,
    Peak,
    Landmark,
    Bridge,
}

/// <summary>Místo, které umíme popsat: název, poloha s výškou bodu, na který se míří, a druh.</summary>
/// <param name="Importance">Jak známé místo je; bereme počet článků na Wikipediích. Při nedostatku místa vyhrává známější.</param>
public sealed record Place(string Name, GeoPoint Location, PlaceKind Kind, int Importance = 0);
