using WindowSeat.Core.Composition;

namespace WindowSeat.Web;

/// <summary>
/// Scéna pro kreslení v JavaScriptu: jen čísla a texty, co nejméně dat přes hranici C# → JS (30× za sekundu).
/// Obrysy ploch jako ploché pole [x0, y0, x1, y1, …].
/// </summary>
public sealed record SceneDto(
    double Cx,
    double Cy,
    double RingRadius,
    string FocusColor,
    string FocusTextColor,
    IReadOnlyList<PolygonDto> Water,
    IReadOnlyList<PolygonDto> Bridges,
    IReadOnlyList<MarkerDto> Markers,
    IReadOnlyList<LabelDto> Labels,
    FocusDto? Focus,
    bool Vibrate,
    string Status)
{
    public static SceneDto From(Scene scene, string status) => new(
        scene.Centre.X,
        scene.Centre.Y,
        SceneStyle.RingRadius,
        SceneStyle.FocusColor,
        SceneStyle.FocusTextColor,
        scene.Water.Select(PolygonDto.From).ToList(),
        scene.Bridges.Select(PolygonDto.From).ToList(),
        scene.Markers.Select(m => new MarkerDto(
            m.Point.X,
            m.Point.Y,
            m.Size,
            m.Opacity,
            m.Focused ? SceneStyle.FocusColor : SceneStyle.ColorOf(m.Place.Kind),
            m.Place.Kind.ToString(),
            m.Hidden,
            m.TruePoint?.X,
            m.TruePoint?.Y)).ToList(),
        scene.Labels.Select(l => new LabelDto(l.Text, l.CentreX, l.Baseline, l.Anchor.X, l.Anchor.Y, l.Bounds.Bottom)).ToList(),
        scene.Focus is { } f ? new FocusDto(f.Name, f.Detail, f.NameFontSize, f.Point.X, f.Point.Y) : null,
        scene.FocusAcquired,
        status);
}

public sealed record PolygonDto(IReadOnlyList<double[]> Rings, bool Highlighted)
{
    public static PolygonDto From(ScenePolygon polygon) => new(
        polygon.Rings.Select(r => r.SelectMany(p => new[] { Math.Round(p.X, 1), Math.Round(p.Y, 1) }).ToArray()).ToList(),
        polygon.Highlighted);
}

/// <param name="Tx">Skutečná poloha ikonky rozestoupené ve vějíři; jinak <c>null</c>.</param>
public sealed record MarkerDto(double X, double Y, double Size, double Opacity, string Color, string Kind, int Hidden, double? Tx, double? Ty);

public sealed record LabelDto(string Text, double Cx, double Baseline, double Ax, double Ay, double Bottom);

public sealed record FocusDto(string Name, string Detail, double NameSize, double X, double Y);
