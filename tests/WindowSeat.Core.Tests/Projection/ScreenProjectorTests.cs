using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class ScreenProjectorTests
{
    private const double Width = 1000;
    private const double Height = 2000;

    private static readonly ScreenProjector Projector = new(HorizontalFieldOfViewDegrees: 60, VerticalFieldOfViewDegrees: 90);
    private static readonly CameraPose LookingNorth = new(0, 0, 0);

    [Fact]
    public void Target_on_the_camera_axis_lands_in_the_centre()
    {
        var point = Projector.Project(LookingNorth, Target(0, 0), Width, Height);

        Assert.NotNull(point);
        Assert.Equal(Width / 2, point.Value.X, 0.01);
        Assert.Equal(Height / 2, point.Value.Y, 0.01);
    }

    [Fact]
    public void Target_to_the_right_moves_right_by_the_tangent_ratio()
    {
        var point = Projector.Project(LookingNorth, Target(10, 0), Width, Height);

        var expected = Width / 2 * (1 + Math.Tan(Rad(10)) / Math.Tan(Rad(30)));
        Assert.Equal(expected, point!.Value.X, 0.01);
        Assert.Equal(Height / 2, point.Value.Y, 0.01);
    }

    [Fact]
    public void Higher_target_moves_up_the_screen()
    {
        var point = Projector.Project(LookingNorth, Target(0, 20), Width, Height);

        Assert.True(point!.Value.Y < Height / 2);
    }

    [Fact]
    public void Target_on_the_edge_of_the_field_of_view_lands_on_the_edge()
    {
        var point = Projector.Project(LookingNorth, Target(30, 0), Width, Height);

        Assert.Equal(Width, point!.Value.X, 0.01);
    }

    [Theory]
    [InlineData(31, 0)]   // těsně za pravým okrajem
    [InlineData(180, 0)]  // za zády
    [InlineData(0, 50)]   // nad horním okrajem
    public void Target_outside_the_view_is_not_projected(double azimuth, double elevation)
    {
        Assert.Null(Projector.Project(LookingNorth, Target(azimuth, elevation), Width, Height));
    }

    [Fact]
    public void Azimuth_wraps_around_north()
    {
        var point = Projector.Project(LookingNorth with { AzimuthDegrees = 355 }, Target(5, 0), Width, Height);

        var expected = Width / 2 * (1 + Math.Tan(Rad(10)) / Math.Tan(Rad(30)));
        Assert.Equal(expected, point!.Value.X, 0.01);
    }

    [Fact]
    public void Rolled_phone_shows_the_horizon_target_below_centre()
    {
        // Pravá hrana zvednutá o 90°: horní hrana míří na západ, cíl na východě je tedy dole.
        var rolled = LookingNorth with { RollDegrees = 90 };

        var point = Projector.Project(rolled, Target(10, 0), Width, Height);

        Assert.Equal(Width / 2, point!.Value.X, 0.01);
        Assert.True(point.Value.Y > Height / 2);
    }

    private static LineOfSight Target(double azimuth, double elevation) => new(azimuth, elevation, 10_000);

    private static double Rad(double degrees) => degrees * Math.PI / 180;
}
