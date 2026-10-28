using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class PolygonProjectionTests
{
    private const double Width = 1000;
    private const double Height = 2000;

    private static readonly ScreenProjector Projector = new(60, 90);
    private static readonly CameraPose LookingNorth = new(0, 0, 0);

    [Fact]
    public void Polygon_in_front_matches_single_point_projection()
    {
        LineOfSight[] ring = [Los(-10, -5), Los(10, -5), Los(10, -15), Los(-10, -15)];

        var polygon = Projector.ProjectPolygon(LookingNorth, ring, Width, Height);

        Assert.Equal(4, polygon.Count);
        for (var i = 0; i < ring.Length; i++)
        {
            var single = Projector.Project(LookingNorth, ring[i], Width, Height)!.Value;
            Assert.Equal(single.X, polygon[i].X, 1e-6);
            Assert.Equal(single.Y, polygon[i].Y, 1e-6);
        }
    }

    [Fact]
    public void Part_behind_the_camera_is_clipped_instead_of_flipped()
    {
        // Řeka zprava zepředu dozadu: dva body před kamerou, dva za ní.
        LineOfSight[] ring = [Los(30, -5), Los(60, -5), Los(150, -5), Los(120, -5)];

        var polygon = Projector.ProjectPolygon(LookingNorth, ring, Width, Height);

        Assert.Equal(4, polygon.Count);
        Assert.All(polygon, p => Assert.True(p.X > Width / 2, "vše vpravo od středu"));
    }

    [Fact]
    public void Polygon_behind_is_empty()
    {
        LineOfSight[] ring = [Los(170, -5), Los(190, -5), Los(190, -15)];

        Assert.Empty(Projector.ProjectPolygon(LookingNorth, ring, Width, Height));
    }

    private static LineOfSight Los(double azimuth, double elevation) => new(azimuth, elevation, 1_000);
}
