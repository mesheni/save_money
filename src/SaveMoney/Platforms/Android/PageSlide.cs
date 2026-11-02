using Android.Animation;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using SaveMoney.Helpers;
using NativeView = Android.Views.View;

namespace SaveMoney.Platforms.Android;

/// <summary>One outgoing page image and one live incoming page, translated as complete surfaces.</summary>
internal sealed class PageSlide : IDisposable
{
    private readonly ViewGroup _decor;
    private readonly FrameLayout _overlay;
    private readonly ImageView _image;
    private readonly Bitmap _bitmap;
    private NativeView? _incoming;
    private ValueAnimator? _animator;
    private bool _disposed;
    private float _offset;
    private readonly int _direction;
    public float Width => _bitmap.Width;

    private PageSlide(ViewGroup decor, NativeView content, int direction)
    {
        _decor = decor;
        _direction = direction;
        _bitmap = Bitmap.CreateBitmap(content.Width, content.Height, Bitmap.Config.Argb8888!)!;
        using (var canvas = new Canvas(_bitmap)) content.Draw(canvas);
        int[] location = new int[2], origin = new int[2];
        content.GetLocationOnScreen(location);
        decor.GetLocationOnScreen(origin);
        _overlay = new FrameLayout(decor.Context!) { Clickable = true };
        _overlay.SetClipChildren(true);
        _overlay.SetBackgroundColor(global::Android.Graphics.Color.ParseColor(ThemePalette.Get("Surface").ToArgbHex()));
        _image = new ImageView(decor.Context!);
        _image.SetImageBitmap(_bitmap);
        _image.SetScaleType(ImageView.ScaleType.FitXy);
        _overlay.AddView(_image, new FrameLayout.LayoutParams(content.Width, content.Height));
        decor.AddView(_overlay, new FrameLayout.LayoutParams(content.Width, content.Height)
        {
            LeftMargin = location[0] - origin[0], TopMargin = location[1] - origin[1],
        });
    }

    public static PageSlide? Capture(Page? page, int direction)
    {
        if (page is not ContentPage { Content.Handler.PlatformView: NativeView content }
            || content.Width <= 0 || content.Height <= 0) return null;
        return CaptureContent(content, direction);
    }

    public static PageSlide? CaptureContent(NativeView content, int direction = 1)
    {
        if (content.Width <= 0 || content.Height <= 0
            || Platform.CurrentActivity?.Window?.DecorView is not ViewGroup decor) return null;
        try { return new PageSlide(decor, content, direction); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); return null; }
    }

    public async Task<bool> BindAsync(Page page)
    {
        // Do not reveal the target before its handler and layout exist (first visit included).
        for (var frame = 0; frame < 90 && !_disposed; frame++)
        {
            if (page is ContentPage { Content.Handler.PlatformView: NativeView native }
                && native.IsAttachedToWindow && native.Width > 0 && native.Height > 0)
            {
                _incoming = native;
                SetOffset(_offset);
                _overlay.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
                return true;
            }
            await Task.Delay(16);
        }
        return false;
    }

    public void SetOffset(float offset)
    {
        if (_disposed) return;
        _offset = Math.Clamp(offset, _direction > 0 ? -Width : 0, _direction > 0 ? 0 : Width);
        // Keep the source intact while a first-visit handler is being created.
        // Reveal both pages in the same frame, never a gap behind the finger.
        _image.TranslationX = _incoming is null ? 0 : _offset;
        if (_incoming is not null) _incoming.TranslationX = _offset + _direction * Width;
    }

    public async Task AnimateAsync(bool commit)
    {
        if (_disposed) return;
        var end = commit ? -_direction * Width : 0;
        if (!Motion.AnimationsEnabled) { SetOffset(end); return; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var start = _offset;
        _animator = ValueAnimator.OfFloat(start, end)!;
        _animator.SetDuration((long)Math.Clamp(Motion.Normal * Math.Abs(end - start) / Width, 90, Motion.Normal));
        _animator.SetInterpolator(new PathInterpolator(.2f, 0, 0, 1));
        _animator.Update += (_, args) => SetOffset(((Java.Lang.Float)args.Animation.AnimatedValue!).FloatValue());
        _animator.AnimationEnd += (_, _) => completion.TrySetResult();
        _animator.AnimationCancel += (_, _) => completion.TrySetResult();
        _animator.Start();
        await completion.Task;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _animator?.Cancel();
            if (_incoming is { Handle: var handle } && handle != IntPtr.Zero) _incoming.TranslationX = 0;
            if (_decor.Handle != IntPtr.Zero && _overlay.Handle != IntPtr.Zero) _decor.RemoveView(_overlay);
            if (_image.Handle != IntPtr.Zero) _image.SetImageBitmap(null);
        }
        catch (ObjectDisposedException) { } // Activity/window may disappear during a drag.
        catch (Java.Lang.RuntimeException ex) { System.Diagnostics.Debug.WriteLine(ex); }
        finally
        {
            _animator?.Dispose();
            _image.Dispose();
            _overlay.Dispose();
            _bitmap.Dispose();
        }
    }
}
