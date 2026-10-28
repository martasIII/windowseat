using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class IconDeclutterTests
{
    [Fact]
    public void Icons_far_apart_are_all_shown()
    {
        var shown = IconDeclutter.Arrange([Item("Vít", 100, 50), Item("Petřín", 300, 20)], minDistance: 24);

        Assert.Equal(2, shown.Count);
        Assert.All(shown, s => Assert.Equal(0, s.Hidden));
    }

    [Fact]
    public void Crowded_spot_keeps_the_best_icon_and_counts_the_rest()
    {
        var shown = IconDeclutter.Arrange(
            [Item("kaple", 205, 2), Item("katedrála", 200, 60), Item("palác", 210, 10), Item("Petřín", 400, 20)],
            minDistance: 24);

        var cathedral = shown.Single(s => s.Value == "katedrála");
        Assert.Equal(2, cathedral.Hidden);
        Assert.Equal(["katedrála", "Petřín"], shown.Select(s => s.Value));
    }

    [Fact]
    public void Hidden_icon_counts_towards_the_nearest_shown_one()
    {
        var shown = IconDeclutter.Arrange(
            [Item("A", 100, 10), Item("B", 140, 9), Item("mezi", 135, 1)],
            minDistance: 24);

        Assert.Equal(1, shown.Single(s => s.Value == "B").Hidden);
        Assert.Equal(0, shown.Single(s => s.Value == "A").Hidden);
    }

    [Fact]
    public void Cap_keeps_the_best_icons_and_drops_the_rest_unless_they_fit_under_one()
    {
        var items = Enumerable.Range(0, 10).Select(i => Item($"m{i}", i * 100, score: i)).Append(Item("pod m9", 905, 0.5));

        var shown = IconDeclutter.Arrange(items, minDistance: 24, maxShown: 3);

        Assert.Equal(["m9", "m8", "m7"], shown.Select(s => s.Value));
        Assert.Equal("pod m9", shown[0].Members.Single().Value);
    }

    [Fact]
    public void Hundreds_of_icons_are_arranged_quickly()
    {
        var random = new Random(1);
        var items = Enumerable.Range(0, 2000)
            .Select(i => Item($"m{i}", random.NextDouble() * 1000, random.NextDouble()))
            .ToList();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var frame = 0; frame < 50; frame++)
            IconDeclutter.Arrange(items, minDistance: 26, maxShown: 60);

        // 50 snímků (sekunda kreslení) s 2000 místy: na stolním počítači hluboko pod sekundu.
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }

    private static IconDeclutter.Item<string> Item(string name, double x, double score) =>
        new(name, new ScreenPoint(x, 300), score);
}
