using System;
using System.Diagnostics;
using System.Numerics;

using static TerraFX.Interop.Windows;
using static TerraFX.Interop.D3D_DRIVER_TYPE;
using static TerraFX.Interop.DXGI_FORMAT;
using static TerraFX.Interop.D3D11_USAGE;
using static TerraFX.Interop.D3D11_BIND_FLAG;
using static TerraFX.Interop.D3D11_RESOURCE_MISC_FLAG;
using static TerraFX.Interop.D3D_SRV_DIMENSION;
using static TerraFX.Interop.D3D11_RTV_DIMENSION;
using TerraFX.Interop;

namespace LibVLCSharp.CustomRendering.Direct3D11
{
    public unsafe class Direct3D11Resources: IDisposable
    {

        private bool _isDisposed;
        public readonly DXGI_FORMAT RenderFormat = DXGI_FORMAT_R8G8B8A8_UNORM;
        private IDXGISwapChain* _swapchain;
         ID3D11RenderTargetView* _swapchainRenderTarget;

         private ID3D11Device* _d3dDevice;
         private ID3D11DeviceContext* _d3dctx;
         private ID3D11Device* _d3deviceVlc;
         private ID3D11DeviceContext* _d3dctxVlc;

         
         private ID3D11Texture2D* _textureVLC;
         private ID3D11RenderTargetView* _textureRenderTarget;
         private HANDLE _sharedHandle;
         private ID3D11Texture2D* _texture;
         private ID3D11ShaderResourceView* _textureShaderInput;


         private Direct3D11NV12ColorConversionShader shader;

         public Direct3D11Resources(uint width, uint height, IntPtr outputWindowHwnd)
         {
            var desc = new DXGI_SWAP_CHAIN_DESC
            {
                BufferDesc = new DXGI_MODE_DESC
                {
                    Width = width,
                    Height = height,
                    Format = DXGI_FORMAT_R8G8B8A8_UNORM,
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

            fixed (IDXGISwapChain** swapchain = &_swapchain)
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

            ID3D10Multithread* pMultithread;
            var iid = IID_ID3D10Multithread;

            Direct3D11Helper.ThrowIfFailed(_d3dDevice->QueryInterface(&iid, (void**)&pMultithread));
            pMultithread->SetMultithreadProtected(TRUE);
            pMultithread->Release();

            var viewport = new D3D11_VIEWPORT 
            {
                Height = height,
                Width = width
            };

            _d3dctx->RSSetViewports(1, &viewport);

            fixed (ID3D11Device** device = &_d3deviceVlc)
            fixed (ID3D11DeviceContext** context = &_d3dctxVlc)
            {
                Direct3D11Helper.ThrowIfFailed(D3D11CreateDevice(null,
                      D3D_DRIVER_TYPE_HARDWARE,
                      IntPtr.Zero,
                      creationFlags | (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_VIDEO_SUPPORT, /* needed for hardware decoding */
                      null, 0,
                      D3D11_SDK_VERSION,
                      device, null, context));
            }

            using ComPtr<ID3D11Resource> pBackBuffer = null;

            iid = IID_ID3D11Texture2D;
            Direct3D11Helper.ThrowIfFailed(_swapchain->GetBuffer(0, &iid, (void**)pBackBuffer.GetAddressOf()));

            fixed (ID3D11RenderTargetView** swapchainRenderTarget = &_swapchainRenderTarget) Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateRenderTargetView(pBackBuffer.Get(), null, swapchainRenderTarget));

            pBackBuffer.Dispose();

            fixed (ID3D11RenderTargetView** swapchainRenderTarget = &_swapchainRenderTarget)
                _d3dctx->OMSetRenderTargets(1, swapchainRenderTarget, null);


            shader = new Direct3D11NV12ColorConversionShader(_d3dctx, _d3dDevice); 
            
        }

        public void ReleaseTextures()
        {
            if (_sharedHandle != null)
            {
                CloseHandle(_sharedHandle);
                _sharedHandle = null;
            }
            if(_textureVLC != null)
            {
                var count = _textureVLC->Release();
                Debug.Assert(count == 0);
                _textureVLC = null;
            }
            if(_textureShaderInput != null)
            {
                var count = _textureShaderInput->Release();
                Debug.Assert(count == 0);
                _textureShaderInput = null;
            }
            if(_textureRenderTarget != null)
            {
                var count = _textureRenderTarget->Release();
                Debug.Assert(count == 0);
                _textureRenderTarget = null;
            }
            if(_texture != null)
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

            fixed (ID3D11Texture2D** texture = &_texture) Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateTexture2D(&texDesc, null, texture));

            IDXGIResource1* sharedResource = null;
            var iid = IID_IDXGIResource1;

            _texture->QueryInterface(&iid, (void**)&sharedResource);

            fixed (void* handle = &_sharedHandle) Direct3D11Helper.ThrowIfFailed(sharedResource->CreateSharedHandle(null, DXGI_SHARED_RESOURCE_READ | DXGI_SHARED_RESOURCE_WRITE, null, (IntPtr*)handle));
            sharedResource->Release();

            ID3D11Device1* d3d11VLC1;
            iid = IID_ID3D11Device1;
            _d3deviceVlc->QueryInterface(&iid, (void**)&d3d11VLC1);

            iid = IID_ID3D11Texture2D;
            fixed (ID3D11Texture2D** texture = &_textureVLC) Direct3D11Helper.ThrowIfFailed(d3d11VLC1->OpenSharedResource1(_sharedHandle, &iid, (void**)texture));
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

            fixed(ID3D11RenderTargetView** trt = &_textureRenderTarget)
            {
                Direct3D11Helper.ThrowIfFailed(_d3deviceVlc->CreateRenderTargetView(res, &renderTargetViewDesc, trt));
                res->Release();
                _d3dctxVlc->OMSetRenderTargets(1, trt, null);
            }
            
        }

        public IntPtr AcquireDeviceForVlc()
        {
            _d3dctxVlc->AddRef();
            return new IntPtr(_d3dctxVlc);
        }

        public void ReleaseDeviceForVlc()
        {
            _d3dctxVlc->Release();
            
        }
        
        public void Present()
        {
            _swapchain->Present(0, 0);
        }

        public void StartRendering()
        {
            // DEBUG: draw greenish background to show where libvlc doesn't draw in the texture
            // Normally you should Clear with a black background
            var greenRGBA = new Vector4(0.5f, 0.5f, 0.0f, 1.0f);
            //var blackRGBA = new Vector4(0, 0, 0, 1);

            _d3dctxVlc->ClearRenderTargetView(_textureRenderTarget, (float*)&greenRGBA);
        }

        public void StopRendering()
        {
            var orangeRGBA = new Vector4(1.0f, 0.5f, 0.0f, 1.0f);
            _d3dctx->ClearRenderTargetView(_swapchainRenderTarget, (float*)&orangeRGBA);
            // Render into the swapchain
            // We start the drawing of the shared texture in our app as early as possible
            // in hope it's done as soon as Swap_cb is called
            shader.DrawIndexed();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            shader?.Dispose();
            shader = null;
            ReleaseTextures();
            //TODO: dispose Device and other stuff from ctor
        }
    }
}