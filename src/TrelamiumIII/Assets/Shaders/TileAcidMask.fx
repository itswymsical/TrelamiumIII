// Acid eating into the terrain, read off the acid residue's canvas and clipped to the terrain.
// Canvas channels: r mark coverage, g eaten amount, b film amount; weighted sums.
//
// Bottom to top: the scar it leaves, the wet film, then the pits, which bite through both and are
// rimmed by the front still eating. Bubbles sit on whatever film is left.

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

texture2D uFlowTex;                         // long streaks; the film runs along them
sampler2D FlowSampler = sampler_state
{
    Texture = <uFlowTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

texture2D uCellTex;                         // cellular noise, dark at each cell's middle; where pits and bubbles start
sampler2D CellSampler = sampler_state
{
    Texture = <uCellTex>;
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
float SurfaceLip;           // world px the film stands proud of a floor

float3 ScarColor;
float ScarAlpha;
float ScarThreshold;        // coverage where the scar begins
float ScarSoftness;         // coverage from first scar to full
float ScarBands;

float PitScale;             // pit cells per tile
float PitDistort;           // how far the streaks drag the pits out of round
float PitReach;             // cell value a fully eaten tile has opened up to
float3 PitColor;
float PitAlpha;
float EatingEdge;           // cell value past a pit's edge still being eaten
float3 EatingColor;
float EatingGlow;           // how far the eating front lights itself, 0..1

float FilmThreshold;        // eroded film amount at which the film exists
float FilmDepth;            // film amount past the threshold to a fully shaded core
float FlowScale;            // streak cycles per tile
float FlowStrength;         // how far the streaks move the film's edge
float2 FlowDrift;           // streak scroll, uv/sec; negative y runs downhill
float FilmBands;
float FilmAlpha;

float3 ShadowColor;
float3 BodyColor;
float3 CoreColor;
float3 SurfaceColor;        // the film's top row
float SurfaceAlpha;

float BubbleScale;          // bubble cells per tile
float BubbleSpeed;          // bubbles per second, per cell
float BubbleSize;           // cell value a bubble grows to before it pops
float3 BubbleColor;
float BubbleAlpha;

float DripLength;           // world px the longest drip hangs under an overhang

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

float Flow(float2 p)
{
    return tex2Dlod(FlowSampler, float4(p, 0.0, 0.0)).r * 0.65
         + tex2Dlod(FlowSampler, float4(p * 2.3 + 0.17, 0.0, 0.0)).r * 0.35;
}

float Bands(float value, float bands)
{
    return ceil(saturate(value) * bands) / max(bands, 1.0);
}

// Whether open air at this cell hangs a drip off the overhang above it. Each column of cells picks its own length.
float Drip(float2 cellWorld, float film)
{
    float column = floor(cellWorld.x / (CellSize * 2.0));
    float seed = tex2Dlod(CellSampler, float4(column * 0.131, 0.37, 0.0, 0.0)).r;
    float reach = DripLength * saturate(seed * 2.4 - 0.5) * (0.75 + 0.25 * sin(Time * 1.1 + seed * 40.0)) * saturate(film * 2.0);

    float above = 0.0;
    float2 step1 = float2(0.0, CellSize);

    if (reach >= CellSize * 0.5) above = max(above, Terrain(cellWorld - step1 * 0.75));
    if (reach >= CellSize * 1.5) above = max(above, Terrain(cellWorld - step1 * 1.75));
    if (reach >= CellSize * 2.5) above = max(above, Terrain(cellWorld - step1 * 2.75));
    if (reach >= CellSize * 3.5) above = max(above, Terrain(cellWorld - step1 * 3.75));

    return above;
}

float4 AcidPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 world = WorldAt(uv);
    float2 cellWorld = (floor(world / CellSize) + 0.5) * CellSize;
    float2 cell = CanvasUv(cellWorld);

    float4 canvas = tex2Dlod(CanvasSampler, float4(cell, 0.0, 0.0));

    if (canvas.r < 0.004)
        discard;

    float4 lightSum = tex2Dlod(LightSampler, float4(cell, 0.0, 0.0));
    float3 light = lightSum.gba / max(lightSum.r, 0.0001);
    float2 tiles = cellWorld / 16.0;

    float solid = Terrain(world);

    float flow = Flow(tiles * FlowScale + FlowDrift * Time);
    float film = canvas.b - FlowStrength * (flow - 0.5);

    if (solid < 0.5)
    {
        // Past the terrain only the film shows: a lip over a floor, and drips under an overhang.
        float onFloor = step(0.5, SurfaceLip) * Terrain(world + float2(0.0, SurfaceLip));
        float dripping = Drip(cellWorld, canvas.b);

        if (film < FilmThreshold || max(onFloor, dripping) < 0.5)
            discard;

        float3 runny = (onFloor > 0.5) ? SurfaceColor : lerp(BodyColor, CoreColor, flow);

        return float4(runny * light * SurfaceAlpha, SurfaceAlpha);
    }

    // The scar. Mottled, so it reads as eaten rather than painted on, and scaled rather than offset by the mottling so
    // bare terrain never picks one up from the noise alone.
    float cells = tex2Dlod(CellSampler, float4(tiles * PitScale + (Flow(tiles * FlowScale) - 0.5) * PitDistort, 0.0, 0.0)).r;
    float scar = Bands((canvas.r * lerp(0.7, 1.3, cells) - ScarThreshold) / max(ScarSoftness, 0.001), ScarBands);

    float alpha = scar * ScarAlpha;
    float3 color = ScarColor * light * alpha;

    // The film over it.
    if (film >= FilmThreshold && canvas.b >= 0.004)
    {
        float depth = saturate((film - FilmThreshold) / max(FilmDepth, 0.001));
        float shade = Bands(depth * lerp(0.55, 1.2, flow), FilmBands);

        float3 body = lerp(ShadowColor, BodyColor, saturate(shade * 2.0));
        body = lerp(body, CoreColor, saturate(shade * 2.0 - 1.0));

        float filmAlpha = FilmAlpha * lerp(0.7, 1.0, shade);

        // The terrain's top row is the surface of the pool.
        if (Terrain(world - float2(0.0, CellSize)) < 0.5)
        {
            body = SurfaceColor;
            filmAlpha = SurfaceAlpha;
        }

        // Bubbles swell out from the middle of a cell and pop, on a phase of their own.
        float bubbleCell = tex2Dlod(CellSampler, float4(tiles * BubbleScale, 0.0, 0.0)).r;
        float phase = tex2Dlod(CellSampler, float4(tiles * BubbleScale * 0.31 + 0.41, 0.0, 0.0)).r;
        float grown = frac(Time * BubbleSpeed + phase * 7.0);
        float ring = step(bubbleCell, BubbleSize * grown) * step(BubbleSize * grown - 0.05, bubbleCell);

        if (ring * step(0.25, shade) > 0.5)
        {
            body = BubbleColor;
            filmAlpha = BubbleAlpha;
        }

        color = body * light * filmAlpha + color * (1.0 - filmAlpha);
        alpha = filmAlpha + alpha * (1.0 - filmAlpha);
    }

    // The pits, which go through everything above. They open from the deepest cells outward, and stay world-locked so
    // a hole keeps its place as it grows.
    float opened = canvas.g * PitReach;

    if (canvas.g >= 0.02 && cells < opened)
    {
        color = PitColor * light * PitAlpha + color * (1.0 - PitAlpha);
        alpha = PitAlpha + alpha * (1.0 - PitAlpha);
    }
    else if (canvas.g >= 0.02 && canvas.b >= 0.004 && cells < opened + EatingEdge)
    {
        // Lights itself part of the way, since this is the part of the acid actually doing something.
        float3 eatingLight = lerp(light, float3(1.0, 1.0, 1.0), EatingGlow);

        return float4(EatingColor * eatingLight, 1.0);
    }

    if (alpha <= 0.004)
        discard;

    return float4(color, alpha);
}

technique TileAcidMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 AcidPass();
    }
}
