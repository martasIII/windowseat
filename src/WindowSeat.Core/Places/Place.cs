using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

public enum PlaceKind
{
    City,
    Peak,
    Landmark,
}

/// <summary>Místo, které umíme popsat: název, poloha s výškou bodu, na který se míří, a druh.</summary>
public sealed record Place(string Name, GeoPoint Location, PlaceKind Kind);
