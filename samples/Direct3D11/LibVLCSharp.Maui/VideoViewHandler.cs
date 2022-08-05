using Microsoft.Maui.Handlers;

namespace LibVLCSharp.Maui
{
    /// <summary>
    /// Platform independent part of VideoViewHandler.
    /// Individual platform partial extensions add the " : ViewHandler&lt;IVideoView, TPlatformControl&gt;"
    /// </summary>
    public partial class VideoViewHandler
    {
        public static PropertyMapper<IVideoView, VideoViewHandler> CustomEntryMapper =
            new PropertyMapper<IVideoView, VideoViewHandler>(ViewHandler.ViewMapper)
            {
                [nameof(IVideoView.MediaPlayer)] = MapMediaPlayer,
            };

        //special syntax to mark this as static method implemented in platform dependent partial extensions
        static partial void MapMediaPlayer(VideoViewHandler handler, IVideoView entry);


        //ctor missing
        public VideoViewHandler() : this(null, null)
        {
        }

        public VideoViewHandler(IPropertyMapper? mapper, CommandMapper? commandMapper = null) : base(
            mapper ?? CustomEntryMapper, commandMapper)
        {
        }
    }
}