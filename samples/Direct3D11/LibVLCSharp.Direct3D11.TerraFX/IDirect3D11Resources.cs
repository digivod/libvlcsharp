
namespace LibVLCSharp.Direct3D11.TerraFX;

/// <summary>
/// Direct3D11 support.
/// Current implementation is TerraFX based, but pure C(++) might be a cleaner option.
/// Interface is independent from TerraFX 
/// </summary>
public interface IDirect3D11Resources : IDisposable
{
    /// <summary>
    /// DXGI_FORMAT Internal render format used. Typically DXGI_FORMAT_B8G8R8A8_UNORM
    /// </summary>
    uint DxgiRenderFormat { get; }
    
    /// <summary>
    /// true if the swapchain has been created with CreateSwapChainForComposition as needed by WinUI
    /// (false is the classic hwnd based swap chain)
    /// </summary>
    bool IsCompositeSwapChainForWinUI { get; }

    /// <summary>
    /// Native pointer to the swap chain.
    /// Needed by WinUI to call ISwapChainPanelNative.SetSwapChain on the WinUI SwapChain control.
    /// If the caller must ensure it has no references to the swap chain on calling Dispose on
    /// this class!  Meaning:
    /// call ISwapChainPanelNative.SetSwapChain(IntPtr.Zero) before IDirect3D11Resources.Dispose()
    /// </summary>
    IntPtr SwapChainNativePtr { get; }
    
    /// <summary>
    /// creates/updates the resources for the given dimensions
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    void CreateResources(uint width, uint height);
    
    /// <summary>
    /// returns the device we provide to libVLC
    /// </summary>
    /// <returns></returns>
    IntPtr AcquireDeviceForVlc();
    
    /// <summary>
    /// releases the devices we previously provided to libVLC via AcquireDeviceForVlc( 
    /// </summary>
    void ReleaseDeviceForVlc();
    
    /// <summary>
    /// Presents the swapchain (on Swap callback)
    /// </summary>
    void Present();
    
    /// <summary>
    /// Notification that libVLC starts the rendering: StartRendering(enter)
    /// </summary>
    void StartRendering();
    /// <summary>
    /// Notification that libVLC completed the rendering: StartRendering(!enter)
    /// </summary>
    void StopRendering();
}
