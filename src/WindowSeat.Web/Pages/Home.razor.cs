using System.Globalization;
using System.Numerics;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WindowSeat.Core.Composition;
using WindowSeat.Core.Geo;
using WindowSeat.Core.Orientation;
using WindowSeat.Core.Places;
using WindowSeat.Core.Projection;

namespace WindowSeat.Web.Pages;

/// <summary>
/// Webová verze: kamera, senzory a kreslení obstará JavaScript (wwwroot/js/windowseat.js), všechno ostatní
/// sdílená knihovna stejně jako v appce: spojení gyroskopu s kompasem, vyhlazení, kalibrace a skládání scény.
/// </summary>
public partial class Home : IAsyncDisposable
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Typický hlavní fotoaparát telefonu (ohnisko ~26 mm na kinofilm): web parametry objektivu nezjistí.
    private static readonly LensFieldOfView TypicalLens = new(LongSideDegrees: 68, ShortSideDegrees: 53);

    private const double LabelHitRadius = 60;

    private readonly SceneComposer _composer = new();
    private readonly HeadingFusion _fusion = new();
    private readonly PoseSmoother _smoother = new(TimeSpan.FromMilliseconds(150));

    private ElementReference _video;
    private ElementReference _canvas;
    private ElementReference _status;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Home>? _self;

    private bool _dataLoaded;
    private bool _started;
    private string? _loadError;
    private string? _startProblems;

    private PoseCorrection _correction;
    private PoseCorrection _dragStart;
    private bool _dragging;
    private double _lastGyroAzimuth;
    private double _lastFusedAzimuth;
    private double _declination;
    private double? _gpsAccuracy;
    private Scene? _lastScene;
    private CameraPose? _lastPose;

    private SceneHit? _detail;
    private bool _pinned;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Praha podrobně, okolí jen to, co je vidět zdaleka; Vltava a mosty jako test přesnosti.
            IReadOnlyList<Place> places = [];
            foreach (var file in new[] { "data/praha.json", "data/okoli.json" })
            {
                await using var stream = await Http.GetStreamAsync(file);
                places = PlaceCatalog.Merge(places, (await PlaceCatalog.ReadAsync(stream)).Places);
            }

            _composer.Places = PlaceCatalog.Merge(places, TestPlaces.All);

            await using (var water = await Http.GetStreamAsync("data/vltava.json"))
                _composer.Water = (await WaterCatalog.ReadAsync(water)).Areas;
            await using (var bridges = await Http.GetStreamAsync("data/mosty.json"))
                _composer.Bridges = (await WaterCatalog.ReadAsync(bridges)).Areas;

            _dataLoaded = true;
        }
        catch (Exception ex)
        {
            _loadError = $"Data se nenačetla: {ex.Message}";
        }
    }

    private async Task StartAsync()
    {
        _module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/windowseat.js");
        _self ??= DotNetObjectReference.Create(this);

        var result = await _module.InvokeAsync<StartResult>("start", _self, _video, _canvas, _status);
        var problems = new[] { ("Senzory pohybu", result.Motion), ("Kamera", result.Camera), ("Poloha", result.Location) }
            .Where(p => p.Item2 is not ("ok" or "granted"))
            .Select(p => $"{p.Item1}: {p.Item2}")
            .ToList();

        // Spouštíme i s problémem (bez kamery jsou popisky na černém, bez kompasu pomůže kalibrace); jen ho ukážeme.
        _startProblems = problems.Count > 0 ? string.Join(" · ", problems) : null;
        _started = true;
    }

    /// <summary>Poloha z GPS. Výška z prohlížeče je nad elipsoidem a Android ji často nedá vůbec.</summary>
    [JSInvokable]
    public void OnLocation(double latitude, double longitude, double? altitude, double accuracy, double? speed)
    {
        _gpsAccuracy = accuracy;
        var seaLevel = altitude is { } ellipsoid
            ? ellipsoid - ApproximateGeoidSeparation(latitude, longitude)
            : _composer.Observer?.AltitudeMeters ?? FallbackAltitudeMeters;

        _composer.Observer = new GeoPoint(latitude, longitude, seaLevel);
        _declination = ApproximateDeclination(latitude, longitude);
    }

    [JSInvokable]
    public void OnLocationError(string message) => _gpsAccuracy = null;

    /// <summary>
    /// Jeden snímek: z natočení telefonu spočítá pózu kamery a složí scénu, kterou JavaScript nakreslí.
    /// </summary>
    /// <param name="gyro">Natočení bez kompasu [x, y, z, w].</param>
    /// <param name="compass">Natočení s kompasem (Android), nebo <c>null</c>.</param>
    /// <param name="heading">Kompas z iPhonu ve stupních od magnetického severu, nebo <c>null</c>.</param>
    [JSInvokable]
    public SceneDto Frame(double[] gyro, double[]? compass, double? heading, double width, double height)
    {
        var now = DateTimeOffset.UtcNow;
        var gyroPose = CameraPose.FromDeviceRotation(Quaternion(gyro));

        double? compassAzimuth = compass is not null
            ? CameraPose.FromDeviceRotation(Quaternion(compass)).AzimuthDegrees + _declination
            : heading is { } h ? h + _declination : null;

        _lastGyroAzimuth = gyroPose.AzimuthDegrees;
        _lastFusedAzimuth = _fusion.Update(gyroPose.AzimuthDegrees, compassAzimuth, now);

        var pose = _smoother.Smooth(_correction.ApplyTo(gyroPose with { AzimuthDegrees = _lastFusedAzimuth }), now);
        _lastPose = pose;

        var fieldOfView = TypicalLens.VisibleHorizontalDegrees(width, height);
        var scene = _composer.Compose(pose, width, height, fieldOfView, now, Measure);
        _lastScene = scene;

        // Kartička po podržení v kroužku; zmizí, když kroužek z místa sjede (pokud není přišpendlená).
        if (!_pinned)
        {
            if (scene.FocusDwelled && scene.Focus is { } focus)
                SetDetail(new SceneHit(focus.Place, focus.Los, focus.Point));
            else if (scene.Focus is null && _detail is not null)
                SetDetail(null);
        }

        return SceneDto.From(scene, Status(pose, compassAzimuth));
    }

    [JSInvokable]
    public void OnTap(double x, double y)
    {
        var hit = _lastScene?.HitTest(new ScreenPoint(x, y), LabelHitRadius);
        _pinned = hit is not null;
        _composer.Highlighted = hit?.Place;
        SetDetail(hit);
    }

    /// <summary>Kalibrace tažením prstu do stran: popisky se posunou na skutečná místa.</summary>
    [JSInvokable]
    public void OnDrag(double dx, bool done)
    {
        if (_lastScene is not { } scene)
            return;

        if (!_dragging)
        {
            _dragStart = _correction;
            _dragging = true;
        }

        var focalLength = scene.Width / 2 / Math.Tan(scene.Projector.HorizontalFieldOfViewDegrees * Math.PI / 360);
        _correction = _dragStart.Dragged(dx, 0, focalLength);

        if (done)
        {
            _dragging = false;
            _fusion.LockTo(_lastGyroAzimuth, _lastFusedAzimuth);
        }
    }

    private void CloseDetail()
    {
        _pinned = false;
        _composer.Highlighted = null;
        _detail = null;
    }

    private void SetDetail(SceneHit? hit)
    {
        _detail = hit;
        StateHasChanged();
    }

    private string Status(CameraPose pose, double? compassAzimuth)
    {
        var heading = _fusion.IsLocked ? "kalibrováno" : compassAzimuth is null ? "bez kompasu" : "kompas ✓";
        var position = _composer.Observer is null ? "čekám na GPS" : $"GPS ±{_gpsAccuracy ?? 0:0} m";
        return string.Format(Invariant, "{0:0}° · {1} · {2}", pose.AzimuthDegrees, heading, position);
    }

    private static string DetailMeta(SceneHit hit) => hit.Place.Kind == PlaceKind.CelestialBody
        ? string.Format(Invariant, "{0:0}° nad obzorem · směr {1:0}°", hit.Los.ElevationDegrees, hit.Los.AzimuthDegrees)
        : string.Format(Invariant, "{0:0.0} km · směr {1:0}°", hit.Los.DistanceMeters / 1000, hit.Los.AzimuthDegrees);

    private static string NavigateUrl(Place place) => string.Format(Invariant,
        "https://www.google.com/maps/dir/?api=1&destination={0:0.######},{1:0.######}&travelmode=walking",
        place.Location.LatitudeDegrees, place.Location.LongitudeDegrees);

    private double Measure(string text, double size) =>
        ((IJSInProcessRuntime)JS).Invoke<double>("windowSeatMeasure", text, size);

    private static Quaternion Quaternion(double[] q) => new((float)q[0], (float)q[1], (float)q[2], (float)q[3]);

    // Dokud nemáme výškový model: kde GPS výšku nedá, odhad pro pražskou kotlinu.
    private const double FallbackAltitudeMeters = 250;

    /// <summary>
    /// O kolik je geoid (hladina moře) nad elipsoidem WGS84. Zatím hrubě: v Česku 44–47 m.
    /// TODO: model geoidu pro celý svět, až web vyjde z Česka.
    /// </summary>
    private static double ApproximateGeoidSeparation(double latitude, double longitude) =>
        InCzechia(latitude, longitude) ? 45 : 0;

    /// <summary>
    /// Magnetická deklinace. Zatím hrubě pro Česko (+5,4° v roce 2026, změřeno appkou přes model Androidu).
    /// TODO: světový magnetický model (WMM) v knihovně.
    /// </summary>
    private static double ApproximateDeclination(double latitude, double longitude) =>
        InCzechia(latitude, longitude) ? 5.4 : 0;

    private static bool InCzechia(double latitude, double longitude) =>
        latitude is > 48.5 and < 51.1 && longitude is > 12 and < 18.9;

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
        _self?.Dispose();
    }

    private sealed record StartResult(string Motion, string Camera, string Location);
}
