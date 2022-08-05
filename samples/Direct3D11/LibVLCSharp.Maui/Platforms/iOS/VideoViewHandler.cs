using Microsoft.Maui.Handlers;
using UIKit;

namespace LibVLCSharp.Maui;

// All the code in this file is only included on iOS.
public partial class VideoViewHandler : ViewHandler<IVideoView, UIKit.UILabel>
{

    protected override UIKit.UILabel CreatePlatformView()
    {
        return new UIKit.UILabel()
        {
            Text = "Coming soon...",
        };
    }

    static partial void MapMediaPlayer(VideoViewHandler handler, IVideoView entry)
    {
        //put platform independent IVideoView.MediaPlayer into platform control
        //handler.PlatformView.MediaPlayer = entry.MediaPlayer;
    }


}