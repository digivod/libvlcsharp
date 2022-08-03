using System;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static TerraFX.Interop.Windows;
using static TerraFX.Interop.DXGI_FORMAT;
using static TerraFX.Interop.D3D11_INPUT_CLASSIFICATION;
using static TerraFX.Interop.D3D11_USAGE;
using static TerraFX.Interop.D3D11_BIND_FLAG;
using static TerraFX.Interop.D3D11_CPU_ACCESS_FLAG;
using static TerraFX.Interop.D3D11_MAP;
using static TerraFX.Interop.D3D_PRIMITIVE_TOPOLOGY;
using static TerraFX.Interop.D3D11_FILTER;
using static TerraFX.Interop.D3D11_TEXTURE_ADDRESS_MODE;
using static TerraFX.Interop.D3D11_COMPARISON_FUNC;
using TerraFX.Interop;

namespace LibVLCSharp.CustomRendering.Direct3D11
{
    public unsafe class Direct3D11NV12ColorConversionShader: IDisposable
    {
        
        private  ID3D11DeviceContext* _d3dctx;
        private  ID3D11Device* _d3dDevice;

        public Direct3D11NV12ColorConversionShader(ID3D11DeviceContext* d3dctx, ID3D11Device* d3dDevice)
        {
            _d3dctx = d3dctx;
            _d3dDevice = d3dDevice;
            
            CreateShader();
        }

        public void Dispose()
        {
            //nothing yet.
            _d3dctx = null;
            _d3dDevice = null;
        }

        
        [StructLayout(LayoutKind.Sequential)]
        internal struct Position
        {
            internal float x;
            internal float y;
            internal float z;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Texture
        {
            internal float x;
            internal float y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ShaderInput
        {
            internal Position position;
            internal Texture texture;
        }

        private readonly float BORDER_LEFT = -0.95f;
        private readonly float BORDER_RIGHT = 0.85f;
        private readonly float BORDER_TOP = 0.95f;
        private readonly float BORDER_BOTTOM = -0.90f;

        /* our vertex/pixel shader */
        private ID3D11VertexShader* pVS;
        private ID3D11PixelShader* pPS;
        private ID3D11InputLayout* pShadersInputLayout;

        private ID3D11Buffer* pVertexBuffer;
        private  int vertexBufferStride;

        public  uint quadIndexCount;
        private ID3D11Buffer* pIndexBuffer;

        private ID3D11SamplerState* samplerState;

        private void CreateShader()
        {
            Guid iid;
            ID3DBlob* VS, PS, pErrBlob;

            using ComPtr<ID3DBlob> vertexShaderBlob = null;

            fixed (byte* shader = Encoding.ASCII.GetBytes(DefaultShaders.HLSL))
            fixed (byte* vshader = Encoding.ASCII.GetBytes("VShader"))
            fixed (byte* vs4 = Encoding.ASCII.GetBytes("vs_4_0"))
            fixed (byte* pshader = Encoding.ASCII.GetBytes("PShader"))
            fixed (byte* ps4 = Encoding.ASCII.GetBytes("ps_4_0"))
            {
                var result = D3DCompile(shader, (nuint)DefaultShaders.HLSL.Length, null, null, null, (sbyte*)vshader,
                    (sbyte*)vs4, 0, 0, &VS, &pErrBlob);
                if (FAILED(result) && pErrBlob != null)
                {
                    var errorMessage =
                        Encoding.ASCII.GetString((byte*)pErrBlob->GetBufferPointer(), (int)pErrBlob->GetBufferSize());
                    Debug.WriteLine(errorMessage);
                    Direct3D11Helper.ThrowIfFailed(result);
                }

                result = D3DCompile(shader, (nuint)DefaultShaders.HLSL.Length, null, null, null, (sbyte*)pshader, (sbyte*)ps4,
                    0, 0, &PS, &pErrBlob);
                if (FAILED(result) && pErrBlob != null)
                {
                    var errorMessage =
                        Encoding.ASCII.GetString((byte*)pErrBlob->GetBufferPointer(), (int)pErrBlob->GetBufferSize());
                    Debug.WriteLine(errorMessage);
                    Direct3D11Helper.ThrowIfFailed(result);
                }
            }

            fixed (ID3D11VertexShader** vertexShader = &pVS)
            fixed (ID3D11PixelShader** pixelShader = &pPS)
            {
                Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateVertexShader(VS->GetBufferPointer(), VS->GetBufferSize(), null,
                    vertexShader));
                Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreatePixelShader(PS->GetBufferPointer(), PS->GetBufferSize(), null,
                    pixelShader));
            }

            fixed (byte* position = Encoding.ASCII.GetBytes("POSITION"))
            fixed (byte* textcoord = Encoding.ASCII.GetBytes("TEXCOORD"))
            fixed (ID3D11InputLayout** shadersInputLayout = &pShadersInputLayout)
            {
                var inputElementDescs = stackalloc D3D11_INPUT_ELEMENT_DESC[2];
                {
                    inputElementDescs[0] = new D3D11_INPUT_ELEMENT_DESC
                    {
                        SemanticName = (sbyte*)position,
                        SemanticIndex = 0,
                        Format = DXGI_FORMAT_R32G32B32_FLOAT,
                        InputSlot = 0,
                        AlignedByteOffset = D3D11_APPEND_ALIGNED_ELEMENT,
                        InputSlotClass = D3D11_INPUT_PER_VERTEX_DATA,
                        InstanceDataStepRate = 0
                    };

                    inputElementDescs[1] = new D3D11_INPUT_ELEMENT_DESC
                    {
                        SemanticName = (sbyte*)textcoord,
                        SemanticIndex = 0,
                        Format = DXGI_FORMAT_R32G32_FLOAT,
                        InputSlot = 0,
                        AlignedByteOffset = D3D11_APPEND_ALIGNED_ELEMENT,
                        InputSlotClass = D3D11_INPUT_PER_VERTEX_DATA,
                        InstanceDataStepRate = 0
                    };
                }

                Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateInputLayout(inputElementDescs, 2, VS->GetBufferPointer(),
                    VS->GetBufferSize(), shadersInputLayout));
            }

            var ourVerticles = new ShaderInput[4];

            ourVerticles[0] = new ShaderInput
            {
                position = new Position
                {
                    x = BORDER_LEFT,
                    y = BORDER_BOTTOM,
                    z = 0.0f
                },
                texture = new Texture { x = 0.0f, y = 1.0f }
            };

            ourVerticles[1] = new ShaderInput
            {
                position = new Position
                {
                    x = BORDER_RIGHT,
                    y = BORDER_BOTTOM,
                    z = 0.0f
                },
                texture = new Texture { x = 1.0f, y = 1.0f }
            };

            ourVerticles[2] = new ShaderInput
            {
                position = new Position
                {
                    x = BORDER_RIGHT,
                    y = BORDER_TOP,
                    z = 0.0f
                },
                texture = new Texture { x = 1.0f, y = 0.0f }
            };

            ourVerticles[3] = new ShaderInput
            {
                position = new Position
                {
                    x = BORDER_LEFT,
                    y = BORDER_TOP,
                    z = 0.0f
                },
                texture = new Texture { x = 0.0f, y = 0.0f }
            };

            var verticlesSize = (uint)sizeof(ShaderInput) * 4;

            var bd = new D3D11_BUFFER_DESC
            {
                Usage = D3D11_USAGE_DYNAMIC,
                ByteWidth = verticlesSize,
                BindFlags = (uint)D3D11_BIND_VERTEX_BUFFER,
                CPUAccessFlags = (uint)D3D11_CPU_ACCESS_WRITE
            };

            pVertexBuffer = CreateBuffer(bd);
            vertexBufferStride = Marshal.SizeOf(ourVerticles[0]);

            D3D11_MAPPED_SUBRESOURCE ms;

            ID3D11Resource* res;
            iid = IID_ID3D11Resource;

            Direct3D11Helper.ThrowIfFailed(pVertexBuffer->QueryInterface(&iid, (void**)&res));

            Direct3D11Helper.ThrowIfFailed(_d3dctx->Map(res, 0, D3D11_MAP_WRITE_DISCARD, 0, &ms));
            for (var i = 0; i < ourVerticles.Length; i++)
            {
                Marshal.StructureToPtr(ourVerticles[i], (IntPtr)ms.pData + (i * vertexBufferStride), false);
            }

            //Buffer.MemoryCopy(ms.pData, ourVerticles, verticlesSize, verticlesSize);
            _d3dctx->Unmap(res, 0);

            quadIndexCount = 6;

            var bufferDesc = new D3D11_BUFFER_DESC
            {
                Usage = D3D11_USAGE_DYNAMIC,
                ByteWidth = sizeof(ushort) * quadIndexCount,
                BindFlags = (uint)D3D11_BIND_INDEX_BUFFER,
                CPUAccessFlags = (uint)D3D11_CPU_ACCESS_WRITE
            };

            pIndexBuffer = CreateBuffer(bufferDesc);

            Direct3D11Helper.ThrowIfFailed(pIndexBuffer->QueryInterface(&iid, (void**)&res));

            Direct3D11Helper.ThrowIfFailed(_d3dctx->Map(res, 0, D3D11_MAP_WRITE_DISCARD, 0, &ms));
            Marshal.WriteInt16((IntPtr)ms.pData, 0 * sizeof(ushort), 3);
            Marshal.WriteInt16((IntPtr)ms.pData, 1 * sizeof(ushort), 1);
            Marshal.WriteInt16((IntPtr)ms.pData, 2 * sizeof(ushort), 0);
            Marshal.WriteInt16((IntPtr)ms.pData, 3 * sizeof(ushort), 2);
            Marshal.WriteInt16((IntPtr)ms.pData, 4 * sizeof(ushort), 1);
            Marshal.WriteInt16((IntPtr)ms.pData, 5 * sizeof(ushort), 3);

            _d3dctx->Unmap(res, 0);

            _d3dctx->IASetPrimitiveTopology(D3D10_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            _d3dctx->IASetInputLayout(pShadersInputLayout);
            uint offset = 0;

            var vv = (uint)vertexBufferStride;
            fixed (ID3D11Buffer** buffer = &pVertexBuffer)
                _d3dctx->IASetVertexBuffers(0, 1, buffer, &vv, &offset);

            _d3dctx->IASetIndexBuffer(pIndexBuffer, DXGI_FORMAT_R16_UINT, 0);

            _d3dctx->VSSetShader(pVS, null, 0);
            _d3dctx->PSSetShader(pPS, null, 0);

            var samplerDesc = new D3D11_SAMPLER_DESC
            {
                Filter = D3D11_FILTER_MIN_MAG_LINEAR_MIP_POINT,
                AddressU = D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressV = D3D11_TEXTURE_ADDRESS_CLAMP,
                AddressW = D3D11_TEXTURE_ADDRESS_CLAMP,
                ComparisonFunc = D3D11_COMPARISON_ALWAYS,
                MinLOD = 0,
                MaxLOD = D3D11_FLOAT32_MAX
            };

            fixed (ID3D11SamplerState** ss = &samplerState)
            {
                Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateSamplerState(&samplerDesc, ss));
                _d3dctx->PSSetSamplers(0, 1, ss);
            }
        }
        
        private ID3D11Buffer* CreateBuffer(D3D11_BUFFER_DESC bd)
        {
            ID3D11Buffer* buffer;

            Direct3D11Helper.ThrowIfFailed(_d3dDevice->CreateBuffer(&bd, null, &buffer));

            return buffer;
        }

        public void DrawIndexed()
        {
            _d3dctx->DrawIndexed(quadIndexCount, 0, 0);
        }
    }
}
