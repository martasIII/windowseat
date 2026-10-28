using WindowSeat.Core.Geo;
using WindowSeat.Core.Places;

namespace WindowSeat.Core.Tests.Places;

public class LayerWeightsTests
{
    private static readonly Place Cathedral = new("Katedrála sv. Víta", default, PlaceKind.Church, 60);
    private static readonly Place VillageChurch = new("Kostel sv. Fabiána", default, PlaceKind.Church, 3);
    private static readonly Place Sun = new("Slunce", default, PlaceKind.CelestialBody);

    [Fact]
    public void Famous_cathedral_far_away_beats_an_unknown_church_next_door()
    {
        var weights = LayerWeights.Default;

        Assert.True(weights.Score(Cathedral, 8_000) > weights.Score(VillageChurch, 300));
    }

    [Fact]
    public void Nearer_of_two_equally_known_places_wins()
    {
        var weights = LayerWeights.Default;

        Assert.True(weights.Score(Cathedral, 2_000) > weights.Score(Cathedral, 10_000));
    }

    [Fact]
    public void Switched_off_layer_is_hidden_and_scores_zero()
    {
        var weights = LayerWeights.Default.With(PlaceKind.Church, 0);

        Assert.False(weights.IsVisible(PlaceKind.Church));
        Assert.Equal(0, weights.Score(Cathedral, 1_000));
    }

    [Fact]
    public void Sun_and_moon_cannot_be_switched_off_and_always_come_first()
    {
        var weights = LayerWeights.Default.With(PlaceKind.CelestialBody, 0);

        Assert.True(weights.IsVisible(PlaceKind.CelestialBody));
        Assert.True(weights.Score(Sun, 1e9) > weights.Score(Cathedral, 100));
    }
}
