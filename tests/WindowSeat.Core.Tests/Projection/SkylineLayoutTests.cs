using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class SkylineLayoutTests
{
    private const double ScreenWidth = 400;
    private const double Column = 24;

    [Fact]
    public void Label_sits_right_above_its_target_when_there_is_room()
    {
        var slots = SkylineLayout.Arrange([Candidate("Vít", 200, 10)], ScreenWidth, Column);

        Assert.Equal(200, slots.Single().LabelX);
    }

    [Fact]
    public void Close_targets_get_neighbouring_columns_and_the_better_one_keeps_its_place()
    {
        var slots = SkylineLayout.Arrange([Candidate("kostel", 205, 1), Candidate("katedrála", 200, 10)], ScreenWidth, Column);

        Assert.Equal(200, slots.Single(s => s.Item == "katedrála").LabelX);
        var church = slots.Single(s => s.Item == "kostel").LabelX;
        Assert.True(Math.Abs(church - 200) >= Column);
    }

    [Fact]
    public void Labels_never_leave_the_screen()
    {
        var slots = SkylineLayout.Arrange([Candidate("kraj", -30, 1), Candidate("druhý kraj", 450, 1)], ScreenWidth, Column);

        Assert.All(slots, s => Assert.InRange(s.LabelX, Column / 2, ScreenWidth - Column / 2));
    }

    [Fact]
    public void Crowded_spot_keeps_the_best_labels_and_drops_the_rest()
    {
        var crowd = Enumerable.Range(1, 10).Select(i => Candidate($"m{i}", 200, i));

        var slots = SkylineLayout.Arrange(crowd, ScreenWidth, Column, maxShiftColumns: 2);

        // Na místě a dva sloupce na každou stranu: nejvýš pět popisků, ty s nejvyšším skóre.
        Assert.Equal(5, slots.Count);
        Assert.Equal(["m10", "m9", "m8", "m7", "m6"], slots.Select(s => s.Item));
    }

    private static SkylineLayout.Candidate<string> Candidate(string name, double x, double score) =>
        new(name, new ScreenPoint(x, 300), score);
}
