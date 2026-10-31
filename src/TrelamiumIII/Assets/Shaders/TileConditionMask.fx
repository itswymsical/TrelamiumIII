// A coat on the terrain read off a tile condition's canvas: slime, chemical residues.
// Frame inputs come from TileCanvas, the look from TileFilmLook. Shading decisions are made per
// CellSize world pixels, on the same grid as the tile art, so the coat moves with the terrain.

sampler2D CanvasSampler : register(s0);     // r coverage, gba tile colour; weighted sums

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
float SurfaceLip;           // world px the coat may stand proud of the terrain into open air

float Threshold;            // eroded coverage at which the coat exists
float NoiseScale;           // FBM cycles per tile
float NoiseStrength;        // how far the FBM moves the edge
float2 NoiseDrift;          // FBM scroll, uv/sec; positive y moves the pattern up
float DepthRange;           // coverage past Threshold to a fully shaded core
float Bands;                // shading steps across that depth

float3 ShadowColor;
float3 BodyColor;
float3 CoreColor;
float BodyAlpha;
float BodyAlphaFloor;       // alpha of the thinnest band, as a fraction of BodyAlpha

float3 OutlineColor;        // the coat's rim, and its lip beside walls and ceilings
float OutlineAlpha;
float3 HighlightColor;      // the top of the coat; zero alpha turns it off
float HighlightAlpha;

float EmissiveEdge;         // how far rim and highlight ignore world light, 0..1
float3 AmbientLight;        // floor under the light recovered from the canvas

float TintStrength;         // how far each tile's colour re-dyes the palette; 0 draws it as authored
float TintWash;             // how much of their own colour the rim and highlight keep under a tint

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

float2 CellCentre(float2 world)
{
    return (floor(world / CellSize) + 0.5) * CellSize;
}

float Coverage(float2 world)
{
    return tex2Dlod(CanvasSampler, float4(CanvasUv(world), 0.0, 0.0)).r;
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

float4 FilmPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 world = WorldAt(uv);

    float solid = Terrain(world);
    float beside = max(max(Terrain(world + float2(0.0, SurfaceLip)), Terrain(world - float2(0.0, SurfaceLip))),
                       max(Terrain(world + float2(SurfaceLip, 0.0)), Terrain(world - float2(SurfaceLip, 0.0))));
    float lip = (1.0 - solid) * beside * step(0.5, SurfaceLip);

    if (solid + lip < 0.5)
        discard;

    float2 cellWorld = CellCentre(world);
    float2 cell = CanvasUv(cellWorld);

    float4 canvas = tex2Dlod(CanvasSampler, float4(cell, 0.0, 0.0));
    float coverage = canvas.r;
    float4 lightSum = tex2Dlod(LightSampler, float4(cell, 0.0, 0.0));
    float3 light = max(lightSum.gba / max(lightSum.r, 0.0001), AmbientLight);

    // The local tile colour, divided back out of the coverage it was weighted by, as a ratio to the
    // body colour. An untinted tile carries BodyColor, so its dye is one and it draws as authored.
    float3 tint = canvas.gba / max(coverage, 0.0001);
    float3 dye = lerp(float3(1.0, 1.0, 1.0), clamp(tint / max(BodyColor, 0.04), 0.0, 8.0), TintStrength);
    float3 edgeDye = lerp(dye, float3(1.0, 1.0, 1.0), TintWash);

    float fbm = Fbm(cellWorld / 16.0 * NoiseScale + NoiseDrift * Time);
    float erosion = NoiseStrength * (fbm - 0.5);
    float d = coverage - erosion;

    if (d < Threshold)
        discard;

    float3 edgeLight = lerp(light, float3(1.0, 1.0, 1.0), EmissiveEdge);

    // The top of the coat: its lip over a floor, or with no lip, the terrain's own top row.
    float overFloor = lip * Terrain(world + float2(0.0, SurfaceLip));
    float topRow = solid * (1.0 - Terrain(world - float2(0.0, CellSize))) * (1.0 - step(0.5, SurfaceLip));

    if (HighlightAlpha > 0.0 && max(overFloor, topRow) > 0.5)
        return float4(saturate(HighlightColor * edgeDye) * edgeLight * HighlightAlpha, HighlightAlpha);

    float2 dx = float2(CellSize, 0.0);
    float2 dy = float2(0.0, CellSize);
    float rim = min(min(Coverage(cellWorld + dx), Coverage(cellWorld - dx)), min(Coverage(cellWorld + dy), Coverage(cellWorld - dy))) - erosion;

    if (rim < Threshold || lip > 0.5)
        return float4(saturate(OutlineColor * edgeDye) * edgeLight * OutlineAlpha, OutlineAlpha);

    float t = 1.0 - exp(-(d - Threshold) / max(DepthRange, 0.001));
    float shade = saturate(t * (0.55 + 0.9 * fbm));
    shade = saturate(floor(shade * Bands) / max(Bands - 1.0, 1.0));

    float3 color = lerp(ShadowColor, BodyColor, saturate(shade * 2.0));
    color = saturate(lerp(color, CoreColor, saturate(shade * 2.0 - 1.0)) * dye);

    float alpha = BodyAlpha * lerp(BodyAlphaFloor, 1.0, shade);

    return float4(color * light * alpha, alpha);
}

technique TileConditionMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 FilmPass();
    }
}
