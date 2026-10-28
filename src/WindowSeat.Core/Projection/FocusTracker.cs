namespace WindowSeat.Core.Projection;

/// <summary>Co je právě zaměřené a jestli se to právě změnilo (kvůli vibraci a kartičce s detailem).</summary>
/// <param name="JustAcquired">Cíl se v tomto snímku chytil; appka cvakne vibrací.</param>
/// <param name="DwellReached">Cíl drží zaměřený dost dlouho na detail.</param>
/// <param name="JustDwelled">Doba podržení uplynula právě teď; appka ukáže kartičku.</param>
public readonly record struct FocusState<T>(T? Focused, bool JustAcquired, bool DwellReached, bool JustDwelled);

/// <summary>
/// Zaměřování kroužkem uprostřed obrazovky. Chytí cíl, který do kroužku vjede nejblíž středu, a pustí ho
/// až za větším okruhem (hystereze), aby popisek neblikal mezi dvěma blízkými věcmi při chvění ruky.
/// </summary>
public sealed class FocusTracker<T>(double acquireRadius, double releaseRadius, TimeSpan dwell)
    where T : class
{
    private T? _focused;
    private DateTimeOffset _since;
    private bool _dwelled;

    /// <param name="candidates">Cíle v záběru a jejich vzdálenost od středu kroužku (v bodech obrazovky).</param>
    public FocusState<T> Update(IEnumerable<(T Item, double Distance)> candidates, DateTimeOffset now)
    {
        var list = candidates.ToList();

        // Zaměřený cíl držíme, dokud nevyjede z většího okruhu, i když je jiný blíž.
        var current = _focused is null ? default : list.FirstOrDefault(c => ReferenceEquals(c.Item, _focused));
        if (current.Item is not null && current.Distance <= releaseRadius)
            return State(justAcquired: false, now);

        var best = list
            .Where(c => c.Distance <= acquireRadius)
            .OrderBy(c => c.Distance)
            .FirstOrDefault();

        if (best.Item is null)
        {
            _focused = null;
            return default;
        }

        _focused = best.Item;
        _since = now;
        _dwelled = false;
        return State(justAcquired: true, now);
    }

    /// <summary>Zapomene zaměření, třeba když uživatel cíl přišpendlí klepnutím.</summary>
    public void Reset() => _focused = null;

    private FocusState<T> State(bool justAcquired, DateTimeOffset now)
    {
        var dwellReached = now - _since >= dwell;
        var justDwelled = dwellReached && !_dwelled;
        _dwelled |= dwellReached;
        return new FocusState<T>(_focused, justAcquired, dwellReached, justDwelled);
    }
}
