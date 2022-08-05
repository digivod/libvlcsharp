using Microsoft.Maui.Handlers;

namespace LibVLCSharp.Maui;

// All the code in this file is only included on Windows.
public partial class VideoViewHandler : ViewHandler<IVideoView, WinUI.VideoView>
{
       
    protected override WinUI.VideoView CreatePlatformView()
    {
        return new WinUI.VideoView();
    }


    static partial void MapMediaPlayer(VideoViewHandler handler, IVideoView entry)
    {
        //put platform independent IVideoView.MediaPlayer into platform control
        handler.PlatformView.MediaPlayer = entry.MediaPlayer;
    }

    private void PlatformDispose()
    {
        PlatformView.Dispose();
    }

    protected override void ConnectHandler(WinUI.VideoView platformView)
    {
        base.ConnectHandler(platformView);
    }

    protected override void DisconnectHandler(WinUI.VideoView platformView)
    {
        base.DisconnectHandler(platformView);
    }
    
}