namespace WindowSeat.Core.Projection;

/// <summary>
/// Šipky na okraji k důležitým místům mimo záběr: „← Hrad 6 km“. Bere jen to, co je do čtvrt otáčky
/// od záběru (za zády nikoho nezajímá), a na každou stranu jen pár nejdůležitějších.
/// </summary>
public static class EdgeHints
{
    public sealed record Candidate<T>(T Item, double AzimuthDegrees, double Score);

    /// <param name="TurnDegrees">O kolik stupňů se otočit, záporné doleva.</param>
    public sealed record Hint<T>(T Item, double TurnDegrees);

    public static (IReadOnlyList<Hint<T>> Left, IReadOnlyList<Hint<T>> Right) Select<T>(
        IEnumerable<Candidate<T>> candidates,
        double cameraAzimuthDegrees,
        double horizontalFieldOfViewDegrees,
        int perSide = 2,
        double maxTurnDegrees = 90)
    {
        var half = horizontalFieldOfViewDegrees / 2;
        var offScreen = candidates
            .Select(c => (c, turn: ((c.AzimuthDegrees - cameraAzimuthDegrees) % 360 + 540) % 360 - 180))
            .Where(x => Math.Abs(x.turn) > half && Math.Abs(x.turn) <= maxTurnDegrees)
            .ToList();

        IReadOnlyList<Hint<T>> Side(Func<double, bool> isSide) => offScreen
            .Where(x => isSide(x.turn))
            .OrderByDescending(x => x.c.Score)
            .Take(perSide)
            .OrderBy(x => Math.Abs(x.turn))
            .Select(x => new Hint<T>(x.c.Item, x.turn))
            .ToList();

        return (Side(t => t < 0), Side(t => t > 0));
    }
}
