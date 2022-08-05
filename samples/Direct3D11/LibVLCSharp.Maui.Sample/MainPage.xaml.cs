namespace LibVLCSharp.Maui.Sample
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {

            InitializeComponent();
            //there is no this.Closed because we are no window but a ContentPage. We'll need to find another way to dispose
            this.Unloaded += (sender, args) =>
            {
                VideoView.MediaPlayer.Stop();
                this.VideoView.Dispose();
                this.Content = null;
            };
            VideoView.MediaPlayer = new MediaPlayer(App.Libvlc);
            VideoView.Loaded += StartPlayback;
        }

        private void StartPlayback(object sender, EventArgs e)
        {

            using var media =
                new Media(
                    new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4"));

            VideoView.MediaPlayer.Media = media;
            VideoView.MediaPlayer.Play();
        }

    }
}