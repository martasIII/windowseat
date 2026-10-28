namespace WindowSeat.Core.Projection;

/// <summary>
/// Rozmístí popisky tak, aby se nepřekrývaly. Známější místa dostanou místo první; když je nad cílem obsazeno,
/// popisek se posune o patro výš (čárka k cíli se prodlouží). Co se nevejde ani tak, nezobrazí se.
/// </summary>
public static class LabelLayout
{
    /// <param name="Width">Šířka popisku; 0 = výchozí šířka z <see cref="Arrange{T}"/>.</param>
    public sealed record Request<T>(T Item, ScreenPoint Anchor, double Importance, double Width = 0);

    /// <param name="Level">O kolik pater je popisek nad základní polohou; 0 = hned nad cílem.</param>
    public sealed record Placed<T>(T Item, ScreenPoint Anchor, int Level, double Width);

    /// <param name="obstacles">Plochy, kam popisky nesmí (třeba svislé popisky vrcholů v pruhu nahoře).</param>
    public static IReadOnlyList<Placed<T>> Arrange<T>(
        IEnumerable<Request<T>> requests,
        double labelWidth,
        double labelHeight,
        int maxLevels = 4,
        IEnumerable<(double Left, double Top, double Right, double Bottom)>? obstacles = null)
    {
        var occupied = new List<(double Left, double Top, double Right, double Bottom)>(obstacles ?? []);
        var placed = new List<Placed<T>>();

        foreach (var request in requests.OrderByDescending(r => r.Importance))
        {
            var width = request.Width > 0 ? request.Width : labelWidth;
            for (var level = 0; level < maxLevels; level++)
            {
                var box = Box(request.Anchor, level, width, labelHeight);
                if (occupied.Any(o => Overlaps(o, box)))
                    continue;

                occupied.Add(box);
                placed.Add(new Placed<T>(request.Item, request.Anchor, level, width));
                break;
            }
        }

        return placed;
    }

    /// <summary>Obdélník popisku: vodorovně na střed cíle, svisle nad ním podle patra.</summary>
    public static (double Left, double Top, double Right, double Bottom) Box(ScreenPoint anchor, int level, double width, double height)
    {
        var bottom = anchor.Y - 32 - level * (height + 4);
        return (anchor.X - width / 2, bottom - height, anchor.X + width / 2, bottom);
    }

    private static bool Overlaps(
        (double Left, double Top, double Right, double Bottom) a,
        (double Left, double Top, double Right, double Bottom) b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
}
