using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Tests.Orientation;

public class PoseSmootherTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 28, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Jitter_is_damped()
    {
        var smoother = new PoseSmoother(TimeSpan.FromMilliseconds(150));
        smoother.Smooth(new CameraPose(100, 0, 0), Start);

        // Ruka cukne o 2° na jeden snímek.
        var pose = smoother.Smooth(new CameraPose(102, 0, 0), Start.AddMilliseconds(20));

        Assert.InRange(pose.AzimuthDegrees, 100.1, 100.5);
    }

    [Fact]
    public void Steady_turn_settles_within_a_second()
    {
        var smoother = new PoseSmoother(TimeSpan.FromMilliseconds(150));
        smoother.Smooth(new CameraPose(100, 0, 0), Start);

        CameraPose pose = default;
        for (var ms = 20; ms <= 1000; ms += 20)
            pose = smoother.Smooth(new CameraPose(130, 10, 5), Start.AddMilliseconds(ms));

        Assert.Equal(130, pose.AzimuthDegrees, 0.1);
        Assert.Equal(10, pose.ElevationDegrees, 0.1);
        Assert.Equal(5, pose.RollDegrees, 0.1);
    }

    [Fact]
    public void Turning_through_north_takes_the_short_way()
    {
        var smoother = new PoseSmoother(TimeSpan.FromMilliseconds(150));
        smoother.Smooth(new CameraPose(358, 0, 0), Start);

        var pose = smoother.Smooth(new CameraPose(2, 0, 0), Start.AddMilliseconds(20));

        // Mezi 358° a 2° přes sever, ne přes jih.
        Assert.True(pose.AzimuthDegrees > 358 || pose.AzimuthDegrees < 2);
    }
}
