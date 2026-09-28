// Frost read off a tile condition's canvas: translucent ice with crystal veins, a bright freezing
// front at its edge, rime standing proud of the terrain, and sparkle.
// Canvas channels: r coverage, g crystal growth (divide by weight).

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

texture2D uCellTex;                         // soft cellular noise; shapes the frost edge
sampler2D CellSampler = sampler_state
{
    Texture = <uCellTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

texture2D uVeinTex;                         // bright crack lines on black; the crystal veins
sampler2D VeinSampler = sampler_state
{
    Texture = <uVeinTex>;
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
float SurfaceLip;           // world px of rime above the terrain

float Threshold;            // eroded coverage at which frost exists
float CellScale;            // edge noise cycles per tile
float NoiseStrength;        // how far the cells move the edge
float DepthRange;           // coverage past Threshold to the thickest ice
float EdgeWidth;            // coverage past Threshold that still reads as the freezing front

float VeinScale;            // vein texture cycles per tile
float VeinCut;              // vein texture value below which there is no vein
float VeinAlpha;

float3 ShadowColor;         // thin ice
float3 BodyColor;           // thick ice
float3 VeinColor;
float3 EdgeColor;           // the freezing front
float3 RimeColor;
float BodyAlpha;
float BodyAlphaFloor;       // alpha of the thinnest ice, as a fraction of BodyAlpha
float EdgeAlpha;
float RimeAlpha;

float SparkleDensity;       // fraction of grains that sparkle
float SparkleSpeed;         // twinkle rate, radians/sec
float3 SparkleColor;

float3 AmbientLight;        // floor under the light recovered from the canvas

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

// Hash without sine: stays uniform at world-sized coordinates, where sin() loses precision.
float Hash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);

    return frac((p3.x + p3.y) * p3.z);
}

float4 FrostPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 world = WorldAt(uv);

    float solid = Terrain(world);
    float beside = max(max(Terrain(world + float2(0.0, SurfaceLip)), Terrain(world - float2(0.0, SurfaceLip))),
                       max(Terrain(world + float2(SurfaceLip, 0.0)), Terrain(world - float2(SurfaceLip, 0.0))));
    float lip = (1.0 - solid) * beside * step(0.5, SurfaceLip);

    if (solid + lip < 0.5)
        discard;

    float2 cellWorld = (floor(world / CellSize) + 0.5) * CellSize;
    float2 cell = CanvasUv(cellWorld);
    float2 tiles = cellWorld / 16.0;

    float4 canvas = tex2Dlod(CanvasSampler, float4(cell, 0.0, 0.0));
    float4 lightSum = tex2Dlod(LightSampler, float4(cell, 0.0, 0.0));
    float weight = max(lightSum.r, 0.0001);
    float3 light = max(lightSum.gba / weight, AmbientLight);
    float growth = saturate(canvas.g / weight);

    // Still cells rather than drifting noise: ice does not boil.
    float cells = tex2Dlod(CellSampler, float4(tiles * CellScale, 0.0, 0.0)).r;
    float d = canvas.r - NoiseStrength * (cells - 0.5);

    if (d < Threshold)
        discard;

    float depth = saturate((d - Threshold) / max(DepthRange, 0.001));
    float front = 1.0 - saturate((d - Threshold) / max(EdgeWidth, 0.001));

    float veins = tex2Dlod(VeinSampler, float4(tiles * VeinScale, 0.0, 0.0)).r;
    veins = saturate((veins - VeinCut) / max(1.0 - VeinCut, 0.001)) * growth;

    float3 color = lerp(ShadowColor, BodyColor, depth);
    float alpha = BodyAlpha * lerp(BodyAlphaFloor, 1.0, depth);

    color = lerp(color, VeinColor, veins);
    alpha = max(alpha, veins * VeinAlpha);

    color *= light;

    // The front and the rime are ice at its thinnest, catching light, so they hold up in shadow.
    float3 halfLit = lerp(light, float3(1.0, 1.0, 1.0), 0.5);

    color = lerp(color, EdgeColor * halfLit, front);
    alpha = max(alpha, front * EdgeAlpha);

    color = lerp(color, RimeColor * halfLit, lip);
    alpha = lerp(alpha, RimeAlpha, lip);

    float grain = Hash(floor(world / CellSize));
    float twinkle = step(1.0 - SparkleDensity, grain) * pow(saturate(sin(Time * SparkleSpeed + grain * 40.0)), 16.0);

    return float4(color * alpha + SparkleColor * twinkle * alpha, alpha);
}

technique TileFrostMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 FrostPass();
    }
}
