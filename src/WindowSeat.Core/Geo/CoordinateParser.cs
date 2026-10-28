using System.Globalization;
using System.Text.RegularExpressions;

namespace WindowSeat.Core.Geo;

/// <summary>
/// Čte souřadnice zkopírované z map: „50.0786189N, 14.3194294E“ (Mapy.cz)
/// nebo „50.0786, 14.3194“ (Google). Jen desetinné stupně, šířka první.
/// </summary>
public static partial class CoordinateParser
{
    public static bool TryParse(string? text, out double latitude, out double longitude)
    {
        latitude = longitude = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var matches = Coordinate().Matches(text);
        if (matches.Count != 2)
            return false;

        if (!TryRead(matches[0], 'S', out latitude) || !TryRead(matches[1], 'W', out longitude))
            return false;

        return Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180;
    }

    private static bool TryRead(Match match, char negativeHemisphere, out double value)
    {
        if (!double.TryParse(match.Groups["number"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;

        if (char.ToUpperInvariant(match.Groups["hemisphere"].Value.FirstOrDefault()) == negativeHemisphere)
            value = -value;
        return true;
    }

    [GeneratedRegex(@"(?<number>-?\d+(?:\.\d+)?)\s*°?\s*(?<hemisphere>[NSEWnsew])?")]
    private static partial Regex Coordinate();
}
