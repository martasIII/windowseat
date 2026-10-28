using WindowSeat.Core.Orientation;

namespace WindowSeat.Core.Tests.Orientation;

public class HeadingTests
{
    [Theory]
    [InlineData(0, HeadingMode.CourseLeftWindow, 270)]   // na sever, levé okno = západ
    [InlineData(0, HeadingMode.CourseRightWindow, 90)]   // na sever, pravé okno = východ
    [InlineData(300, HeadingMode.CourseRightWindow, 30)]
    [InlineData(45, HeadingMode.CourseLeftWindow, 315)]
    public void Side_window_looks_ninety_degrees_off_the_course(double course, HeadingMode mode, double expected)
    {
        Assert.Equal(expected, Heading.CameraAzimuthFromCourse(course, mode), 1e-9);
    }

    [Fact]
    public void Compass_mode_has_no_course_based_azimuth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Heading.CameraAzimuthFromCourse(0, HeadingMode.Compass));
    }
}
