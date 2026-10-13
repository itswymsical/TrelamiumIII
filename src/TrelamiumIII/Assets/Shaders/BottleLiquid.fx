// Animated pattern over the liquid in a bottle item sprite, one technique per look (names match LiquidSurface.fx).
//
// s0 is the bottle's liquid mask; only its alpha is read. uv * TextureSize is the position in sprite pixels.
//
//   TextureSize  mask size in pixels
//   PixelSize    sprite pixels per output cell; 2 matches the art grid
//   Scale        pattern size multiplier (1 = the world surface's size)
//   Speed, Strength, ColorA/B  as in LiquidSurface.fx
// Vertex colour multiplies the result.

#include "LiquidPatterns.fxh"

sampler2D MaskSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float Time;
float2 TextureSize;
float PixelSize;
float Scale;
float Speed;
float Strength;
float3 ColorA;
float3 ColorB;

float2 SpritePixel(float2 uv)
{
    return floor(uv * TextureSize / PixelSize) * PixelSize;
}

float4 Output(float3 rgb, float alpha, float4 vertex)
{
    alpha = saturate(alpha * Strength * vertex.a);
    return float4(rgb * vertex.rgb * alpha, alpha);
}

float4 BrineBottlePass(float4 vertex : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float strand = BrineStrands(NoiseSampler, SpritePixel(uv), Time, Speed, Scale);

    return Output(lerp(ColorB, ColorA, strand), strand * tex2D(MaskSampler, uv).a, vertex);
}

technique Brine
{
    pass BrineBottle
    {
        PixelShader = compile ps_3_0 BrineBottlePass();
    }
}
