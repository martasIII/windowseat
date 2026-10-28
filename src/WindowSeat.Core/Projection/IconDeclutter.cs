namespace WindowSeat.Core.Projection;

/// <summary>
/// Proředí ikonky, aby se nepřekrývaly: v hustém místě zůstane ta s nejvyšším skóre a ostatní pod ní
/// se jen započítají (odznak „+N“). Z nízkého místa leží celé město v úzkém proužku nad obzorem;
/// bez proředění by z něj byla zeď ikonek.
/// </summary>
/// <remarks>
/// Běží v každém snímku nad stovkami míst, proto sousedy hledá v mřížce (políčko = nejmenší vzdálenost)
/// místo porovnávání všeho se vším, a počet ikonek má strop.
/// </remarks>
public static class IconDeclutter
{
    public sealed record Item<T>(T Value, ScreenPoint Point, double Score);

    /// <param name="Members">Méně důležitá místa schovaná pod touto ikonkou, od nejdůležitějšího.</param>
    public sealed record Shown<T>(T Value, ScreenPoint Point, double Score, IReadOnlyList<Item<T>> Members)
    {
        public int Hidden => Members.Count;
    }

    /// <param name="minDistance">Nejmenší vzdálenost středů dvou ikonek na obrazovce.</param>
    /// <param name="maxShown">Nejvíc ikonek; další místa se jen přidají k blízké ikonce, nebo se nekreslí vůbec.</param>
    public static IReadOnlyList<Shown<T>> Arrange<T>(IEnumerable<Item<T>> items, double minDistance, int maxShown = int.MaxValue)
    {
        var shown = new List<(Item<T> Head, List<Item<T>> Members)>();
        var grid = new Dictionary<(int X, int Y), List<int>>();

        foreach (var item in items.OrderByDescending(i => i.Score))
        {
            var cell = Cell(item.Point, minDistance);
            var nearest = -1;
            var nearestDistance = minDistance;

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!grid.TryGetValue((cell.X + dx, cell.Y + dy), out var indices))
                        continue;

                    foreach (var index in indices)
                    {
                        var distance = Distance(shown[index].Head.Point, item.Point);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearest = index;
                        }
                    }
                }
            }

            if (nearest >= 0)
            {
                shown[nearest].Members.Add(item);
            }
            else if (shown.Count < maxShown)
            {
                if (!grid.TryGetValue(cell, out var indices))
                    grid[cell] = indices = [];
                indices.Add(shown.Count);
                shown.Add((item, []));
            }
        }

        return shown.Select(s => new Shown<T>(s.Head.Value, s.Head.Point, s.Head.Score, s.Members)).ToList();
    }

    private static (int X, int Y) Cell(ScreenPoint point, double size) =>
        ((int)Math.Floor(point.X / size), (int)Math.Floor(point.Y / size));

    private static double Distance(ScreenPoint a, ScreenPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
