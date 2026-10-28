using System.Globalization;
using CommunityToolkit.Maui.Core;
using WindowSeat.Core.Orientation;

namespace WindowSeat.App;

public partial class MainPage : ContentPage
{
	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	private CancellationTokenSource? _cts;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_cts = new CancellationTokenSource();

		if (await Permissions.RequestAsync<Permissions.Camera>() == PermissionStatus.Granted)
			await StartCameraAsync(_cts.Token);
		else
			ShowStatus("Bez oprávnění ke kameře není náhled.");

		StartOrientation();
		StartCompass();

		if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted)
			await StartLocationAsync();
		else
			LocationLabel.Text = "GPS: bez oprávnění";
	}

	protected override void OnDisappearing()
	{
		_cts?.Cancel();
		Camera.StopCameraPreview();

		if (OrientationSensor.Default.IsMonitoring)
			OrientationSensor.Default.Stop();
		if (Compass.Default.IsMonitoring)
			Compass.Default.Stop();
		Geolocation.Default.StopListeningForeground();

		base.OnDisappearing();
	}

	private async Task StartCameraAsync(CancellationToken token)
	{
		try
		{
			var cameras = await Camera.GetAvailableCameras(token);
			Camera.SelectedCamera = cameras.FirstOrDefault(c => c.Position == CameraPosition.Rear) ?? cameras.FirstOrDefault();
			await Camera.StartCameraPreview(token);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			ShowStatus($"Kamera: {ex.Message}");
		}
	}

	private void StartOrientation()
	{
		if (!OrientationSensor.Default.IsSupported)
		{
			PoseLabel.Text = "Orientace: senzor chybí";
			return;
		}

		OrientationSensor.Default.ReadingChanged += (_, e) =>
		{
			var pose = CameraPose.FromDeviceRotation(e.Reading.Orientation);
			var text = string.Format(Invariant,
				"Směr {0,5:0.0}°  výška {1,5:0.0}°  náklon {2,5:0.0}°",
				pose.AzimuthDegrees, pose.ElevationDegrees, pose.RollDegrees);
			MainThread.BeginInvokeOnMainThread(() => PoseLabel.Text = text);
		};
		OrientationSensor.Default.Start(SensorSpeed.UI);
	}

	private void StartCompass()
	{
		if (!Compass.Default.IsSupported)
		{
			CompassLabel.Text = "Kompas: chybí";
			return;
		}

		Compass.Default.ReadingChanged += (_, e) =>
		{
			var text = string.Format(Invariant, "Kompas {0,5:0.0}° (magnetický)", e.Reading.HeadingMagneticNorth);
			MainThread.BeginInvokeOnMainThread(() => CompassLabel.Text = text);
		};
		Compass.Default.Start(SensorSpeed.UI, applyLowPassFilter: true);
	}

	private async Task StartLocationAsync()
	{
		Geolocation.Default.LocationChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() => ShowLocation(e.Location));

		var started = await Geolocation.Default.StartListeningForegroundAsync(
			new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1)));

		if (!started)
			LocationLabel.Text = "GPS: nelze spustit";
	}

	private void ShowLocation(Location location)
	{
		var altitude = location.Altitude is { } a ? a.ToString("0", Invariant) + " m" : "?";
		var course = location.Course is { } c ? c.ToString("0", Invariant) + "°" : "–";
		var speed = location.Speed is { } s ? (s * 3.6).ToString("0", Invariant) + " km/h" : "–";

		LocationLabel.Text = string.Format(Invariant,
			"{0:0.00000}, {1:0.00000}  ±{2:0} m\nvýška {3}  kurz {4}  {5}",
			location.Latitude, location.Longitude, location.Accuracy ?? 0, altitude, course, speed);
	}

	private void ShowStatus(string message)
	{
		StatusLabel.Text = message;
		StatusLabel.IsVisible = true;
	}
}
