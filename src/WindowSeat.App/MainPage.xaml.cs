using System.Globalization;
using CommunityToolkit.Maui.Core;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.App;

public partial class MainPage : ContentPage
{
	private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

	// Jak daleko od popisku ještě bereme klepnutí jako výběr toho popisku.
	private const double LabelHitRadius = 90;

	private readonly PlacesOverlay _overlay = new(TestPlaces.All);

	private CancellationTokenSource? _cts;
	private Location? _gps;
	private double? _course;
	private double _declination;
	private PoseCorrection _correction;
	private (GeoPoint At, double Separation)? _geoid;
	private bool _geoidPending;
	private bool _loadingSettings;

	private CalibrationStep _calibration = CalibrationStep.Off;

	private enum CalibrationStep
	{
		Off,
		PickLabel,
		PickTarget,
	}

	public MainPage()
	{
		InitializeComponent();
		Overlay.Drawable = _overlay;

		OrientationSensor.Default.ReadingChanged += (_, e) =>
		{
			var magnetic = CameraPose.FromDeviceRotation(e.Reading.Orientation);
			MainThread.BeginInvokeOnMainThread(() => ShowPose(magnetic));
		};

		Geolocation.Default.LocationChanged += (_, e) => MainThread.BeginInvokeOnMainThread(() =>
		{
			_gps = e.Location;

			// Kurz má smysl jen v pohybu; při stání si necháme poslední platný.
			if (e.Location.Course is { } course && e.Location.Speed is > 2)
				_course = course;

			UpdateObserver();
		});

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

		if (OrientationSensor.Default.IsSupported)
			OrientationSensor.Default.Start(SensorSpeed.UI);
		else
			PoseLabel.Text = "Orientace: senzor chybí";

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
		Geolocation.Default.StopListeningForeground();

		base.OnDisappearing();
	}

	private async Task StartCameraAsync(CancellationToken token)
	{
		try
		{
			// Kamera jde ovládat až po připojení nativního ovladače; dřív hlásí „Unable to retrieve Handler“.
			if (Camera.Handler is null)
			{
				var attached = new TaskCompletionSource();
				void OnHandlerChanged(object? sender, EventArgs e)
				{
					if (Camera.Handler is not null)
						attached.TrySetResult();
				}

				Camera.HandlerChanged += OnHandlerChanged;
				try
				{
					await attached.Task.WaitAsync(token);
				}
				finally
				{
					Camera.HandlerChanged -= OnHandlerChanged;
				}
			}

			var cameras = await Camera.GetAvailableCameras(token);
			Camera.SelectedCamera = cameras.FirstOrDefault(c => c.Position == CameraPosition.Rear) ?? cameras.FirstOrDefault();
			await Camera.StartCameraPreview(token);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			ShowStatus($"Kamera: {ex.Message}");
		}
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
			source = string.Format(Invariant, "kompas {0:+0.0;-0.0}° dekl.", _declination);
			if (mode != HeadingMode.Compass)
				source += ", kurz čeká na jízdu";
		}

		pose = _correction.ApplyTo(pose);

		PoseLabel.Text = string.Format(Invariant,
			"Směr {0,5:0.0}° ({3})\nvýška {1,5:0.0}°  náklon {2,5:0.0}°",
			pose.AzimuthDegrees, pose.ElevationDegrees, pose.RollDegrees, source);

		_overlay.Pose = pose;
		Overlay.Invalidate();
	}

	private async Task StartLocationAsync()
	{
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

		var (altitude, altitudeSource) = ObserverAltitude(latitude, longitude);

		_declination = MagneticDeclination.Degrees(latitude, longitude, altitude);
		_overlay.Observer = new GeoPoint(latitude, longitude, altitude);
		Overlay.Invalidate();

		var course = _course is { } c ? c.ToString("0", Invariant) + "°" : "–";
		var speed = _gps?.Speed is { } s ? (s * 3.6).ToString("0", Invariant) + " km/h" : "–";

		LocationLabel.Text = string.Format(Invariant,
			"{0:0.00000}, {1:0.00000} ({2})\nvýška {3:0} m n. m. ({4})\nkurz {5}  rychlost {6}",
			latitude, longitude, source, altitude, altitudeSource, course, speed);
	}

	private (double Meters, string Source) ObserverAltitude(double latitude, double longitude)
	{
		if (AppSettings.ManualAltitude || _gps?.Altitude is not { } gpsAltitude)
			return (AppSettings.ManualAltitudeMeters, AppSettings.ManualAltitude ? "ruční" : "ruční, GPS ji nedává");

		if (_gps.AltitudeReferenceSystem != AltitudeReferenceSystem.Ellipsoid)
			return (gpsAltitude, "GPS");

		// Android hlásí výšku nad elipsoidem; převedeme ji na nadmořskou přes odchylku geoidu.
		var here = new GeoPoint(latitude, longitude);
		if (_geoid is { } geoid && LineOfSight.GroundDistance(geoid.At, here) < 20_000)
			return (gpsAltitude - geoid.Separation, "GPS");

		_ = RefreshGeoidAsync(here);
		return (gpsAltitude, "GPS nad elipsoidem, přepočítávám");
	}

	private async Task RefreshGeoidAsync(GeoPoint at)
	{
		if (_geoidPending)
			return;

		_geoidPending = true;
		try
		{
			if (await SeaLevelAltitude.GeoidSeparationAsync(at.LatitudeDegrees, at.LongitudeDegrees) is { } separation)
			{
				_geoid = (at, separation);
				UpdateObserver();
			}
		}
		catch (Exception ex)
		{
			ShowStatus($"Přepočet výšky: {ex.Message}");
		}
		finally
		{
			_geoidPending = false;
		}
	}

	private void OnCalibrateClicked(object? sender, EventArgs e)
	{
		SettingsPanel.IsVisible = false;
		SettingsButton.Text = "Nastavení";
		SetCalibration(CalibrationStep.PickLabel, "Klepni na popisek místa, které vidíš.");
	}

	private void OnOverlayTapped(object? sender, TappedEventArgs e)
	{
		if (_calibration == CalibrationStep.Off || e.GetPosition(Overlay) is not { } position)
			return;

		var tapped = new ScreenPoint(position.X, position.Y);

		if (_calibration == CalibrationStep.PickLabel)
		{
			var nearest = _overlay.Drawn
				.Select(label => (label, distance: DistanceToLabel(label, tapped)))
				.Where(x => x.distance < LabelHitRadius)
				.OrderBy(x => x.distance)
				.Select(x => x.label)
				.FirstOrDefault();

			if (nearest is null)
				return;

			_overlay.Highlighted = nearest.Place;
			SetCalibration(CalibrationStep.PickTarget, $"Teď klepni přesně na skutečné místo: {nearest.Place.Name}.");
			return;
		}

		if (_overlay.Highlighted is not { } place || _overlay.Pose is not { } pose
			|| _overlay.Observer is not { } observer || _overlay.LastFrame is not { } frame)
			return;

		var target = LineOfSight.Between(observer, place.Location);
		_correction = _correction.CalibrateWith(pose, frame.Projector, tapped, target, frame.Width, frame.Height);
		AppSettings.Correction = _correction;
		ShowCorrection();

		_overlay.Highlighted = null;
		SetCalibration(CalibrationStep.Off, null);
	}

	private static double DistanceToLabel(DrawnLabel label, ScreenPoint tapped)
	{
		// Klepnout jde na tečku i na text nad ní; bereme bližší z obou.
		var toDot = Math.Sqrt(Math.Pow(label.Point.X - tapped.X, 2) + Math.Pow(label.Point.Y - tapped.Y, 2));
		var toText = Math.Sqrt(Math.Pow(label.LabelCentre.X - tapped.X, 2) + Math.Pow(label.LabelCentre.Y - tapped.Y, 2));
		return Math.Min(toDot, toText);
	}

	private void OnCalibrationCancelled(object? sender, EventArgs e)
	{
		_overlay.Highlighted = null;
		SetCalibration(CalibrationStep.Off, null);
	}

	private void OnResetCorrectionClicked(object? sender, EventArgs e)
	{
		_correction = PoseCorrection.None;
		AppSettings.Correction = _correction;
		ShowCorrection();
	}

	private void SetCalibration(CalibrationStep step, string? hint)
	{
		_calibration = step;
		HintLabel.Text = hint;
		HintPanel.IsVisible = step != CalibrationStep.Off;
		Overlay.Invalidate();
	}

	private void ShowCorrection()
	{
		CorrectionLabel.Text = _correction == PoseCorrection.None
			? "Kalibrace: bez opravy"
			: string.Format(Invariant, "Kalibrace: směr {0:+0.0;-0.0}°, výška {1:+0.0;-0.0}°",
				_correction.AzimuthDegrees, _correction.ElevationDegrees);
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
		_correction = AppSettings.Correction;
		ShowCorrection();
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
