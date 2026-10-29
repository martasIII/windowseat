using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class PoseCorrectionTests
{
    private const double Width = 900;
    private const double Height = 2000;

    private static readonly ScreenProjector Projector = new(50, 2 * Math.Atan(Math.Tan(25 * Math.PI / 180) * Height / Width) * 180 / Math.PI);

    [Fact]
    public void Unproject_reverses_project()
    {
        var pose = new CameraPose(144.4, 9.9, -6.9);
        var target = new LineOfSight(151.3, 8.3, 350);

        var point = Projector.Project(pose, target, Width, Height)!.Value;
        var back = Projector.Unproject(pose, point, Width, Height);

        Assert.Equal(target.AzimuthDegrees, back.AzimuthDegrees, 1e-6);
        Assert.Equal(target.ElevationDegrees, back.ElevationDegrees, 1e-6);
    }

    [Fact]
    public void Tap_on_the_real_target_moves_its_label_there()
    {
        // Telefon ve skutečnosti míří na 154,4°, kompas tvrdí 144,4° (případ Hvězdy z 28. 10.).
        var truePose = new CameraPose(154.4, 10, 0);
        var sensorPose = truePose with { AzimuthDegrees = 144.4, ElevationDegrees = 8 };
        var star = new LineOfSight(151.3, 10.5, 350);
        var whereUserSeesIt = Projector.Project(truePose, star, Width, Height)!.Value;

        var correction = PoseCorrection.None.CalibrateWith(sensorPose, Projector, whereUserSeesIt, star, Width, Height);
        var labelAfter = Projector.Project(correction.ApplyTo(sensorPose), star, Width, Height)!.Value;

        Assert.Equal(10, correction.AzimuthDegrees, 0.1);
        Assert.Equal(2, correction.ElevationDegrees, 0.1);
        Assert.Equal(whereUserSeesIt.X, labelAfter.X, 1.0);
        Assert.Equal(whereUserSeesIt.Y, labelAfter.Y, 1.0);
    }

    [Fact]
    public void Second_calibration_refines_the_first()
    {
        var truePose = new CameraPose(20, 0, 0);
        var sensorPose = new CameraPose(10, 0, 0);
        var first = new PoseCorrection(7, 0);
        var target = new LineOfSight(25, 0, 1000);
        var tapped = Projector.Project(truePose, target, Width, Height)!.Value;

        var refined = first.CalibrateWith(first.ApplyTo(sensorPose), Projector, tapped, target, Width, Height);

        Assert.Equal(10, refined.AzimuthDegrees, 0.1);
    }

    [Fact]
    public void Correction_wraps_around_north()
    {
        var truePose = new CameraPose(5, 0, 0);
        var sensorPose = new CameraPose(355, 0, 0);
        var target = new LineOfSight(8, 0, 1000);
        var tapped = Projector.Project(truePose, target, Width, Height)!.Value;

        var correction = PoseCorrection.None.CalibrateWith(sensorPose, Projector, tapped, target, Width, Height);

        Assert.Equal(10, correction.AzimuthDegrees, 0.1);
        Assert.Equal(5, correction.ApplyTo(sensorPose).AzimuthDegrees, 0.1);
    }

    [Fact]
    public void Repeated_vertical_drags_cannot_tilt_the_view_into_the_ground()
    {
        // Případ z Letné 29. 10.: několik tahů prstem dolů nasčítalo opravu −97°.
        var correction = PoseCorrection.None;
        for (var i = 0; i < 5; i++)
            correction = correction.Dragged(dxPixels: 0, dyPixels: -800, focalLengthPixels: 600);

        Assert.Equal(-PoseCorrection.MaxElevationDegrees, correction.ElevationDegrees, 1e-9);
    }

    [Fact]
    public void Azimuth_correction_stays_within_half_a_turn()
    {
        Assert.Equal(-10, new PoseCorrection(350, 0).AzimuthDegrees, 1e-9);
        Assert.Equal(10, new PoseCorrection(-350, 0).AzimuthDegrees, 1e-9);
    }
}
