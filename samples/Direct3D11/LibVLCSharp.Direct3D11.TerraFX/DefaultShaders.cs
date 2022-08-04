﻿namespace LibVLCSharp.Direct3D11.TerraFX;

/// <summary>
/// Default shader strings for custom rendering sample code
/// </summary>
public static class DefaultShaders
{
    /// <summary>
    /// Default HLSL shader string used in the official D3D11 custom rendering sample
    /// </summary>
    public const string HLSL = @"
Texture2D shaderTexture;
SamplerState samplerState;
struct PS_INPUT
{
    float4 position     : SV_POSITION;
    float4 textureCoord : TEXCOORD0;
};

float4 PShader(PS_INPUT In) : SV_TARGET
{
    return shaderTexture.Sample(samplerState, In.textureCoord);
}

struct VS_INPUT
{
    float4 position     : POSITION;
    float4 textureCoord : TEXCOORD0;
};

struct VS_OUTPUT
{
    float4 position     : SV_POSITION;
    float4 textureCoord : TEXCOORD0;
};

VS_OUTPUT VShader(VS_INPUT In)
{
    return In;
}
";
}