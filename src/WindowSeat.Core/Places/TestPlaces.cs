using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

/// <summary>
/// Ruční sada pro ladění ze země, než přijdou data z GeoNames.
/// Blízké cíle z Prahy 6 na kontrolu přesnosti, vzdálené na kontrolu směru.
/// U staveb je výška střed budovy nebo věže, ne terén pod ní.
/// </summary>
public static class TestPlaces
{
    public static IReadOnlyList<Place> All { get; } =
    [
        new("Letohrádek Hvězda", new GeoPoint(50.0834, 14.3263, 405), PlaceKind.Landmark),
        new("Bílá hora", new GeoPoint(50.07861, 14.31944, 381), PlaceKind.Peak),
        new("Petřínská rozhledna", new GeoPoint(50.0835, 14.3951, 350), PlaceKind.Landmark),
        new("Pražský hrad", new GeoPoint(50.0909, 14.4005, 300), PlaceKind.Landmark),
        new("Žižkovský vysílač", new GeoPoint(50.0810, 14.4511, 370), PlaceKind.Landmark),
        new("Říp", new GeoPoint(50.3867, 14.2883, 459), PlaceKind.Peak),
        new("Milešovka", new GeoPoint(50.5553, 13.9317, 837), PlaceKind.Peak),
        new("Ještěd", new GeoPoint(50.7326, 14.9848, 1012), PlaceKind.Peak),
        new("Sněžka", new GeoPoint(50.7360, 15.7398, 1603), PlaceKind.Peak),
        new("Klínovec", new GeoPoint(50.3964, 12.9677, 1244), PlaceKind.Peak),
        new("Kladno", new GeoPoint(50.1473, 14.1029, 300), PlaceKind.City),
        new("Plzeň", new GeoPoint(49.7384, 13.3736, 310), PlaceKind.City),
        new("Brno", new GeoPoint(49.1951, 16.6068, 237), PlaceKind.City),
    ];
}
