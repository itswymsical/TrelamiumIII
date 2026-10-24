// Animated pattern over the liquid in a bottle item sprite, one technique per look (names match LiquidSurface.fx).
//
// s0 is the bottle's liquid mask; overlays read only its alpha. uv * TextureSize is the position in sprite pixels.
//
//   TextureSize  mask size in pixels
//   PixelSize    sprite pixels per output cell; 2 matches the art grid
//   Scale        pattern size multiplier (1 = the world surface's size)
//   Speed, Strength, ColorA/B  as in LiquidSurface.fx
// Vertex colour multiplies the result.
//
// Techniques that draw the whole liquid (BottledLiquidItem.SurfaceIsLiquid) also get:
//   Shadow, Body, Highlight  LiquidPalette stops at shade 0, 0.4 and 1
//   ShadeRange   darkest mask shade and (lightest - darkest), so mask shades map to 0-1 as in the baked sprite
//   LiquidBounds opaque mask rectangle in pixels: left, top, right, bottom (exclusive)
//   uGlassTex    the bottle's glass, same size as the mask; clear cells just above the mask are headroom to slosh into

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

texture2D uGlassTex;
sampler2D GlassSampler = sampler_state
{
    Texture = <uGlassTex>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
};

float Time;
float2 TextureSize;
float PixelSize;
float Scale;
float Speed;
float Strength;
float3 ColorA;
float3 ColorB;

float3 Shadow;
float3 Body;
float3 Highlight;
float2 ShadeRange;
float4 LiquidBounds;

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

// LiquidPalette.At.
float3 PaletteAt(float shade)
{
    return lerp(lerp(Shadow, Body, saturate(shade / 0.4)), Highlight, saturate((shade - 0.4) / 0.6));
}

// Fill-line depth below the liquid's top, in cells, at side (-1 left wall, 1 right) and t. The line tilts back and forth
// with a travelling ripple; a slow noise swell sets how hard. Returns (depth, rate of change).
float2 SloshLevel(float side, float t)
{
    float swell = smoothstep(0.38, 0.62, tex2D(NoiseSampler, float2(t * 0.031, 0.37)).r);
    float amplitude = lerp(0.45, 1.6, swell) * Strength;

    float phase = t * 2.6;
    float ripple = side * 2.4 - t * 4.7;
    float level = amplitude * (sin(phase) * side + 0.3 * sin(ripple));
    float rate = amplitude * (2.6 * cos(phase) * side - 1.41 * cos(ripple));

    return float2(level, rate);
}

// The whole liquid, sloshing: the line rises into the headroom on one side and drops on the other, carrying the mask's
// shading with it, and throws drops into the air while it moves fast. Air cells stay clear for the glass drawn over them.
float4 SplashBottlePass(float4 vertex : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 cell = SpritePixel(uv) + PixelSize * 0.5;
    float4 mask = tex2D(MaskSampler, cell / TextureSize);
    float depth = (cell.y - LiquidBounds.y) / PixelSize;

    bool headroom = mask.a < 0.5 && depth > -1 && cell.x > LiquidBounds.x && cell.x < LiquidBounds.z
        && tex2D(GlassSampler, cell / TextureSize).a < 0.5;
    if (mask.a < 0.5 && !headroom)
        return 0;

    float halfWidth = (LiquidBounds.z - LiquidBounds.x) * 0.5;
    float side = (cell.x - LiquidBounds.x - halfWidth) / halfWidth;

    float t = Time * Speed;
    float2 slosh = SloshLevel(side, t);
    float below = depth - slosh.x;

    if (below < 0)
    {
        float chance = 0.5 * saturate(abs(slosh.y) * 0.4) * saturate(1.4 + below);
        float drop = step(1 - chance, Hash(floor(cell / PixelSize) + float2(floor(t * 9), 0)));

        return float4(Highlight, 1) * vertex * drop;
    }

    // Shading follows the line: read it from where this cell sat before the line moved by whole cells.
    float lift = clamp(ceil(slosh.x - 0.5), -1, 4);
    float4 source = tex2D(MaskSampler, (cell - float2(0, lift * PixelSize)) / TextureSize);
    if (source.a < 0.5)
        source = mask;

    float shade = source.a < 0.5 ? 1 : (source.r / source.a - ShadeRange.x) / ShadeRange.y;

    if (below < 1)
    {
        shade = max(shade, 0.78);
    }
    else
    {
        float churn = tex2D(NoiseSampler, cell / (Scale * 48) + float2(t * 0.05 - slosh.x * 0.04, -t * 0.03)).r;
        shade = saturate(shade + (churn - 0.5) * 0.5);
    }

    return float4(PaletteAt(round(shade * 5) / 5), 1) * vertex;
}

technique Splash
{
    pass SplashBottle
    {
        PixelShader = compile ps_3_0 SplashBottlePass();
    }
}
