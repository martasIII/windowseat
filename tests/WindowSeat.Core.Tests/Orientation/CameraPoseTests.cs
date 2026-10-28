using System.Numerics;
using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Tests.Orientation;

public class CameraPoseTests
{
    private const double Tolerance = 0.01;

    // Telefon na výšku, displejem k sobě, kamera míří vodorovně na sever.
    private static readonly Quaternion UprightFacingNorth = Rotation(Vector3.UnitX, 90);

    [Fact]
    public void Phone_lying_flat_screen_up_looks_straight_down()
    {
        var pose = CameraPose.FromDeviceRotation(Quaternion.Identity);

        Assert.Equal(-90, pose.ElevationDegrees, Tolerance);
    }

    [Fact]
    public void Upright_phone_looks_north_at_the_horizon_without_roll()
    {
        var pose = CameraPose.FromDeviceRotation(UprightFacingNorth);

        Assert.Equal(0, pose.AzimuthDegrees, Tolerance);
        Assert.Equal(0, pose.ElevationDegrees, Tolerance);
        Assert.Equal(0, pose.RollDegrees, Tolerance);
    }

    [Theory]
    [InlineData(-90, 90)]  // otočení doprava = na východ
    [InlineData(180, 180)]
    [InlineData(90, 270)]  // otočení doleva = na západ
    [InlineData(-30, 30)]
    public void Turning_around_the_vertical_axis_changes_azimuth(float turnDegrees, double expectedAzimuth)
    {
        var rotation = Quaternion.Concatenate(UprightFacingNorth, Rotation(Vector3.UnitZ, turnDegrees));

        var pose = CameraPose.FromDeviceRotation(rotation);

        Assert.Equal(expectedAzimuth, pose.AzimuthDegrees, Tolerance);
        Assert.Equal(0, pose.ElevationDegrees, Tolerance);
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(-15, -15)]
    public void Tilting_the_top_edge_back_raises_the_camera(float tiltDegrees, double expectedElevation)
    {
        var pose = CameraPose.FromDeviceRotation(Rotation(Vector3.UnitX, 90 + tiltDegrees));

        Assert.Equal(0, pose.AzimuthDegrees, Tolerance);
        Assert.Equal(expectedElevation, pose.ElevationDegrees, Tolerance);
    }

    [Fact]
    public void Lifting_the_right_edge_gives_positive_roll_and_keeps_direction()
    {
        // Rotace kolem osy pohledu (sever = světová Y) o −20° zvedá pravou hranu.
        var rotation = Quaternion.Concatenate(UprightFacingNorth, Rotation(Vector3.UnitY, -20));

        var pose = CameraPose.FromDeviceRotation(rotation);

        Assert.Equal(20, pose.RollDegrees, Tolerance);
        Assert.Equal(0, pose.AzimuthDegrees, Tolerance);
        Assert.Equal(0, pose.ElevationDegrees, Tolerance);
    }

    private static Quaternion Rotation(Vector3 axis, float degrees) =>
        Quaternion.CreateFromAxisAngle(axis, degrees * MathF.PI / 180f);
}
