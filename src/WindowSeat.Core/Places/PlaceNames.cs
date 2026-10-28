using System.Text.RegularExpressions;

namespace WindowSeat.Core.Places;

/// <summary>
/// Zkracuje názvy z Wikidat na popisek, který se vejde: „katedrála svatého Víta v Praze“ → „Katedrála sv. Víta“.
/// Kontext města (v Praze, Praha 6…) je na místě zbytečný, uživatel ví, kde stojí.
/// </summary>
public static partial class PlaceNames
{
    public static string Shorten(string name)
    {
        var shortened = Saint().Replace(name, "sv. ");
        shortened = CitySuffix().Replace(shortened, "");
        shortened = shortened.Trim().TrimEnd(',');

        return shortened.Length == 0 ? name : char.ToUpperInvariant(shortened[0]) + shortened[1..];
    }

    // Jen uvnitř názvu: „Svatá Hora“ na začátku je jméno místa, ne světec.
    [GeneratedRegex(@"(?<=\S\s)[Ss]vat(ého|é|ých|ý|á|ou)\s+")]
    private static partial Regex Saint();

    // „ v Praze“, „ v Praze-Starém Městě“, „ (Praha)“, „, Praha 6“ na konci názvu.
    [GeneratedRegex(@"(\s+v\s+Praze(-[^,()]+)?|\s*\(Praha[^)]*\)|,\s*Praha.*)$")]
    private static partial Regex CitySuffix();
}
