namespace WindowSeat.Core.Orientation;

/// <summary>
/// Vyhlazuje pózu kamery, aby se popisky netřásly s rukou. Exponenciální filtr s časovou konstantou:
/// drobné chvění utlumí, plynulé otočení doběhne se zpožděním zlomku sekundy. Úhly bere kratší cestou přes sever.
/// </summary>
public sealed class PoseSmoother(TimeSpan timeConstant)
{
    private CameraPose? _smoothed;
    private DateTimeOffset _last;

    public CameraPose Smooth(CameraPose pose, DateTimeOffset now)
    {
        if (_smoothed is not { } previous)
        {
            _smoothed = pose;
            _last = now;
            return pose;
        }

        var elapsed = Math.Max(0, (now - _last).TotalSeconds);
        _last = now;
        var gain = 1 - Math.Exp(-elapsed / timeConstant.TotalSeconds);

        var smoothed = new CameraPose(
            Wrap(previous.AzimuthDegrees + SignedDifference(pose.AzimuthDegrees, previous.AzimuthDegrees) * gain),
            previous.ElevationDegrees + (pose.ElevationDegrees - previous.ElevationDegrees) * gain,
            previous.RollDegrees + SignedDifference(pose.RollDegrees, previous.RollDegrees) * gain);

        _smoothed = smoothed;
        return smoothed;
    }

    private static double SignedDifference(double to, double from) => ((to - from) % 360 + 540) % 360 - 180;

    private static double Wrap(double degrees) => (degrees % 360 + 360) % 360;
}
