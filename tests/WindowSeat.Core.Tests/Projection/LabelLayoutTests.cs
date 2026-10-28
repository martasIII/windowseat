using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class LabelLayoutTests
{
    private const double Width = 200;
    private const double Height = 24;

    [Fact]
    public void Distant_labels_stay_right_above_their_targets()
    {
        var placed = LabelLayout.Arrange(
        [
            new LabelLayout.Request<string>("A", new ScreenPoint(100, 500), 10),
            new LabelLayout.Request<string>("B", new ScreenPoint(600, 500), 5),
        ], Width, Height);

        Assert.All(placed, p => Assert.Equal(0, p.Level));
    }

    [Fact]
    public void Colliding_label_moves_up_and_the_more_important_keeps_the_ground_floor()
    {
        var placed = LabelLayout.Arrange(
        [
            new LabelLayout.Request<string>("most", new ScreenPoint(300, 500), 5),
            new LabelLayout.Request<string>("katedrála", new ScreenPoint(320, 505), 60),
        ], Width, Height);

        Assert.Equal(0, placed.Single(p => p.Item == "katedrála").Level);
        Assert.Equal(1, placed.Single(p => p.Item == "most").Level);
    }

    [Fact]
    public void Narrow_labels_fit_side_by_side_where_default_width_would_collide()
    {
        var placed = LabelLayout.Arrange(
        [
            new LabelLayout.Request<string>("Vít", new ScreenPoint(300, 500), 60, Width: 60),
            new LabelLayout.Request<string>("Týn", new ScreenPoint(380, 500), 38, Width: 60),
        ], Width, Height);

        Assert.All(placed, p => Assert.Equal(0, p.Level));
        Assert.All(placed, p => Assert.Equal(60, p.Width));
    }

    [Fact]
    public void Labels_that_do_not_fit_any_floor_are_dropped_least_important_first()
    {
        var requests = Enumerable.Range(1, 6)
            .Select(i => new LabelLayout.Request<int>(i, new ScreenPoint(300, 500), Importance: i));

        var placed = LabelLayout.Arrange(requests, Width, Height, maxLevels: 4);

        Assert.Equal([6, 5, 4, 3], placed.Select(p => p.Item));
    }
}
