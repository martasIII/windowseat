using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class LensFieldOfViewTests
{
    [Fact]
    public void Full_frame_equivalent_26mm_lens_has_the_familiar_angles()
    {
        // 36 × 24 mm a 26 mm ohnisko: 69,4° na šířku, 49,6° na výšku.
        var lens = LensFieldOfView.FromSensor(26, 36, 24);

        Assert.Equal(69.4, lens.LongSideDegrees, 0.1);
        Assert.Equal(49.6, lens.ShortSideDegrees, 0.1);
    }

    [Fact]
    public void Tall_phone_screen_crops_the_sides_of_a_four_to_three_image()
    {
        var lens = new LensFieldOfView(LongSideDegrees: 66, ShortSideDegrees: 52);

        // Obrazovka 1080 × 2408: výška pojme celou delší stranu, šířka jen část kratší.
        var visible = lens.VisibleHorizontalDegrees(1080, 2408);

        var expected = 2 * Math.Atan(Math.Tan(33 * Math.PI / 180) * 1080 / 2408) * 180 / Math.PI;
        Assert.Equal(expected, visible, 1e-9);
        Assert.True(visible < 52);
    }

    [Fact]
    public void Wide_view_shows_the_whole_short_side()
    {
        var lens = new LensFieldOfView(66, 52);

        Assert.Equal(52, lens.VisibleHorizontalDegrees(1000, 1000), 1e-9);
    }
}
