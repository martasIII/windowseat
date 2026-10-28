namespace WindowSeat.App;

/// <summary>
/// O kolik stupňů je zeměpisný sever napravo od magnetického (kladné = východní deklinace).
/// Rotační senzor Androidu míří k magnetickému severu; v Praze je rozdíl kolem 5°.
/// </summary>
public static class MagneticDeclination
{
    public static double Degrees(double latitude, double longitude, double altitudeMeters)
    {
#if ANDROID
        var field = new Android.Hardware.GeomagneticField(
            (float)latitude, (float)longitude, (float)altitudeMeters, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        return field.Declination;
#else
        return 0;
#endif
    }
}
