using WindowSeat.Core.Geo;

namespace WindowSeat.Core.Places;

/// <summary>
/// Ruční sada pro ladění ze země, než přijdou skutečná data. Souřadnice a důležitost (počet článků
/// na Wikipediích) jsou z Wikidat. Výška je bod, na který popisek míří: u staveb horní část věže
/// nebo střechy, u mostů mostovka, u vrcholů vrchol.
/// </summary>
public static class TestPlaces
{
    public static IReadOnlyList<Place> All { get; } =
    [
        // Praha 6, blízké cíle na kontrolu přesnosti.
        new("Letohrádek Hvězda", new GeoPoint(50.0834, 14.3263, 405), PlaceKind.Landmark, 20),
        new("Bílá hora", new GeoPoint(50.07861, 14.31944, 381), PlaceKind.Peak, 10),

        // Pražské památky.
        new("Katedrála sv. Víta", new GeoPoint(50.090833, 14.400556, 320), PlaceKind.Landmark, 60),
        new("Karlův most", new GeoPoint(50.086389, 14.411944, 195), PlaceKind.Bridge, 62),
        new("Kostel sv. Mikuláše", new GeoPoint(50.087778, 14.403611, 255), PlaceKind.Landmark, 26),
        new("Týnský chrám", new GeoPoint(50.08771, 14.4227, 265), PlaceKind.Landmark, 38),
        new("Národní divadlo", new GeoPoint(50.081111, 14.413611, 220), PlaceKind.Landmark, 38),
        new("Národní muzeum", new GeoPoint(50.079843, 14.430417, 290), PlaceKind.Landmark, 41),
        new("Tančící dům", new GeoPoint(50.075556, 14.414167, 215), PlaceKind.Landmark, 50),
        new("Rudolfinum", new GeoPoint(50.0900, 14.4156, 210), PlaceKind.Landmark, 26),
        new("Staroměstská radnice", new GeoPoint(50.087108, 14.420607, 245), PlaceKind.Landmark, 23),
        new("Prašná brána", new GeoPoint(50.087222, 14.427778, 245), PlaceKind.Landmark, 28),
        new("Staroměstská mostecká věž", new GeoPoint(50.086293, 14.413655, 230), PlaceKind.Landmark, 11),
        new("Malostranská mostecká věž", new GeoPoint(50.087314, 14.40686, 230), PlaceKind.Landmark, 11),
        new("Strahovský klášter", new GeoPoint(50.085963, 14.389282, 315), PlaceKind.Landmark, 33),
        new("Emauzy", new GeoPoint(50.072222, 14.4175, 225), PlaceKind.Landmark, 19),
        new("Vyšehrad", new GeoPoint(50.064427, 14.417644, 270), PlaceKind.Landmark, 40),
        new("Petřínská rozhledna", new GeoPoint(50.083544, 14.39505, 370), PlaceKind.Landmark, 22),
        new("Žižkovský vysílač", new GeoPoint(50.080833, 14.451389, 400), PlaceKind.Landmark, 29),

        // Mosty přes Vltavu, po proudu.
        new("Palackého most", new GeoPoint(50.072778, 14.412222, 195), PlaceKind.Bridge, 12),
        new("Jiráskův most", new GeoPoint(50.075558, 14.411633, 195), PlaceKind.Bridge, 5),
        new("Most Legií", new GeoPoint(50.081278, 14.410111, 195), PlaceKind.Bridge, 12),
        new("Mánesův most", new GeoPoint(50.089522, 14.412728, 195), PlaceKind.Bridge, 16),
        new("Čechův most", new GeoPoint(50.093021, 14.417058, 196), PlaceKind.Bridge, 12),
        new("Štefánikův most", new GeoPoint(50.0944, 14.427, 196), PlaceKind.Bridge, 7),
        new("Hlávkův most", new GeoPoint(50.095556, 14.436944, 196), PlaceKind.Bridge, 5),

        // Vzdálené vrcholy a města na kontrolu směru.
        new("Říp", new GeoPoint(50.3867, 14.2883, 459), PlaceKind.Peak, 30),
        new("Milešovka", new GeoPoint(50.5553, 13.9317, 837), PlaceKind.Peak, 25),
        new("Ještěd", new GeoPoint(50.7326, 14.9848, 1012), PlaceKind.Peak, 35),
        new("Sněžka", new GeoPoint(50.7360, 15.7398, 1603), PlaceKind.Peak, 60),
        new("Klínovec", new GeoPoint(50.3964, 12.9677, 1244), PlaceKind.Peak, 25),
        new("Kladno", new GeoPoint(50.1473, 14.1029, 300), PlaceKind.City, 60),
        new("Plzeň", new GeoPoint(49.7384, 13.3736, 310), PlaceKind.City, 100),
        new("Brno", new GeoPoint(49.1951, 16.6068, 237), PlaceKind.City, 150),
    ];
}
