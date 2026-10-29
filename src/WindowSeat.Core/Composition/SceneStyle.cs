using WindowSeat.Core.Places;

namespace WindowSeat.Core.Composition;

/// <summary>Rozměry a barvy scény, společné pro všechna kreslení (appka, prohlížeč).</summary>
public static class SceneStyle
{
    public const double FontSize = 15;
    public const double FocusFontSize = 20;
    public const double MinFocusFontSize = 13;
    public const double LabelHeight = 20;
    public const double RingRadius = 40;

    /// <summary>Kolik nejvýznamnějších míst má název trvale.</summary>
    public const int PermanentLabels = 4;

    /// <summary>Nejmenší vzdálenost dvou ikonek; bližší se schovají pod důležitější.</summary>
    public const double MinIconDistance = 26;

    /// <summary>Strop ikonek v záběru; v centru Prahy jsou jich jinak stovky a kreslení se zpomalí.</summary>
    public const int MaxIcons = 60;

    /// <summary>Poloměr vějíře: větší než okruh, kde zaměřovač pustí hlavní ikonku, aby šlo přejet na sousedy.</summary>
    public const double FanRadius = 85;

    public const double FanSpacing = 44;
    public const int MaxFanSize = 12;

    /// <summary>Zvýraznění zaměřeného místa (ikonka, kroužek, čára).</summary>
    public const string FocusColor = "#D85A30";

    /// <summary>Text názvu zaměřeného místa.</summary>
    public const string FocusTextColor = "#712B13";

    /// <summary>Barva ikonky podle vrstvy.</summary>
    public static string ColorOf(PlaceKind kind) => kind switch
    {
        PlaceKind.Peak => "#BA7517",
        PlaceKind.Bridge => "#185FA5",
        PlaceKind.CelestialBody => "#EF9F27",
        PlaceKind.City => "#444441",
        PlaceKind.Building => "#5F5E5A",
        PlaceKind.Castle => "#993556",
        PlaceKind.Tower => "#534AB7",
        _ => "#0F6E56",
    };
}
