using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using LibVLCSharp.Direct3D11.TerraFX;
using static TerraFX.Interop.Windows;
using TerraFX.Interop;
using static LibVLCSharp.MediaPlayer;

namespace LibVLCSharp.CustomRendering.Direct3D11
{
    unsafe class Program
    {
        static Form form;

        const int WIDTH = 1500;
        const int HEIGHT = 900;



        static RTL_CRITICAL_SECTION sizeLock = new RTL_CRITICAL_SECTION();

        private static IDirect3D11Resources _direct3D11Resources;


        
        static LibVLC libvlc;
        static MediaPlayer mediaplayer;

        static ReportSizeChange reportSize;
        static IntPtr reportOpaque;

        static uint width, height;

        static void Main()
        {
            CreateWindow();

            InitializeDirect3D();

            InitializeLibVLC();

            Application.Run();
        }

        static void CreateWindow()
        {
            form = new Form() { Width = WIDTH, Height = HEIGHT, Text = typeof(Program).Namespace };
            form.Show();
            form.Resize += Form_Resize;
            form.FormClosing += Form_FormClosing;
        }

        static void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            mediaplayer?.Stop();
            
            //unsubscribe callbacks
            mediaplayer?.SetOutputCallbacks(VideoEngine.Disable, OutputSetup, OutputCleanup, OutputSetResize, UpdateOutput, Swap, StartRendering, null, null, SelectPlane);
            
            mediaplayer?.Dispose();
            mediaplayer = null;

            _direct3D11Resources.Dispose();
            libvlc?.Dispose();
            libvlc = null;

            Environment.Exit(0);
        }

        static void Form_Resize(object sender, EventArgs e)
        {
            width = Convert.ToUInt32(form.ClientRectangle.Width);
            height = Convert.ToUInt32(form.ClientRectangle.Height);

            fixed (RTL_CRITICAL_SECTION* sl = &sizeLock)
            {
                EnterCriticalSection(sl);

                reportSize?.Invoke(reportOpaque, width, height);

                LeaveCriticalSection(sl);
            }

            reportSize?.Invoke(reportOpaque, width, height);
        }

        static void InitializeDirect3D()
        {
            _direct3D11Resources = Direct3D11Resources.CreateHwndSwapChain(WIDTH, HEIGHT, form.Handle);


            fixed (RTL_CRITICAL_SECTION* sl = &sizeLock)
                InitializeCriticalSection(sl);
        }


        static void InitializeLibVLC()
        {
            libvlc = new LibVLC(enableDebugLogs: true);
            libvlc.Log += (s, e) => Debug.WriteLine(e.FormattedLog);

            mediaplayer = new MediaPlayer(libvlc);

            using var media = new Media(new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4"));
            
            mediaplayer.Media = media;

            mediaplayer.SetOutputCallbacks(VideoEngine.D3D11, OutputSetup, OutputCleanup, OutputSetResize, UpdateOutput, Swap, StartRendering, null, null, SelectPlane);

            mediaplayer.Play();
        }



        // libvlc

        static bool OutputSetup(ref IntPtr opaque, SetupDeviceConfig* config, ref SetupDeviceInfo setup)
        {
            setup.D3D11.DeviceContext = _direct3D11Resources.AcquireDeviceForVlc().ToPointer();
            return true;
        }

        static void OutputCleanup(IntPtr opaque)
        {
            // here we can release all things Direct3D11 for good (if playing only one file)
            _direct3D11Resources.ReleaseDeviceForVlc();
        }

        static void OutputSetResize(IntPtr opaque, ReportSizeChange report_size_change, IntPtr report_opaque)
        {
            fixed (RTL_CRITICAL_SECTION* sl = &sizeLock)
            { 
                EnterCriticalSection(sl);

                if (report_size_change != null && report_opaque != IntPtr.Zero)
                {
                    reportSize = report_size_change;
                    reportOpaque = report_opaque;
                    reportSize?.Invoke(reportOpaque, width, height);
                }

                LeaveCriticalSection(sl);
            }
        }

        static bool UpdateOutput(IntPtr opaque, RenderConfig* config, ref OutputConfig output)
        {
            _direct3D11Resources.CreateResources(config->Width, config->Height);

            output.Union.DxgiFormat = (int)_direct3D11Resources.DxgiRenderFormat;
            output.FullRange = true;
            output.ColorSpace = ColorSpace.BT709;
            output.ColorPrimaries = ColorPrimaries.BT709;
            output.TransferFunction = TransferFunction.SRGB;
            output.Orientation = VideoOrientation.TopLeft;

            return true;
        }


        static void Swap(IntPtr opaque)
        {
            _direct3D11Resources.Present();
        }

        static bool StartRendering(IntPtr opaque, bool enter)
        {
            if(enter)
            {
                _direct3D11Resources.StartRendering();
            }
            else
            {
                _direct3D11Resources.StopRendering();
            }
            return true;
        }

        static unsafe bool SelectPlane(IntPtr opaque, UIntPtr plane, void* output)
        {
            if ((ulong)plane != 0)
                return false;
            return true;
        }
    }
}