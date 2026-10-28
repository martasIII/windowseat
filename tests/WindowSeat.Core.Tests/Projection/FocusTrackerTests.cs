using WindowSeat.Core.Projection;

namespace WindowSeat.Core.Tests.Projection;

public class FocusTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 28, 21, 0, 0, TimeSpan.Zero);
    private static readonly string Cathedral = "katedrála";
    private static readonly string Church = "kostel";

    private static FocusTracker<string> Tracker() => new(acquireRadius: 40, releaseRadius: 70, dwell: TimeSpan.FromMilliseconds(600));

    [Fact]
    public void Target_entering_the_ring_is_acquired_once()
    {
        var tracker = Tracker();

        var first = tracker.Update([(Cathedral, 30.0)], Start);
        var second = tracker.Update([(Cathedral, 25.0)], Start.AddMilliseconds(20));

        Assert.Equal(Cathedral, first.Focused);
        Assert.True(first.JustAcquired);
        Assert.False(second.JustAcquired);
    }

    [Fact]
    public void Target_outside_the_ring_is_not_acquired()
    {
        Assert.Null(Tracker().Update([(Cathedral, 45.0)], Start).Focused);
    }

    [Fact]
    public void Focus_holds_between_the_inner_and_outer_ring_and_ignores_a_closer_neighbour()
    {
        var tracker = Tracker();
        tracker.Update([(Cathedral, 10.0)], Start);

        // Ruka se pohne: katedrála je mimo kroužek, ale uvnitř většího okruhu; kostel je teď blíž středu.
        var state = tracker.Update([(Cathedral, 60.0), (Church, 15.0)], Start.AddMilliseconds(20));

        Assert.Equal(Cathedral, state.Focused);
    }

    [Fact]
    public void Focus_is_released_beyond_the_outer_ring_and_moves_to_the_next_target()
    {
        var tracker = Tracker();
        tracker.Update([(Cathedral, 10.0)], Start);

        var state = tracker.Update([(Cathedral, 80.0), (Church, 15.0)], Start.AddMilliseconds(20));

        Assert.Equal(Church, state.Focused);
        Assert.True(state.JustAcquired);
    }

    [Fact]
    public void Detail_appears_after_dwelling_and_only_once()
    {
        var tracker = Tracker();
        tracker.Update([(Cathedral, 10.0)], Start);

        var early = tracker.Update([(Cathedral, 10.0)], Start.AddMilliseconds(400));
        var dwelled = tracker.Update([(Cathedral, 10.0)], Start.AddMilliseconds(620));
        var later = tracker.Update([(Cathedral, 10.0)], Start.AddMilliseconds(900));

        Assert.False(early.DwellReached);
        Assert.True(dwelled.JustDwelled);
        Assert.True(later.DwellReached);
        Assert.False(later.JustDwelled);
    }

    [Fact]
    public void Nothing_in_view_clears_the_focus()
    {
        var tracker = Tracker();
        tracker.Update([(Cathedral, 10.0)], Start);

        Assert.Null(tracker.Update([], Start.AddMilliseconds(20)).Focused);
    }
}
