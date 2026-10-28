using System.Globalization;
using CommunityToolkit.Maui.Core;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;

namespace WindowSeat.App;

public partial class MainPage : ContentPage
{
	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	private readonly PlacesOverlay _overlay = new(TestPlaces.All);

	private CancellationTokenSource? _cts;
	private Location? _gps;
	private double? _course;
	private double _declination;
	private bool _loadingSettings;

	public MainPage()
	{
		InitializeComponent();
		Overlay.Drawable = _overlay;
		LoadSettings();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_cts = new CancellationTokenSource();
		UpdateObserver();

		if (await Permissions.RequestAsync<Permissions.Camera>() == PermissionStatus.Granted)
			await StartCameraAsync(_cts.Token);
		else
			ShowStatus("Bez oprávnění ke kameře není náhled.");

		StartOrientation();
		StartCompass();

		if (await Permissions.RequestAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted)
			await StartLocationAsync();
		else
			ShowStatus("Bez oprávnění k poloze funguje jen ruční poloha.");
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
			var magnetic = CameraPose.FromDeviceRotation(e.Reading.Orientation);
			MainThread.BeginInvokeOnMainThread(() => ShowPose(magnetic));
		};
		OrientationSensor.Default.Start(SensorSpeed.UI);
	}

	private void ShowPose(CameraPose magnetic)
	{
		var mode = AppSettings.HeadingMode;
		CameraPose pose;
		string source;

		if (mode != HeadingMode.Compass && _course is { } course)
		{
			// Ve vozidle bereme směr z kurzu; náklon a výšku pohledu dál ze senzoru, ty kov neruší.
			pose = magnetic with { AzimuthDegrees = Heading.CameraAzimuthFromCourse(course, mode) };
			source = "kurz GPS";
		}
		else
		{
			// Senzor míří k magnetickému severu, místa máme vůči zeměpisnému.
			pose = magnetic with { AzimuthDegrees = (magnetic.AzimuthDegrees + _declination + 360) % 360 };
			source = mode == HeadingMode.Compass ? "kompas" : "kompas, kurz čeká na jízdu";
		}

		PoseLabel.Text = string.Format(Invariant,
			"Směr {0,5:0.0}° ({3})\nvýška {1,5:0.0}°  náklon {2,5:0.0}°",
			pose.AzimuthDegrees, pose.ElevationDegrees, pose.RollDegrees, source);

		_overlay.Pose = pose;
		Overlay.Invalidate();
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
			var heading = e.Reading.HeadingMagneticNorth;
			MainThread.BeginInvokeOnMainThread(() => CompassLabel.Text = string.Format(Invariant,
				"Kompas {0,5:0.0}° magn.  deklinace {1:+0.0;-0.0}°", heading, _declination));
		};
		Compass.Default.Start(SensorSpeed.UI, applyLowPassFilter: true);
	}

	private async Task StartLocationAsync()
	{
		Geolocation.Default.LocationChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
		{
			_gps = e.Location;

			// Kurz má smysl jen v pohybu; při stání si necháme poslední platný.
			if (e.Location.Course is { } course && e.Location.Speed is > 2)
				_course = course;

			UpdateObserver();
		});

		var started = await Geolocation.Default.StartListeningForegroundAsync(
			new GeolocationListeningRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(1)));

		if (!started)
			ShowStatus("GPS nelze spustit, zapni ruční polohu.");
	}

	/// <summary>Složí polohu pozorovatele z GPS a ručního nastavení.</summary>
	private void UpdateObserver()
	{
		double latitude, longitude;
		string source;

		if (AppSettings.ManualPosition)
			(latitude, longitude, source) = (AppSettings.ManualLatitude, AppSettings.ManualLongitude, "ruční");
		else if (_gps is not null)
			(latitude, longitude, source) = (_gps.Latitude, _gps.Longitude, $"GPS ±{_gps.Accuracy ?? 0:0} m");
		else
		{
			LocationLabel.Text = "Poloha: čekám na GPS (nebo zapni ruční)";
			return;
		}

		double altitude;
		string altitudeSource;
		if (AppSettings.ManualAltitude)
			(altitude, altitudeSource) = (AppSettings.ManualAltitudeMeters, "ruční");
		else if (_gps?.Altitude is { } gpsAltitude)
			(altitude, altitudeSource) = (gpsAltitude, "GPS");
		else
			(altitude, altitudeSource) = (AppSettings.ManualAltitudeMeters, "ruční, GPS ji nedává");

		_declination = MagneticDeclination.Degrees(latitude, longitude, altitude);
		_overlay.Observer = new GeoPoint(latitude, longitude, altitude);
		Overlay.Invalidate();

		var course = _course is { } c ? c.ToString("0", Invariant) + "°" : "–";
		var speed = _gps?.Speed is { } s ? (s * 3.6).ToString("0", Invariant) + " km/h" : "–";

		LocationLabel.Text = string.Format(Invariant,
			"{0:0.00000}, {1:0.00000} ({2})\nvýška {3:0} m ({4})\nkurz {5}  rychlost {6}",
			latitude, longitude, source, altitude, altitudeSource, course, speed);
	}

	private void LoadSettings()
	{
		// Přepínače při nastavení hodnoty vyvolají Toggled; bez příznaku by přepsaly uložené hodnoty rozpracovaným stavem.
		_loadingSettings = true;
		HeadingPicker.SelectedIndex = (int)AppSettings.HeadingMode;
		PositionEntry.Text = string.Format(Invariant, "{0:0.0000000}, {1:0.0000000}", AppSettings.ManualLatitude, AppSettings.ManualLongitude);
		AltitudeEntry.Text = AppSettings.ManualAltitudeMeters.ToString("0", Invariant);
		ManualPositionSwitch.IsToggled = AppSettings.ManualPosition;
		ManualAltitudeSwitch.IsToggled = AppSettings.ManualAltitude;
		FieldOfViewSlider.Value = AppSettings.HorizontalFieldOfViewDegrees;
		ApplyFieldOfView(AppSettings.HorizontalFieldOfViewDegrees);
		_loadingSettings = false;
	}

	private void OnSettingsChanged(object? sender, EventArgs e)
	{
		if (_loadingSettings)
			return;

		if (CoordinateParser.TryParse(PositionEntry.Text, out var latitude, out var longitude))
		{
			AppSettings.ManualLatitude = latitude;
			AppSettings.ManualLongitude = longitude;
			PositionEntry.TextColor = Colors.White;
		}
		else
			PositionEntry.TextColor = Color.FromArgb("#F09595");

		if (double.TryParse(AltitudeEntry.Text?.Replace(',', '.'), NumberStyles.Float, Invariant, out var altitude))
			AppSettings.ManualAltitudeMeters = altitude;

		if (HeadingPicker.SelectedIndex >= 0)
			AppSettings.HeadingMode = (HeadingMode)HeadingPicker.SelectedIndex;

		AppSettings.ManualPosition = ManualPositionSwitch.IsToggled;
		AppSettings.ManualAltitude = ManualAltitudeSwitch.IsToggled;
		UpdateObserver();
	}

	private void OnFieldOfViewChanged(object? sender, ValueChangedEventArgs e)
	{
		if (_loadingSettings)
			return;

		var degrees = Math.Round(e.NewValue, 1);
		AppSettings.HorizontalFieldOfViewDegrees = degrees;
		ApplyFieldOfView(degrees);
	}

	private void ApplyFieldOfView(double degrees)
	{
		FieldOfViewLabel.Text = string.Format(Invariant, "Zorný úhel na šířku: {0:0.0}°", degrees);
		_overlay.HorizontalFieldOfViewDegrees = degrees;
		Overlay.Invalidate();
	}

	private void OnSettingsClicked(object? sender, EventArgs e)
	{
		SettingsPanel.IsVisible = !SettingsPanel.IsVisible;
		SettingsButton.Text = SettingsPanel.IsVisible ? "Hotovo" : "Nastavení";
	}

	private void ShowStatus(string message)
	{
		StatusLabel.Text = message;
		StatusLabel.IsVisible = true;
	}
}
