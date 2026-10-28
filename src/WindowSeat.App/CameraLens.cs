using WindowSeat.Core.Projection;
#if ANDROID
using Android.Hardware.Camera2;
#endif

namespace WindowSeat.App;

/// <summary>Zorný úhel zadního fotoaparátu z údajů, které o objektivu hlásí systém.</summary>
public static class CameraLens
{
    public static LensFieldOfView? Rear()
    {
#if ANDROID
        var manager = (CameraManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.CameraService);
        if (manager is null)
            return null;

        foreach (var id in manager.GetCameraIdList())
        {
            var characteristics = manager.GetCameraCharacteristics(id);
            if ((characteristics.Get(CameraCharacteristics.LensFacing!) as Java.Lang.Integer)?.IntValue() != (int)LensFacing.Back)
                continue;

            // První zadní kamera je hlavní; její ohnisko a fyzická velikost snímače dají zorný úhel.
            var focalLengths = characteristics.Get(CameraCharacteristics.LensInfoAvailableFocalLengths!) is { } array
                ? Android.Runtime.JNIEnv.GetArray<float>(array.Handle)
                : null;
            var sensor = characteristics.Get(CameraCharacteristics.SensorInfoPhysicalSize!) as Android.Util.SizeF;

            if (focalLengths is { Length: > 0 } && sensor is not null)
                return LensFieldOfView.FromSensor(focalLengths[0], sensor.Width, sensor.Height);
        }
#endif
        return null;
    }

    /// <summary>Náhled přes celou obrazovku; okraje, které se nevejdou, se oříznou (výchozí je celý snímek s černými pruhy).</summary>
    public static void FillScreen(IViewHandler? handler)
    {
#if ANDROID
        if (handler?.PlatformView is AndroidX.Camera.View.PreviewView preview)
            preview.SetScaleType(AndroidX.Camera.View.PreviewView.ScaleType.FillCenter!);
#endif
    }
}
