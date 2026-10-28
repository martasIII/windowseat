namespace WindowSeat.Core.Projection;

/// <summary>
/// Popisky v pruhu u horního okraje, psané svisle, s čarou dolů k cíli (jako PeakFinder).
/// Každý popisek zabírá úzký sloupec; když je místo nad cílem obsazené, posune se vedle a čára se zešikmí.
/// Pořadí podle skóre: co se nevejde, nezobrazí se.
/// </summary>
public static class SkylineLayout
{
    public sealed record Candidate<T>(T Item, ScreenPoint Anchor, double Score);

    /// <param name="LabelX">Vodorovná poloha sloupce s popiskem; může se lišit od cíle.</param>
    public sealed record Slot<T>(T Item, ScreenPoint Anchor, double LabelX);

    /// <param name="columnWidth">Šířka sloupce jednoho popisku (výška písma plus mezera).</param>
    /// <param name="maxShiftColumns">O kolik sloupců smí popisek uhnout od cíle.</param>
    public static IReadOnlyList<Slot<T>> Arrange<T>(
        IEnumerable<Candidate<T>> candidates,
        double screenWidth,
        double columnWidth,
        int maxShiftColumns = 2)
    {
        var taken = new List<double>();
        var placed = new List<Slot<T>>();
        var minX = columnWidth / 2;
        var maxX = screenWidth - columnWidth / 2;

        foreach (var candidate in candidates.OrderByDescending(c => c.Score))
        {
            var preferred = Math.Clamp(candidate.Anchor.X, minX, maxX);

            // Zkoušíme přímo nad cílem, pak střídavě vlevo a vpravo po půl sloupci.
            var options = Enumerable.Range(0, maxShiftColumns * 4 + 1)
                .Select(i => preferred + (i % 2 == 1 ? -1 : 1) * ((i + 1) / 2) * columnWidth / 2)
                .Where(x => x >= minX && x <= maxX);

            foreach (var x in options)
            {
                if (taken.Any(t => Math.Abs(t - x) < columnWidth))
                    continue;

                taken.Add(x);
                placed.Add(new Slot<T>(candidate.Item, candidate.Anchor, x));
                break;
            }
        }

        // Sloupce rozdáme znovu v pořadí cílů zleva doprava, aby se čáry ke slotům nekřížily.
        var columns = placed.Select(s => s.LabelX).Order().ToList();
        return placed
            .OrderBy(s => s.Anchor.X)
            .Select((slot, i) => slot with { LabelX = columns[i] })
            .ToList();
    }
}
