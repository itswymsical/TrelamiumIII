// Fire and char read off the burn condition's canvas, clipped to the terrain.
// Canvas channels: r mark coverage, g heat (divide by weight), b char amount, a fire amount.

sampler2D CanvasSampler : register(s0);     // weighted sums, see above

texture2D uCanvasLight;                     // r kernel weight, gba world light; weighted sums
sampler2D LightSampler = sampler_state
{
    Texture = <uCanvasLight>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

texture2D uMaskTex;                         // solid tile layer; alpha is terrain
sampler2D MaskSampler = sampler_state
{
    Texture = <uMaskTex>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

float Time;
float2 CanvasScreenSize;    // screen px the canvas covers
matrix ScreenToWorld;       // screen px -> world px, snapped to the drawn tile grid
matrix WorldToScreen;
float2 MaskOrigin;          // world px of the mask's top-left texel
float2 MaskTexel;           // 1 / mask size
float CellSize;             // world px per shading decision; 2 is one tile art pixel

float FireThreshold;        // eroded fire amount at which flame shows
float FireNoiseScale;       // FBM cycles per tile
float FireNoiseStrength;
float2 FireDrift;           // FBM scroll, uv/sec; positive y licks upward
float FireEdgeWidth;        // fire amount past FireThreshold from rim to body
float FireBodyAlpha;
float FireEdgeAlpha;

// Blackbody ramp, coolest to hottest. The fourth is the flash at the centre of a raging tile.
float3 EmberColor;
float3 CoolColor;
float3 HotColor;
float3 FlashColor;

float3 CharColor;
float CharAlpha;            // soot opacity at full char
float CharThreshold;        // char amount where soot begins
float CharSoftness;         // char amount from first soot to full
float CharNoiseScale;       // mottling cycles per tile
float CharNoiseStrength;
float CharBands;            // soot opacity steps

float2 WorldAt(float2 uv)
{
    return mul(float4(uv * CanvasScreenSize, 0.0, 1.0), ScreenToWorld).xy;
}

float2 CanvasUv(float2 world)
{
    return mul(float4(world, 0.0, 1.0), WorldToScreen).xy / CanvasScreenSize;
}

float Terrain(float2 world)
{
    return step(0.5, tex2Dlod(MaskSampler, float4((world - MaskOrigin) * MaskTexel, 0.0, 0.0)).a);
}

float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    for (int i = 0; i < 4; i++)
    {
        sum += tex2Dlod(NoiseSampler, float4(p, 0.0, 0.0)).r * amp;
        p = p * 2.02 + 0.13;
        amp *= 0.5;
    }

    return sum;
}

float3 HeatRamp(float heat)
{
    float3 low = lerp(EmberColor, CoolColor, saturate(heat / 0.45));
    float3 mid = lerp(CoolColor, HotColor, saturate((heat - 0.45) / 0.37));
    float3 high = lerp(HotColor, FlashColor, saturate((heat - 0.82) / 0.18));

    float3 color = (heat < 0.45) ? low : mid;

    return (heat < 0.82) ? color : high;
}

float4 BurnPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 world = WorldAt(uv);

    if (Terrain(world) < 0.5)
        discard;

    float2 cellWorld = (floor(world / CellSize) + 0.5) * CellSize;
    float2 cell = CanvasUv(cellWorld);
    float2 tiles = cellWorld / 16.0;

    float4 canvas = tex2Dlod(CanvasSampler, float4(cell, 0.0, 0.0));
    float4 lightSum = tex2Dlod(LightSampler, float4(cell, 0.0, 0.0));
    float weight = lightSum.r;
    float3 light = lightSum.gba / max(weight, 0.0001);
    float heat = saturate(canvas.g / max(weight, 0.0001));

    // Still and world-locked, so the scorch keeps its shape while the fire moves over it. The mottling scales the
    // char rather than offsetting it, so bare terrain never picks up soot from the noise alone.
    float mottle = Fbm(tiles * CharNoiseScale);
    float charred = canvas.b * lerp(1.0 - CharNoiseStrength, 1.0 + CharNoiseStrength, mottle);
    float soot = saturate((charred - CharThreshold) / max(CharSoftness, 0.001));
    soot = ceil(soot * CharBands) / max(CharBands, 1.0);
    float sootAlpha = soot * CharAlpha;

    float flicker = Fbm(tiles * FireNoiseScale + FireDrift * Time);
    float f = canvas.a - FireNoiseStrength * (flicker - 0.5);
    float depth = saturate((f - FireThreshold) / max(FireEdgeWidth, 0.001));

    // Hotter at the rim, where the front is eating into fresh fuel; the flicker keeps the body moving.
    float3 fireColor = HeatRamp(saturate(heat * lerp(0.8, 1.15, flicker) + (1.0 - depth) * 0.35));
    float fireAlpha = step(FireThreshold, f) * step(0.004, canvas.a) * lerp(FireEdgeAlpha, FireBodyAlpha, depth);

    float alpha = fireAlpha + sootAlpha * (1.0 - fireAlpha);

    if (alpha <= 0.004)
        discard;

    // The fire lights itself; the soot is lit by the world like the tile under it.
    return float4(fireColor * fireAlpha + CharColor * light * sootAlpha * (1.0 - fireAlpha), alpha);
}

technique TileBurnMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 BurnPass();
    }
}
