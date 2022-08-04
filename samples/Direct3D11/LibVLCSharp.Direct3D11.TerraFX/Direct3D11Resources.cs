using System.Diagnostics;
using System.Numerics;
using TerraFX.Interop;
using static TerraFX.Interop.Windows;
using static TerraFX.Interop.D3D_DRIVER_TYPE;
using static TerraFX.Interop.DXGI_FORMAT;
using static TerraFX.Interop.D3D11_USAGE;
using static TerraFX.Interop.D3D11_BIND_FLAG;
using static TerraFX.Interop.D3D11_RESOURCE_MISC_FLAG;
using static TerraFX.Interop.D3D_SRV_DIMENSION;
using static TerraFX.Interop.D3D11_RTV_DIMENSION;

namespace LibVLCSharp.Direct3D11.TerraFX
{
    public unsafe class Direct3D11Resources : IDirect3D11Resources
    {
        private bool _isDisposed;
        private const DXGI_FORMAT RenderFormat = DXGI_FORMAT_B8G8R8A8_UNORM;

        public uint DxgiRenderFormat => (uint)RenderFormat;

        private ComPtr<IDXGISwapChain> _swapchain;


        public bool IsCompositeSwapChainForWinUI { get; }

        public IntPtr SwapChainNativePtr =>new IntPtr(_swapchain.Get());

        private ComPtr<ID3D11RenderTargetView> _swapchainRenderTargetView;

        private ID3D11Device* _d3dDevice;
        private ID3D11DeviceContext* _d3dctx;
        private ID3D11Device* _d3deviceVlc;
        private ID3D11DeviceContext* _d3dctxVlc;


        private ID3D11Texture2D* _textureVLC;
        private ID3D11RenderTargetView* _textureRenderTarget;
        private HANDLE _sharedHandle;
        private ID3D11Texture2D* _texture;
        private ID3D11ShaderResourceView* _textureShaderInput;


        private Direct3D11NV12ColorConversionShader _shader;

        public static IDirect3D11Resources CreateCompositionSwapChain(uint width, uint height)
            => new Direct3D11Resources(width, height);

        public static IDirect3D11Resources CreateHwndSwapChain(uint width, uint height, IntPtr hwnd)
            => new Direct3D11Resources(width, height, hwnd);

        /// <summary>
        /// hwnd based ctor
        /// </summary>
        private Direct3D11Resources(uint width, uint height, IntPtr outputWindowHwnd)
        {
            IsCompositeSwapChainForWinUI = false;
            var desc = new DXGI_SWAP_CHAIN_DESC
            {
                BufferDesc = new DXGI_MODE_DESC
                {
                    Width = width,
                    Height = height,
                    Format = RenderFormat,
                },
                SampleDesc = new DXGI_SAMPLE_DESC
                {
                    Count = 1
                },
                BufferCount = 1,
                Windowed = TRUE,
                OutputWindow = outputWindowHwnd, // form.Handle,
                BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                Flags = (uint)DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH
            };

            uint creationFlags = 0;
#if DEBUG
            creationFlags |= (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG;
#endif

            fixed (IDXGISwapChain** swapchain = _swapchain)
            fixed (ID3D11Device** device = &_d3dDevice)
            fixed (ID3D11DeviceContext** context = &_d3dctx)
            {
                Direct3D11Helper.ThrowIfFailed(D3D11CreateDeviceAndSwapChain(null,
                    D3D_DRIVER_TYPE_HARDWARE,
                    IntPtr.Zero,
                    creationFlags,
                    null,
                    0,
                    D3D11_SDK_VERSION,
                    &desc,
                    swapchain,
                    device,
                    null,
                    context));
            }

            MakeDeviceMultiThreaded(_d3dDevice);

            CreateVlcDevice(creationFlags);

            SetViewport(width, height);
            //CreateRenderTargetView();

            _shader = new Direct3D11NV12ColorConversionShader(_d3dctx, _d3dDevice);
        }

        /// <summary>
        /// composition swapchain based ctor
        /// </summary>
        private Direct3D11Resources(uint width, uint height)
        {
            IsCompositeSwapChainForWinUI = true;
            var desc = new DXGI_SWAP_CHAIN_DESC1
            {
                Width = width,
                Height = height,
                Format = RenderFormat,
                Stereo = 0,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0, },
                BufferCount = 2,
                BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                SwapEffect =
                    DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL, // FlipSequential is mandatory for composition
                Flags = (uint)0, // DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_ALLOW_MODE_SWITCH
                //Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH, // DXGI_SCALING.DXGI_SCALING_NONE,
                AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE,
            };

            uint creationFlags = 0;
#if DEBUG
            creationFlags |= (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG;
#endif
            fixed (ID3D11Device** device = &_d3dDevice)
            fixed (ID3D11DeviceContext** deviceContext = &_d3dctx)
            {
                Direct3D11Helper.ThrowIfFailed(
                    D3D11CreateDevice(null, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, creationFlags, null, 0,
                        D3D11_SDK_VERSION, device, null, deviceContext));

                using ComPtr<IDXGIFactory2> factory = new();
                Direct3D11Helper.ThrowIfFailed(CreateDXGIFactory1(__uuidof<IDXGIFactory2>(),
                    (void**)factory.GetAddressOf()));
                using ComPtr<IDXGISwapChain1> swapChain1 = new();
                Direct3D11Helper.ThrowIfFailed(factory.Get()->CreateSwapChainForComposition((IUnknown*)(*device),
                    &desc, null, swapChain1.GetAddressOf()));
                Direct3D11Helper.ThrowIfFailed(swapChain1.As<IDXGISwapChain>(ref this._swapchain));
            }

            MakeDeviceMultiThreaded(_d3dDevice);

            CreateVlcDevice(creationFlags);

            SetViewport(width, height);
            CreateRenderTargetView();


            _shader = new Direct3D11NV12ColorConversionShader(_d3dctx, _d3dDevice);

            //Direct3D11Helper.ThrowIfFailed(winUiPanel.Get()->SetSwapChain(_swapchain1.Get()));
        }

        #region ctor methods
        private void CreateVlcDevice(uint creationFlags)
        {
            fixed (ID3D11Device** device = &_d3deviceVlc)
            fixed (ID3D11DeviceContext** context = &_d3dctxVlc)
            {
                Direct3D11Helper.ThrowIfFailed(D3D11CreateDevice(null,
                    D3D_DRIVER_TYPE_HARDWARE,
                    IntPtr.Zero,
                    creationFlags |
                    (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_VIDEO_SUPPORT, /* needed for hardware decoding */
                    null, 0,
                    D3D11_SDK_VERSION,
                    device, null, context));
            }
        }
        
        private static void MakeDeviceMultiThreaded(ID3D11Device* device)
        {
            ID3D10Multithread* pMultithread;
            var iid = IID_ID3D10Multithread;

            Direct3D11Helper.ThrowIfFailed(device->QueryInterface(&iid, (void**)&pMultithread));
            pMultithread->SetMultithreadProtected(TRUE);
            pMultithread->Release();
        }
        #endregion


        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            ReleaseTextures();

            _shader.Dispose();
            
            //swapchain
            {
                var count1 = _swapchain.Reset(); //used to get count
                Debug.Assert(count1 == 0); 
                _swapchain.Dispose(); //already Reset, so does nothing 
            }

            if (_d3dctx != null)
            {
                var count2 = _d3dctx->Release();
                Debug.Assert(count2 == 0);
                _d3dctx = null;
            }

            if (_d3dDevice != null)
            {
                var count3 = _d3dDevice->Release();
                Debug.Assert(count3 == 0);
                _d3dDevice = null;
            }

            if (_d3dctxVlc != null)
            {
                var count = _d3dctxVlc->Release();
                Debug.Assert(count == 0);
                _d3dctxVlc = null;
            }
            
            if (_d3deviceVlc != null)
            {
                var count = _d3deviceVlc->Release();
                Debug.Assert(count == 0);
                _d3deviceVlc = null;
            }
        }


        private void SetViewport(uint width, uint height)
        {
            var viewport = new D3D11_VIEWPORT
            {
                Height = height,
                Width = width
            };

            _d3dctx->RSSetViewports(1, &viewport);
        }

        private void CreateRenderTargetView()
        {
            using ComPtr<ID3D11Resource> pBackBuffer = null;

            var iidId3D11Texture2D = IID_ID3D11Texture2D;
            Direct3D11Helper.ThrowIfFailed(_swapchain.Get()->GetBuffer(0, &iidId3D11Texture2D,
                (void**)pBackBuffer.GetAddressOf()));

            fixed (ID3D11RenderTargetView** swapchainRenderTarget = _swapchainRenderTargetView)
                Direct3D11Helper.ThrowIfFailed(
                    _d3dDevice->CreateRenderTargetView(pBackBuffer.Get(), null, swapchainRenderTarget));

            //pBackBuffer.Dispose(); // using above

            fixed (ID3D11RenderTargetView** swapchainRenderTarget = _swapchainRenderTargetView)
                _d3dctx->OMSetRenderTargets(1, swapchainRenderTarget, null);
        }

        private void ReleaseTextures()
        {
            var left = _swapchainRenderTargetView.Reset();
            Debug.Assert(left == 0);

            if (_sharedHandle != null)
            {
                CloseHandle(_sharedHandle);
                _sharedHandle = null;
            }

            if (_textureVLC != null)
            {
                var count = _textureVLC->Release();
                Debug.Assert(count == 0);
                _textureVLC = null;
            }

            if (_textureShaderInput != null)
            {
                var count = _textureShaderInput->Release();
                Debug.Assert(count == 0);
                _textureShaderInput = null;
            }

            if (_textureRenderTarget != null)
            {
                var count = _textureRenderTarget->Release();
                Debug.Assert(count == 0);
                _textureRenderTarget = null;
            }

            if (_texture != null)
            {
                var count = _texture->Release();
                Debug.Assert(count == 0);
                _texture = null;
            }
        }


        public void CreateResources(uint width, uint height)
        {
            ReleaseTextures();
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(Direct3D11Resources));

            if (IsCompositeSwapChainForWinUI)
            {
                //for Resize To work, all references to back buffers etc. must be released first
                //see https://docs.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-resizebuffers
                Direct3D11Helper.ThrowIfFailed(
                    _swapchain.Get()->ResizeBuffers(0 /* keep existing count */, width, height,
                        DXGI_FORMAT_UNKNOWN /* keep existing */, 0));
            }
            SetViewport(width, height);
            CreateRenderTargetView();

            var texDesc = new D3D11_TEXTURE2D_DESC
            {
                MipLevels = 1,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
                BindFlags = (uint)(D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE),
                Usage = D3D11_USAGE_DEFAULT,
                CPUAccessFlags = 0,
                ArraySize = 1,
                Format = RenderFormat,
                Width = width, // config->Width,
                Height = height, // config->Height,
                MiscFlags = (uint)(D3D11_RESOURCE_MISC_SHARED | D3D11_RESOURCE_MISC_SHARED_NTHANDLE)
            };

            fixed (ID3D11Texture2D** texture =
                       &_texture) Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateTexture2D(&texDesc, null, texture));

            IDXGIResource1* sharedResource = null;
            var iid = IID_IDXGIResource1;

            _texture->QueryInterface(&iid, (void**)&sharedResource);

            fixed (void* handle = &_sharedHandle)
                Direct3D11Helper.ThrowIfFailed(sharedResource->CreateSharedHandle(null,
                    DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE, null, (IntPtr*)handle));
            sharedResource->Release();

            ID3D11Device1* d3d11VLC1;
            iid = IID_ID3D11Device1;
            _d3deviceVlc->QueryInterface(&iid, (void**)&d3d11VLC1);

            iid = IID_ID3D11Texture2D;
            fixed (ID3D11Texture2D** texture = &_textureVLC)
                Direct3D11Helper.ThrowIfFailed(d3d11VLC1->OpenSharedResource1(_sharedHandle, &iid, (void**)texture));
            d3d11VLC1->Release();

            var shaderResourceViewDesc = new D3D11_SHADER_RESOURCE_VIEW_DESC
            {
                ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D,
                Format = texDesc.Format
            };

            shaderResourceViewDesc.Texture2D.MipLevels = 1;

            ID3D11Resource* res;
            iid = IID_ID3D11Resource;
            _texture->QueryInterface(&iid, (void**)&res);
            fixed (ID3D11ShaderResourceView** tsi = &_textureShaderInput)
            {
                Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateShaderResourceView(res, &shaderResourceViewDesc, tsi));
                res->Release();
                _d3dctx->PSSetShaderResources(0, 1, tsi);
            }

            var renderTargetViewDesc = new D3D11_RENDER_TARGET_VIEW_DESC
            {
                Format = texDesc.Format,
                ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D
            };

            iid = IID_ID3D11Resource;
            _textureVLC->QueryInterface(&iid, (void**)&res);

            fixed (ID3D11RenderTargetView** trt = &_textureRenderTarget)
            {
                Direct3D11Helper.ThrowIfFailed(_d3deviceVlc->CreateRenderTargetView(res, &renderTargetViewDesc, trt));
                res->Release();
                _d3dctxVlc->OMSetRenderTargets(1, trt, null);
            }
        }

        public IntPtr AcquireDeviceForVlc()
        {
            //Cleanup callback is not really called at the moment, so no ReleaseDeviceForVlc takes place.
            //therefore no AddRef until that is sorted out
            //_d3dctxVlc->AddRef();
            return new IntPtr(_d3dctxVlc);
        }

        public void ReleaseDeviceForVlc()
        {
            //_d3dctxVlc->Release(); //see AcquireDeviceForVlc comment
        }

        public void Present()
        {
            _swapchain.Get()->Present(0, 0);
        }

        public void StartRendering()
        {
            // DEBUG: draw greenish background to show where libvlc doesn't draw in the texture
            // Normally you should Clear with a black background
            var greenRGBA = new Vector4(0.5f, 0.5f, 0.0f, 1.0f);
            //var blackRGBA = new Vector4(0, 0, 0, 1);


            _d3dctxVlc->ClearRenderTargetView(_textureRenderTarget, (float*)&greenRGBA);

            //must be done per call for composition swap chains:
            fixed (ID3D11RenderTargetView** swapchainRenderTarget = _swapchainRenderTargetView)
                _d3dctx->OMSetRenderTargets(1, swapchainRenderTarget, null);
        }

        public void StopRendering()
        {
            var orangeRGBA = new Vector4(1.0f, 0.5f, 0.0f, 1.0f);
            _d3dctx->ClearRenderTargetView(_swapchainRenderTargetView, (float*)&orangeRGBA);
            // Render into the swapchain
            // We start the drawing of the shared texture in our app as early as possible
            // in hope it's done as soon as Swap_cb is called
            _shader.DrawIndexed();
        }

    }
}