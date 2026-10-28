using System.Numerics;
#if ANDROID
using Android.Content;
using Android.Hardware;
#endif

namespace WindowSeat.App;

/// <summary>
/// Natočení telefonu dvakrát: z gyroskopu (bez kompasu, magnety ho neruší, ale nezná sever)
/// a s kompasem (zná sever, ale skáče u kovu). Obojí jako rotace telefon → svět (východ, sever, nahoru).
/// </summary>
/// <param name="Gyro">Rotace z gyroskopu a akcelerometru; vodorovná nula je libovolná.</param>
/// <param name="Compass">Rotace včetně kompasu, vůči magnetickému severu; <c>null</c>, dokud nepřišla.</param>
public readonly record struct OrientationReading(Quaternion Gyro, Quaternion? Compass, DateTimeOffset Timestamp);

#if ANDROID
public sealed class OrientationSource : Java.Lang.Object, ISensorEventListener
{
    private SensorManager? _manager;
    private Quaternion? _compass;

    public event Action<OrientationReading>? ReadingChanged;

    /// <summary>Spustí čtení; <c>false</c>, když telefon nemá gyroskop.</summary>
    public bool Start()
    {
        _manager = (SensorManager?)Android.App.Application.Context.GetSystemService(Context.SensorService);
        var gyro = _manager?.GetDefaultSensor(SensorType.GameRotationVector);
        if (_manager is null || gyro is null)
            return false;

        _manager.RegisterListener(this, gyro, SensorDelay.Game);
        if (_manager.GetDefaultSensor(SensorType.RotationVector) is { } compass)
            _manager.RegisterListener(this, compass, SensorDelay.Game);
        return true;
    }

    public void Stop()
    {
        _manager?.UnregisterListener(this);
        _compass = null;
    }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values is not { Count: >= 3 } values)
            return;

        var q = new float[4];
        SensorManager.GetQuaternionFromVector(q, values.ToArray());
        var rotation = new Quaternion(q[1], q[2], q[3], q[0]);

        // Kompasovou orientaci si jen pamatujeme; čtení posíláme v rytmu gyroskopu.
        if (e.Sensor?.Type == SensorType.RotationVector)
            _compass = rotation;
        else
            ReadingChanged?.Invoke(new OrientationReading(rotation, _compass, DateTimeOffset.UtcNow));
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy)
    {
    }
}
#else
/// <summary>Mimo Android zatím jen orientace z MAUI, která kompas obsahuje; gyroskop a kompas jsou tu totéž.</summary>
public sealed class OrientationSource
{
    public event Action<OrientationReading>? ReadingChanged;

    public bool Start()
    {
        if (!OrientationSensor.Default.IsSupported)
            return false;

        OrientationSensor.Default.ReadingChanged += OnReading;
        OrientationSensor.Default.Start(SensorSpeed.Game);
        return true;
    }

    public void Stop()
    {
        if (!OrientationSensor.Default.IsMonitoring)
            return;

        OrientationSensor.Default.Stop();
        OrientationSensor.Default.ReadingChanged -= OnReading;
    }

    private void OnReading(object? sender, OrientationSensorChangedEventArgs e) =>
        ReadingChanged?.Invoke(new OrientationReading(e.Reading.Orientation, e.Reading.Orientation, DateTimeOffset.UtcNow));
}
#endif
