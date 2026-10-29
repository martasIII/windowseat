using WindowSeat.Core.Geo;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Composition;

/// <summary>Obdélník na obrazovce, počátek vlevo nahoře.</summary>
public readonly record struct ScreenRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;

    public double Bottom => Top + Height;

    /// <summary>Vzdálenost bodu od obdélníku; uvnitř nula.</summary>
    public double DistanceTo(ScreenPoint point)
    {
        var dx = Math.Max(0, Math.Max(Left - point.X, point.X - Right));
        var dy = Math.Max(0, Math.Max(Top - point.Y, point.Y - Bottom));
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>Ikonka místa: kde, jak velká a průhledná, a jestli je zaměřená nebo rozestoupená ve vějíři.</summary>
/// <param name="Hidden">Kolik míst se pod ikonkou schovalo (odznak „+N“).</param>
/// <param name="TruePoint">U ikonky ve vějíři její skutečná poloha (tenká čára k ní); jinak <c>null</c>.</param>
public sealed record SceneMarker(
    Place Place,
    LineOfSight Los,
    ScreenPoint Point,
    double Size,
    double Opacity,
    bool Focused,
    int Hidden,
    ScreenPoint? TruePoint)
{
    public ScreenRect Bounds
    {
        get
        {
            var half = Focused ? 18 : 14;
            return new ScreenRect(Point.X - half, Point.Y - half, half * 2, half * 2);
        }
    }
}

/// <summary>Trvalý název nad ikonkou: text na střed <see cref="CentreX"/>, účaří <see cref="Baseline"/>, čára od ikonky nahoru.</summary>
public sealed record SceneLabel(Place Place, LineOfSight Los, ScreenPoint Anchor, string Text, double CentreX, double Baseline, ScreenRect Bounds);

/// <summary>Zaměřené místo: velký název a podrobnost pod kroužkem.</summary>
/// <param name="NameFontSize">Velikost písma názvu; dlouhé názvy se zmenší, aby se vešly na šířku.</param>
public sealed record SceneFocus(Place Place, LineOfSight Los, ScreenPoint Point, string Name, string Detail, double NameFontSize);

/// <summary>Promítnutý mnohoúhelník (řeka, mostovka): kruhy se kreslí pravidlem sudé–liché.</summary>
public sealed record ScenePolygon(string? Name, IReadOnlyList<IReadOnlyList<ScreenPoint>> Rings, bool Highlighted);

/// <summary>Co klepnutí zasáhlo.</summary>
public sealed record SceneHit(Place Place, LineOfSight Los, ScreenPoint Point);

/// <summary>
/// Jeden snímek popisků, nezávislý na tom, čím se kreslí (MAUI, prohlížeč). Skládá ho <see cref="SceneComposer"/>.
/// </summary>
/// <param name="FocusAcquired">Cíl se v tomto snímku chytil do kroužku (vibrace).</param>
/// <param name="FocusDwelled">Cíl je v kroužku dost dlouho na kartičku s detailem.</param>
public sealed record Scene(
    ScreenProjector Projector,
    double Width,
    double Height,
    IReadOnlyList<ScenePolygon> Water,
    IReadOnlyList<ScenePolygon> Bridges,
    IReadOnlyList<SceneMarker> Markers,
    IReadOnlyList<SceneLabel> Labels,
    SceneFocus? Focus,
    bool FocusAcquired,
    bool FocusDwelled)
{
    public ScreenPoint Centre => new(Width / 2, Height / 2);

    /// <summary>Nejbližší ikonka nebo název k bodu klepnutí, nejdál <paramref name="radius"/>.</summary>
    public SceneHit? HitTest(ScreenPoint tapped, double radius)
    {
        var candidates = Markers
            .Select(m => (Hit: new SceneHit(m.Place, m.Los, m.Point), Distance: Math.Min(Distance(m.Point, tapped), m.Bounds.DistanceTo(tapped))))
            .Concat(Labels.Select(l => (Hit: new SceneHit(l.Place, l.Los, l.Anchor), Distance: l.Bounds.DistanceTo(tapped))));

        return candidates
            .Where(c => c.Distance < radius)
            .OrderBy(c => c.Distance)
            .Select(c => c.Hit)
            .FirstOrDefault();
    }

    /// <summary>Kde ve scéně je dané místo (ikonka, název nebo zaměření); kvůli směru k němu při kalibraci.</summary>
    public SceneHit? Find(Place place) =>
        Markers.Where(m => m.Place == place).Select(m => new SceneHit(m.Place, m.Los, m.Point)).FirstOrDefault()
        ?? Labels.Where(l => l.Place == place).Select(l => new SceneHit(l.Place, l.Los, l.Anchor)).FirstOrDefault()
        ?? (Focus is { } focus && focus.Place == place ? new SceneHit(focus.Place, focus.Los, focus.Point) : null);

    private static double Distance(ScreenPoint a, ScreenPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
