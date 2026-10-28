namespace WindowSeat.Core.Projection;

/// <summary>
/// Proředí ikonky, aby se nepřekrývaly: v hustém místě zůstane ta s nejvyšším skóre a ostatní pod ní
/// se jen započítají (odznak „+N“). Z nízkého místa leží celé město v úzkém proužku nad obzorem;
/// bez proředění by z něj byla zeď ikonek.
/// </summary>
public static class IconDeclutter
{
    public sealed record Item<T>(T Value, ScreenPoint Point, double Score);

    /// <param name="Members">Méně důležitá místa schovaná pod touto ikonkou, od nejdůležitějšího.</param>
    public sealed record Shown<T>(T Value, ScreenPoint Point, double Score, IReadOnlyList<Item<T>> Members)
    {
        public int Hidden => Members.Count;
    }

    /// <param name="minDistance">Nejmenší vzdálenost středů dvou ikonek na obrazovce.</param>
    public static IReadOnlyList<Shown<T>> Arrange<T>(IEnumerable<Item<T>> items, double minDistance)
    {
        var shown = new List<Shown<T>>();
        foreach (var item in items.OrderByDescending(i => i.Score))
        {
            var nearest = shown
                .Select((s, index) => (index, distance: Distance(s.Point, item.Point)))
                .Where(x => x.distance < minDistance)
                .OrderBy(x => x.distance)
                .Select(x => (int?)x.index)
                .FirstOrDefault();

            if (nearest is { } index)
                shown[index] = shown[index] with { Members = [.. shown[index].Members, item] };
            else
                shown.Add(new Shown<T>(item.Value, item.Point, item.Score, []));
        }

        return shown;
    }

    private static double Distance(ScreenPoint a, ScreenPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
