namespace WindowSeat.App;

/// <summary>
/// Android hlásí výšku nad elipsoidem WGS84, místa ale máme nad mořem; v Praze je rozdíl kolem 45 m.
/// Odchylku geoidu spočítá sám Android (od verze 14) z vestavěného modelu, bez sítě.
/// </summary>
public static class SeaLevelAltitude
{
    /// <summary>O kolik je elipsoid pod hladinou moře v daném místě (výška nad mořem = elipsoidická − tato hodnota), nebo <c>null</c>, když to systém neumí.</summary>
    public static Task<double?> GeoidSeparationAsync(double latitude, double longitude)
    {
#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(34))
            return Task.FromResult<double?>(null);

        // Převodník může sahat na disk, proto mimo hlavní vlákno.
        return Task.Run<double?>(() =>
        {
            const double reference = 1000;
            using var location = new Android.Locations.Location("windowseat")
            {
                Latitude = latitude,
                Longitude = longitude,
                Altitude = reference,
            };
            new Android.Locations.Altitude.AltitudeConverter().AddMslAltitudeToLocation(Android.App.Application.Context, location);
            return location.HasMslAltitude ? reference - location.MslAltitudeMeters : null;
        });
#else
        return Task.FromResult<double?>(null);
#endif
    }
}
