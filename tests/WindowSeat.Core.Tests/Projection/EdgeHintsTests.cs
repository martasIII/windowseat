using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class EdgeHintsTests
{
    [Fact]
    public void Places_in_view_get_no_arrow()
    {
        var (left, right) = EdgeHints.Select([new EdgeHints.Candidate<string>("Vít", 100, 50)], 95, 34);

        Assert.Empty(left);
        Assert.Empty(right);
    }

    [Fact]
    public void Off_screen_places_go_to_the_side_you_should_turn_to()
    {
        var (left, right) = EdgeHints.Select(
            [new EdgeHints.Candidate<string>("Hrad", 60, 50), new EdgeHints.Candidate<string>("Žižkov", 140, 30)],
            cameraAzimuthDegrees: 100,
            horizontalFieldOfViewDegrees: 34);

        Assert.Equal(-40, left.Single().TurnDegrees, 1e-9);
        Assert.Equal("Hrad", left.Single().Item);
        Assert.Equal("Žižkov", right.Single().Item);
    }

    [Fact]
    public void Places_behind_you_are_left_out()
    {
        var (left, right) = EdgeHints.Select([new EdgeHints.Candidate<string>("za zády", 280, 100)], 100, 34);

        Assert.Empty(left);
        Assert.Empty(right);
    }

    [Fact]
    public void Each_side_keeps_only_the_most_important_and_works_across_north()
    {
        var (left, _) = EdgeHints.Select(
            [
                new EdgeHints.Candidate<string>("kaple", 340, 1),
                new EdgeHints.Candidate<string>("katedrála", 320, 60),
                new EdgeHints.Candidate<string>("rozhledna", 330, 20),
            ],
            cameraAzimuthDegrees: 10,
            horizontalFieldOfViewDegrees: 34,
            perSide: 2);

        Assert.Equal(["rozhledna", "katedrála"], left.Select(h => h.Item));
    }

    [Fact]
    public void Dragging_labels_right_turns_the_camera_left()
    {
        // Ohnisko 600 bodů: posun o 600 bodů = 45°.
        var correction = PoseCorrection.None.Dragged(dxPixels: 600, dyPixels: 0, focalLengthPixels: 600);

        Assert.Equal(-45, correction.AzimuthDegrees, 1e-9);
    }
}
