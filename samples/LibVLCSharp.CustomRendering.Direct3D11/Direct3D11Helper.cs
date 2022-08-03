using System.Runtime.InteropServices;
using TerraFX.Interop;

namespace LibVLCSharp.CustomRendering.Direct3D11
{
    public static unsafe class Direct3D11Helper
    {
        public static void ThrowIfFailed(HRESULT hr)
        {
            if (Windows.FAILED(hr))
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
    }
}
