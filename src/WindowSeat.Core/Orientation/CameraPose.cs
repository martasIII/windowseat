using System.Numerics;

namespace WindowSeat.Core.Orientation;

/// <summary>
/// Kam míří zadní kamera telefonu.
/// </summary>
/// <param name="AzimuthDegrees">Směr od severu po směru hodinových ručiček, 0–360°.</param>
/// <param name="ElevationDegrees">Úhel nad horizontem, −90° (dolů) až 90° (nahoru).</param>
/// <param name="RollDegrees">Natočení telefonu kolem osy kamery; kladné = pravá hrana výš než levá.</param>
public readonly record struct CameraPose(double AzimuthDegrees, double ElevationDegrees, double RollDegrees)
{
    /// <summary>
    /// Spočítá pózu kamery z natočení telefonu.
    /// </summary>
    /// <param name="deviceToWorld">
    /// Rotace ze souřadnic telefonu (X doprava, Y k horní hraně, Z ven z displeje)
    /// do světových souřadnic (X na východ, Y na sever, Z nahoru), jak ji dává rotační senzor Androidu.
    /// </param>
    public static CameraPose FromDeviceRotation(Quaternion deviceToWorld)
    {
        // Zadní kamera se dívá opačně než displej, tedy po záporné ose Z telefonu.
        var forward = Vector3.Transform(-Vector3.UnitZ, deviceToWorld);
        var up = Vector3.Transform(Vector3.UnitY, deviceToWorld);
        var right = Vector3.Transform(Vector3.UnitX, deviceToWorld);

        var azimuth = NormalizeDegrees(ToDegrees(Math.Atan2(forward.X, forward.Y)));
        var elevation = ToDegrees(Math.Asin(Math.Clamp(forward.Z, -1f, 1f)));
        var roll = ToDegrees(Math.Atan2(right.Z, up.Z));

        return new CameraPose(azimuth, elevation, roll);
    }

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

    private static double NormalizeDegrees(double degrees) => (degrees % 360.0 + 360.0) % 360.0;
}
