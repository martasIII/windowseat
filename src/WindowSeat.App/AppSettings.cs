using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

/// <summary>Nastavení pro ladění ze země, uložené v telefonu mezi spuštěními.</summary>
public static class AppSettings
{
    public static bool ManualPosition
    {
        get => Preferences.Default.Get(nameof(ManualPosition), false);
        set => Preferences.Default.Set(nameof(ManualPosition), value);
    }

    public static double ManualLatitude
    {
        get => Preferences.Default.Get(nameof(ManualLatitude), 50.0786);
        set => Preferences.Default.Set(nameof(ManualLatitude), value);
    }

    public static double ManualLongitude
    {
        get => Preferences.Default.Get(nameof(ManualLongitude), 14.3194);
        set => Preferences.Default.Set(nameof(ManualLongitude), value);
    }

    public static bool ManualAltitude
    {
        get => Preferences.Default.Get(nameof(ManualAltitude), false);
        set => Preferences.Default.Set(nameof(ManualAltitude), value);
    }

    public static double ManualAltitudeMeters
    {
        get => Preferences.Default.Get(nameof(ManualAltitudeMeters), 350.0);
        set => Preferences.Default.Set(nameof(ManualAltitudeMeters), value);
    }

    public static HeadingMode HeadingMode
    {
        get => (HeadingMode)Preferences.Default.Get(nameof(HeadingMode), (int)HeadingMode.Compass);
        set => Preferences.Default.Set(nameof(HeadingMode), (int)value);
    }

    /// <summary>
    /// Ladění doma: když appka nese lokální soubor <c>dev-defaults.json</c> (v gitu ignorovaný, ať domácí poloha
    /// nejde do veřejného repa), při prvním spuštění zapne ruční polohu a výšku podle něj. Jen jednou,
    /// pozdější změny v nastavení zůstanou. Vrací <c>true</c>, když se něco nastavilo.
    /// </summary>
    public static async Task<bool> ApplyDevelopmentDefaultsAsync()
    {
#if DEBUG
        const string appliedKey = "DevDefaultsApplied";
        if (Preferences.Default.Get(appliedKey, false))
            return false;

        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("dev-defaults.json");
            using var json = await System.Text.Json.JsonDocument.ParseAsync(stream);
            var root = json.RootElement;

            ManualLatitude = root.GetProperty("latitude").GetDouble();
            ManualLongitude = root.GetProperty("longitude").GetDouble();
            ManualAltitudeMeters = root.GetProperty("altitudeMeters").GetDouble();
            ManualPosition = true;
            ManualAltitude = true;
            Preferences.Default.Set(appliedKey, true);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    /// <summary>Do jaké vzdálenosti ukazovat místa (km); vrcholy dvojnásobně.</summary>
    public static double RangeKm
    {
        get => Preferences.Default.Get(nameof(RangeKm), ViewRange.DefaultKm);
        set => Preferences.Default.Set(nameof(RangeKm), value);
    }

    /// <summary>Přednost vrstev; co uživatel nezměnil, má výchozí hodnotu.</summary>
    public static LayerWeights Layers
    {
        get => LayerWeights.Configurable.Aggregate(
            LayerWeights.Default,
            (layers, kind) => layers.With(kind, Preferences.Default.Get($"Layer.{kind}", LayerWeights.Default[kind])));
        set
        {
            foreach (var kind in LayerWeights.Configurable)
                Preferences.Default.Set($"Layer.{kind}", value[kind]);
        }
    }

    /// <summary>Oprava pózy z poslední kalibrace na známý cíl.</summary>
    public static PoseCorrection Correction
    {
        get => new(
            Preferences.Default.Get("CorrectionAzimuth", 0.0),
            Preferences.Default.Get("CorrectionElevation", 0.0));
        set
        {
            Preferences.Default.Set("CorrectionAzimuth", value.AzimuthDegrees);
            Preferences.Default.Set("CorrectionElevation", value.ElevationDegrees);
        }
    }

    /// <summary>
    /// Ručně doladěný zorný úhel přes šířku obrazovky, nebo <c>null</c> = automaticky z objektivu.
    /// Nový klíč: dřívější ruční hodnota platila pro náhled s černými pruhy, ne přes celou obrazovku.
    /// </summary>
    public static double? ManualFieldOfViewDegrees
    {
        get => Preferences.Default.ContainsKey(nameof(ManualFieldOfViewDegrees))
            ? Preferences.Default.Get(nameof(ManualFieldOfViewDegrees), 0.0)
            : null;
        set
        {
            if (value is { } degrees)
                Preferences.Default.Set(nameof(ManualFieldOfViewDegrees), degrees);
            else
                Preferences.Default.Remove(nameof(ManualFieldOfViewDegrees));
        }
    }
}
