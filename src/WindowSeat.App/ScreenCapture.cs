#if ANDROID
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Views;
#endif

namespace WindowSeat.App;

/// <summary>
/// Snímek obrazovky přímo z appky: záběr kamery i s popisky, uložený do galerie (Pictures/WindowSeat).
/// Náhled kamery je samostatná plocha mimo okno appky; kopie okna (PixelCopy) má na jeho místě jen průhlednou
/// „díru“. Proto skládáme dvě vrstvy: dospod aktuální snímek z náhledu kamery, navrch kopii okna s popisky.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Uloží snímek a vrátí jeho název; když se nepovede, vyhodí výjimku s důvodem (appka ho ukáže).</summary>
    /// <param name="cameraHandler">Ovladač náhledu kamery; z něj se vezme aktuální obraz.</param>
    public static async Task<string> SaveAsync(IViewHandler? cameraHandler)
    {
#if ANDROID
        if (Platform.CurrentActivity?.Window is not { } window || window.DecorView is not { Width: > 0 } view)
            throw new InvalidOperationException("okno appky není k dispozici");

        using var overlay = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888!)!;
        var copied = new TaskCompletionSource<bool>();
        PixelCopy.Request(window, overlay, new CopyListener(copied), new Handler(Looper.MainLooper!));
        if (!await copied.Task)
            throw new InvalidOperationException("kopie obrazovky se nepovedla");

        using var bitmap = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888!)!;
        using (var canvas = new Canvas(bitmap))
        {
            canvas.DrawColor(Android.Graphics.Color.Black);
            if (cameraHandler?.PlatformView is not AndroidX.Camera.View.PreviewView preview)
                throw new InvalidOperationException("náhled kamery nenalezen");
            if (preview.Bitmap is not { } frame)
                throw new InvalidOperationException("kamera nedala obraz");

            {
                var location = new int[2];
                preview.GetLocationInWindow(location);
                canvas.DrawBitmap(frame, location[0], location[1], null);
                frame.Recycle();
            }

            canvas.DrawBitmap(overlay, 0, 0, null);
        }

        var name = $"WindowSeat_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, name);
        values.Put(MediaStore.IMediaColumns.MimeType, "image/jpeg");
        values.Put(MediaStore.IMediaColumns.RelativePath, "Pictures/WindowSeat");

        var resolver = Platform.CurrentActivity!.ContentResolver!;
        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri!, values);
        if (uri is null)
            throw new InvalidOperationException("galerie odmítla uložení");

        await using (var stream = resolver.OpenOutputStream(uri))
        {
            if (stream is null)
                throw new InvalidOperationException("do galerie nejde zapisovat");
            await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg!, 92, stream);
        }

        return name;
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("snímek zatím jen na Androidu");
#endif
    }

#if ANDROID
    private sealed class CopyListener(TaskCompletionSource<bool> done) : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
    {
        public void OnPixelCopyFinished(int copyResult) => done.TrySetResult(copyResult == (int)PixelCopyResult.Success);
    }
#endif
}
