namespace LibVLCSharp.Maui.Sample
{
    public partial class App : Application
    {
        public static readonly LibVLC Libvlc;

        static App()
        {
            Libvlc = new LibVLC(enableDebugLogs: true);
            Libvlc.Log += (s, e) => System.Diagnostics.Debug.WriteLine(e.FormattedLog);
        }


        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();
        }
    }
}