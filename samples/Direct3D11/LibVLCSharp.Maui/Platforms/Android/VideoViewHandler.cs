using Microsoft.Maui.Handlers;

namespace LibVLCSharp.Maui;

// All the code in this file is only included on Android.
public partial class VideoViewHandler : ViewHandler<IVideoView, Android.Widget.TextView>
{

    protected override Android.Widget.TextView CreatePlatformView()
    {
        throw new NotImplementedException();
    }

    static partial void MapMediaPlayer(VideoViewHandler handler, IVideoView entry)
    {
    }

}
