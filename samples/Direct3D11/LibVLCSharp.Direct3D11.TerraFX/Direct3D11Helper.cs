using System.Runtime.InteropServices;
using TerraFX.Interop;

namespace LibVLCSharp.Direct3D11.TerraFX
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
