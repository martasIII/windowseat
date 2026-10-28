using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class FanLayoutTests
{
    [Fact]
    public void First_icon_goes_straight_up()
    {
        var (dx, dy) = FanLayout.Offsets(5, spacing: 40, minRadius: 80)[0];

        Assert.Equal(0, dx, 1e-9);
        Assert.Equal(-80, dy, 1e-9);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(20)]
    public void Neighbours_keep_their_spacing_and_stay_outside_the_minimum_radius(int count)
    {
        var offsets = FanLayout.Offsets(count, spacing: 40, minRadius: 80);

        Assert.Equal(count, offsets.Count);
        Assert.All(offsets, o => Assert.True(Math.Sqrt(o.Dx * o.Dx + o.Dy * o.Dy) >= 80 - 1e-9));
        for (var i = 0; i < count; i++)
        {
            var (ax, ay) = offsets[i];
            var (bx, by) = offsets[(i + 1) % count];
            Assert.True(Math.Sqrt(Math.Pow(ax - bx, 2) + Math.Pow(ay - by, 2)) >= 40 * 0.95);
        }
    }

    [Fact]
    public void Two_times_zoom_halves_the_tangent_not_the_angle()
    {
        Assert.Equal(17.3, LensFieldOfView.Zoomed(34, 2), 0.1);
        Assert.Equal(34, LensFieldOfView.Zoomed(34, 1), 1e-9);
    }
}
