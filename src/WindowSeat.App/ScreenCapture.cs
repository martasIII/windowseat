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
/// Náhled kamery je samostatná plocha mimo běžné vykreslování, proto kopírujeme obsah okna systémem (PixelCopy)
/// stejně jako systémový screenshot; obyčejné vykreslení appky do obrázku by místo kamery dalo černo.
/// </summary>
public static class ScreenCapture
{
    /// <summary>Uloží snímek a vrátí jeho název, nebo <c>null</c>, když se nepovedl.</summary>
    public static async Task<string?> SaveAsync()
    {
#if ANDROID
        if (Platform.CurrentActivity?.Window is not { } window || window.DecorView is not { Width: > 0 } view)
            return null;

        using var bitmap = Bitmap.CreateBitmap(view.Width, view.Height, Bitmap.Config.Argb8888!)!;
        var copied = new TaskCompletionSource<bool>();
        PixelCopy.Request(window, bitmap, new CopyListener(copied), new Handler(Looper.MainLooper!));
        if (!await copied.Task)
            return null;

        var name = $"WindowSeat_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        var values = new ContentValues();
        values.Put(MediaStore.IMediaColumns.DisplayName, name);
        values.Put(MediaStore.IMediaColumns.MimeType, "image/jpeg");
        values.Put(MediaStore.IMediaColumns.RelativePath, "Pictures/WindowSeat");

        var resolver = Platform.CurrentActivity!.ContentResolver!;
        var uri = resolver.Insert(MediaStore.Images.Media.ExternalContentUri!, values);
        if (uri is null)
            return null;

        await using (var stream = resolver.OpenOutputStream(uri))
        {
            if (stream is null)
                return null;
            await bitmap.CompressAsync(Bitmap.CompressFormat.Jpeg!, 92, stream);
        }

        return name;
#else
        await Task.CompletedTask;
        return null;
#endif
    }

#if ANDROID
    private sealed class CopyListener(TaskCompletionSource<bool> done) : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
    {
        public void OnPixelCopyFinished(int copyResult) => done.TrySetResult(copyResult == (int)PixelCopyResult.Success);
    }
#endif
}
