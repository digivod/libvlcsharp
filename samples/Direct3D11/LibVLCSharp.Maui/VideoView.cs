namespace LibVLCSharp.Maui;

public class VideoView : View, IVideoView, IDisposable
{
    public MediaPlayer MediaPlayer { get; set; }


    public static readonly BindableProperty MediaPlayerProperty = BindableProperty.Create(nameof(MediaPlayer),
        typeof(MediaPlayer), typeof(VideoView), default(MediaPlayer), propertyChanged: OnMediaPlayerPropertyChanged);

    private static void OnMediaPlayerPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
    }

    private bool _isDisposed;
    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;
        PlatformDispose();
    }

    private void PlatformDispose()
    {
    }
}