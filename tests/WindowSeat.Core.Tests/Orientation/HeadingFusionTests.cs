using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Tests.Orientation;

public class HeadingFusionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 28, 19, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_reading_takes_the_compass_direction()
    {
        var fusion = new HeadingFusion();

        Assert.Equal(180, fusion.Update(gyroAzimuthDegrees: 37, compassAzimuthDegrees: 180, Start), 1e-9);
    }

    [Fact]
    public void Turning_the_phone_follows_the_gyroscope_immediately()
    {
        var fusion = new HeadingFusion();
        fusion.Update(0, 180, Start);

        var heading = fusion.Update(30, 180, Start.AddMilliseconds(20));

        Assert.Equal(210, heading, 0.5);
    }

    [Fact]
    public void Compass_jump_near_a_computer_barely_moves_the_heading()
    {
        var fusion = new HeadingFusion();
        fusion.Update(0, 180, Start);

        // Telefon stojí, kompas na sekundu skočí o 80°.
        double heading = 0;
        for (var ms = 20; ms <= 1000; ms += 20)
            heading = fusion.Update(0, 260, Start.AddMilliseconds(ms));

        Assert.InRange(heading, 180, 181);
        Assert.Equal(80, fusion.CompassDeviationDegrees, 1.0);
    }

    [Fact]
    public void Small_steady_compass_difference_is_absorbed_over_seconds()
    {
        var fusion = new HeadingFusion();
        fusion.Update(0, 180, Start);

        double heading = 0;
        for (var ms = 20; ms <= 30_000; ms += 20)
            heading = fusion.Update(0, 190, Start.AddMilliseconds(ms));

        Assert.Equal(190, heading, 0.6);
    }

    [Fact]
    public void Locked_heading_ignores_the_compass()
    {
        var fusion = new HeadingFusion();
        fusion.Update(0, 180, Start);
        fusion.LockTo(gyroAzimuthDegrees: 0, trueAzimuthDegrees: 151);

        double heading = 0;
        for (var ms = 20; ms <= 30_000; ms += 20)
            heading = fusion.Update(0, 180, Start.AddMilliseconds(ms));

        Assert.True(fusion.IsLocked);
        Assert.Equal(151, heading, 1e-9);
    }

    [Fact]
    public void Heading_wraps_around_north()
    {
        var fusion = new HeadingFusion();
        fusion.Update(350, 355, Start);

        var heading = fusion.Update(10, 15, Start.AddMilliseconds(20));

        Assert.Equal(15, heading, 0.5);
    }
}
