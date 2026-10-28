using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace WindowSeat.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		HideSystemBars();
	}

	public override void OnWindowFocusChanged(bool hasFocus)
	{
		base.OnWindowFocusChanged(hasFocus);
		if (hasFocus)
			HideSystemBars();
	}

	/// <summary>
	/// Náhled přes celý displej: bez stavového řádku nahoře a navigační lišty dole, i pod výřezem kamery.
	/// Lišty se ukážou tahem od okraje a samy zase zmizí.
	/// </summary>
	private void HideSystemBars()
	{
		if (Window is not { } window)
			return;

		WindowCompat.SetDecorFitsSystemWindows(window, false);
		if (OperatingSystem.IsAndroidVersionAtLeast(28) && window.Attributes is { } attributes)
		{
			attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
			window.Attributes = attributes;
		}

		if (WindowCompat.GetInsetsController(window, window.DecorView) is not { } controller)
			return;

		controller.Hide(WindowInsetsCompat.Type.SystemBars());
		controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
	}
}
