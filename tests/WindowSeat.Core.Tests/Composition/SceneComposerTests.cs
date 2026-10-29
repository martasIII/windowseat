using WindowSeat.Core.Composition;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Composition;

public class SceneComposerTests
{
    private const double Width = 400;
    private const double Height = 860;
    private const double FieldOfView = 34;

    private static readonly DateTimeOffset Start = new(2026, 10, 29, 21, 0, 0, TimeSpan.Zero);
    private static readonly GeoPoint Letna = new(50.0947, 14.4156, 245);

    // Hrubá šířka textu: 8 bodů na znak; skutečné měření dodá kreslení.
    private static double TextWidth(string text, double size) => text.Length * size * 0.55;

    private static SceneComposer Composer(params Place[] places) => new() { Places = places, Observer = Letna };

    private static Place PlaceAt(string name, double azimuthDegrees, double distanceMeters, int importance = 20, PlaceKind kind = PlaceKind.Church)
    {
        var north = Math.Cos(azimuthDegrees * Math.PI / 180) * distanceMeters;
        var east = Math.Sin(azimuthDegrees * Math.PI / 180) * distanceMeters;
        var location = new GeoPoint(
            Letna.LatitudeDegrees + north / 111_320,
            Letna.LongitudeDegrees + east / (111_320 * Math.Cos(Letna.LatitudeDegrees * Math.PI / 180)),
            Letna.AltitudeMeters);
        return new Place(name, location, kind, importance);
    }

    private static Scene Compose(SceneComposer composer, double azimuth, DateTimeOffset? at = null) =>
        composer.Compose(new CameraPose(azimuth, 0, 0), Width, Height, FieldOfView, at ?? Start, TextWidth);

    [Fact]
    public void Place_in_front_gets_an_icon_and_one_behind_does_not()
    {
        var composer = Composer(PlaceAt("Týn", 180, 1_500), PlaceAt("Stadion", 0, 1_000));

        var scene = Compose(composer, azimuth: 180);

        Assert.Equal(["Týn"], scene.Markers.Select(m => m.Place.Name));
    }

    [Fact]
    public void Target_in_the_ring_is_acquired_and_shows_the_card_after_dwelling()
    {
        var composer = Composer(PlaceAt("Týn", 180, 1_500));

        var first = Compose(composer, 180, Start);
        var early = Compose(composer, 180, Start.AddMilliseconds(300));
        var dwelled = Compose(composer, 180, Start.AddMilliseconds(700));

        Assert.True(first.FocusAcquired);
        Assert.Equal("Týn", first.Focus?.Name);
        Assert.False(early.FocusDwelled);
        Assert.True(dwelled.FocusDwelled);
    }

    [Fact]
    public void Tap_on_an_icon_hits_its_place()
    {
        var composer = Composer(PlaceAt("Týn", 175, 1_500));
        var scene = Compose(composer, 180);
        var icon = scene.Markers.Single();

        var hit = scene.HitTest(new ScreenPoint(icon.Point.X + 5, icon.Point.Y - 5), radius: 60);

        Assert.Equal("Týn", hit?.Place.Name);
    }

    [Fact]
    public void At_most_four_permanent_labels_and_never_for_the_focused_place()
    {
        var places = Enumerable.Range(0, 8).Select(i => PlaceAt($"Kostel {i}", 170 + i * 3, 2_000 + i * 300, importance: 30 - i)).ToArray();
        var composer = Composer(places);

        var scene = Compose(composer, 180);

        Assert.InRange(scene.Labels.Count, 1, SceneStyle.PermanentLabels);
        Assert.DoesNotContain(scene.Labels, l => l.Place.Name == scene.Focus?.Name);
    }

    [Fact]
    public void Long_focused_name_is_shrunk_to_fit_the_screen()
    {
        var composer = Composer(PlaceAt("Kostel Panny Marie Vítězné na Bílé Hoře a ještě něco navíc", 180, 1_500));

        var scene = Compose(composer, 180);

        Assert.True(scene.Focus!.NameFontSize < SceneStyle.FocusFontSize);
        Assert.True(scene.Focus.NameFontSize >= SceneStyle.MinFocusFontSize);
    }

    [Fact]
    public void Pinned_place_stays_focused_wherever_the_ring_points()
    {
        var church = PlaceAt("Týn", 185, 1_500);
        var composer = Composer(church, PlaceAt("Most", 180, 900, kind: PlaceKind.Bridge));
        composer.Highlighted = church;

        var scene = Compose(composer, 180);

        Assert.Equal("Týn", scene.Focus?.Name);
    }
}
