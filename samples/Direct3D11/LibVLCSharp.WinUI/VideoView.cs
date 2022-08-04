using System;
using System.Runtime.InteropServices;
using LibVLCSharp.Direct3D11.TerraFX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT;
// ReSharper disable InconsistentlySynchronizedField

namespace LibVLCSharp.WinUI;

/// <summary>
/// VideoView for WinUI.
/// With MediaPlayer as dependency property 
/// </summary>
public unsafe class VideoView : SwapChainPanel, IDisposable
{
    /// <summary>
    /// Direct3D11 stuff is encapsulated here
    /// </summary>
    private IDirect3D11Resources _direct3D11Resources;

    public static readonly DependencyProperty MediaPlayerProperty = DependencyProperty.Register(
        nameof(MediaPlayer), typeof(MediaPlayer), typeof(VideoView), new PropertyMetadata(null,MediaPlayerChanged));

    private static void MediaPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MediaPlayer oldMediaPlayer)
        {
            ((VideoView)d).DetachMediaPlayer(oldMediaPlayer);
        }
        if (e.NewValue is MediaPlayer newMediaPlayer)
        {
            ((VideoView)d).AttachMediaPlayer(newMediaPlayer);
        }
        
    }

    public MediaPlayer MediaPlayer
    {
        get { return (MediaPlayer)GetValue(MediaPlayerProperty); }
        set { SetValue(MediaPlayerProperty, value); }
    }

    public VideoView()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    public void Dispose()
    {
        var t = MediaPlayer;
        if (t != null)
        {

            t.Stop();
            MediaPlayer = null; //will also detach via dependency property changed callback
            t.Dispose();
        }

        var swapChainPanelNative = this.As<ISwapChainPanelNative>();

        var hr = swapChainPanelNative.SetSwapChain(IntPtr.Zero);
        if (hr != 0)
            throw new InvalidOperationException("SetSwapChain failed with " + hr);

        var tResources = _direct3D11Resources;
        _direct3D11Resources = null;
        tResources?.Dispose();
    }

    /// <summary>
    /// initially set in OnLoaded
    /// </summary>
    private uint _width, _height;

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    private void AttachMediaPlayer(MediaPlayer mediaPlayer)
    {
        if (mediaPlayer == null)
        {
            return;
        }
        if (_width == 0 || _height == 0)
            return; // not yet loaded. OnLoaded will call again

        _direct3D11Resources ??= Direct3D11Resources.CreateCompositionSwapChain(_width, _height);
        SetSwapChainIntoPanel(_direct3D11Resources.SwapChainNativePtr);
        _direct3D11Resources.CreateResources(_width, _height);

        mediaPlayer.SetOutputCallbacks(VideoEngine.D3D11, Setup, Cleanup, SetResize, UpdateOutput, Swap, StartRendering,
            null, null, SelectPlane);
    }

    private void DetachMediaPlayer(MediaPlayer oldMediaPlayer)
    {
        //unsubscribe callbacks
        oldMediaPlayer?.SetOutputCallbacks(VideoEngine.Disable, Setup, Cleanup, SetResize, UpdateOutput, Swap,
            StartRendering,
            null, null, SelectPlane);
        
    }
    
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        lock (_sizeLockObject)
        {
            _width = (uint)this.ActualSize.X;
            _height = (uint)this.ActualSize.Y;
            _reportSize?.Invoke(_reportOpaque, _width, _height);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _width = (uint)this.ActualSize.X;
        _height = (uint)this.ActualSize.Y;
        if (MediaPlayer != null)
            AttachMediaPlayer(MediaPlayer); //Attach don't work before loading, so do again now
    }

    #region ISwapChainPanelNative COM Access. Can be migrated to TerraFX if desired

    /// <summary>
    /// COM object for SwapChain to set the DXGI swapchain into the SwapChainPanel. From DirectN project
    /// </summary>
    [ComImport, Guid("63aad0b8-7c24-40ff-85a8-640d944cc325"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISwapChainPanelNative
    {
        [PreserveSig]
        //HRESULT SetSwapChain(IDXGISwapChain swapChain);
        int SetSwapChain(IntPtr swapChain);
    }

    private void SetSwapChainIntoPanel(IntPtr swapChainForCompositionNativePtr)
    {
        var swapChainPanelNative = this.As<ISwapChainPanelNative>();

        if (swapChainForCompositionNativePtr == IntPtr.Zero)
            throw new InvalidOperationException("no _swapChain1");
        var hr = swapChainPanelNative.SetSwapChain(swapChainForCompositionNativePtr);
        if (hr != 0)
            throw new InvalidOperationException("SetSwapChain failed with " + hr);
    }

    #endregion

    #region vlc callbacks

    private bool Setup(ref IntPtr opaque, SetupDeviceConfig* config, ref SetupDeviceInfo setup)
    {
        setup.D3D11.DeviceContext = _direct3D11Resources.AcquireDeviceForVlc().ToPointer();
        return true;
    }

    private void Cleanup(IntPtr opaque)
    {
        // here we can release all things Direct3D11 for good (if playing only one file)
        _direct3D11Resources.ReleaseDeviceForVlc();
    }

    private readonly object _sizeLockObject = new object();
    private static MediaPlayer.ReportSizeChange _reportSize;
    private static IntPtr _reportOpaque;

    private void SetResize(IntPtr opaque, MediaPlayer.ReportSizeChange reportSizeChange, IntPtr reportOpaque)
    {
        lock (_sizeLockObject)
        {
            if (reportSizeChange != null && reportOpaque != IntPtr.Zero)
            {
                _reportSize = reportSizeChange;
                _reportOpaque = reportOpaque;
                _reportSize?.Invoke(_reportOpaque, _width, _height);

                /*
                var width = (uint)ActualSize.X;
                var height = (uint)ActualSize.Y;
                _reportSize?.Invoke(_reportOpaque, width, height);
                */
            }
        }
    }

    private bool UpdateOutput(IntPtr opaque, RenderConfig* config, ref OutputConfig output)
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


    private void Swap(IntPtr opaque)
    {
        _direct3D11Resources.Present();
    }

    private bool StartRendering(IntPtr opaque, bool enter)
    {
        if (enter)
        {
            _direct3D11Resources.StartRendering();
        }
        else
        {
            _direct3D11Resources.StopRendering();
        }

        return true;
    }

    private bool SelectPlane(IntPtr opaque, UIntPtr plane, void* output)
    {
        if ((ulong)plane != 0)
            return false;
        return true;
    }

    #endregion
}