using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Tests.Orientation;

public class WalkCalibrationTests
{
    private static readonly GeoPoint Start = new(50.0871, 14.3254);

    // Krok po metru na východ (1 m ≈ 0,0000140° délky na 50° šířky).
    private static GeoPoint EastBy(double meters, double northMeters = 0) =>
        new(Start.LatitudeDegrees + northMeters / 111_320, Start.LongitudeDegrees + meters / (111_320 * Math.Cos(Start.LatitudeDegrees * Math.PI / 180)));

    [Fact]
    public void Straight_walk_east_with_a_compass_twenty_degrees_off_gives_the_correction()
    {
        var walk = new WalkCalibration();
        WalkResult result = new(WalkStatus.Walking, 0);

        for (var m = 0; m <= 16; m += 2)
        {
            for (var i = 0; i < 5; i++)
                walk.AddHeading(70 + (i - 2) * 1.5, elevationDegrees: 5);   // appka si myslí 70°, ruka se lehce kýve
            result = walk.AddFix(EastBy(m), accuracyMeters: 4);
        }

        Assert.Equal(WalkStatus.Done, result.Status);
        Assert.Equal(20, result.CorrectionDegrees, 1.0);
    }

    [Fact]
    public void Short_walk_keeps_collecting()
    {
        var walk = new WalkCalibration();

        var result = walk.AddFix(Start, 4);
        result = walk.AddFix(EastBy(6), 4);

        Assert.Equal(WalkStatus.Walking, result.Status);
        Assert.Equal(6, result.DistanceMeters, 0.5);
    }

    [Fact]
    public void Turning_a_corner_is_rejected()
    {
        var walk = new WalkCalibration();
        for (var i = 0; i < 20; i++)
            walk.AddHeading(90, 0);

        walk.AddFix(Start, 4);
        walk.AddFix(EastBy(10), 4);
        var result = walk.AddFix(EastBy(10, northMeters: 12), 4);   // za rohem doleva

        Assert.Equal(WalkStatus.Restarted, result.Status);
        Assert.Contains("rovná", result.Reason);
    }

    [Fact]
    public void Swinging_the_phone_is_rejected()
    {
        var walk = new WalkCalibration();
        WalkResult result = new(WalkStatus.Walking, 0);

        for (var m = 0; m <= 16; m += 2)
        {
            walk.AddHeading(60, 0);
            walk.AddHeading(120, 0);
            result = walk.AddFix(EastBy(m), 4);
        }

        Assert.Equal(WalkStatus.Restarted, result.Status);
        Assert.Contains("kýval", result.Reason);
    }

    [Fact]
    public void Inaccurate_gps_restarts()
    {
        var result = new WalkCalibration().AddFix(Start, accuracyMeters: 40);

        Assert.Equal(WalkStatus.Restarted, result.Status);
    }

    [Fact]
    public void Headings_around_north_average_correctly()
    {
        var walk = new WalkCalibration();
        WalkResult result = new(WalkStatus.Walking, 0);

        // Jde se na sever, appka ukazuje střídavě 355° a 5°; průměr 0°, oprava 0°.
        for (var m = 0; m <= 16; m += 2)
        {
            walk.AddHeading(355, 0);
            walk.AddHeading(5, 0);
            result = walk.AddFix(EastBy(0, northMeters: m), 4);
        }

        Assert.Equal(WalkStatus.Done, result.Status);
        Assert.Equal(0, result.CorrectionDegrees, 1.0);
    }
}
