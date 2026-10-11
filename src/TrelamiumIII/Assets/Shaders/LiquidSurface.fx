// Animated surface drawn over a liquid's visible area, one technique per look.
//
// Each sprite's source rectangle is its world rectangle over a wrapping noise texture, so uv * NoiseSize is
// the world position and every pattern stays locked to the world as the camera moves.
//
//   NoiseSize   pixels across the s0 noise texture
//   PixelSize   world pixels per output cell; 2 matches the art grid
//   Scale       pattern size multiplier (1 = authored size)
//   Speed       animation speed multiplier
//   Strength    overall opacity
//   Tint        the liquid condition's colour, for looks that follow it
//   ColorA/B    look-specific palette
// Vertex colour carries the tile light in rgb and the liquid's edge opacity in alpha.

sampler2D NoiseSampler : register(s0);

texture2D uCellTex;
sampler2D CellSampler = sampler_state
{
    Texture = <uCellTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float Time;
float NoiseSize;
float PixelSize;
float Scale;
float Speed;
float Strength;
float3 Tint;
float3 ColorA;
float3 ColorB;

float2 World(float2 uv)
{
    return floor(uv * NoiseSize / PixelSize) * PixelSize;
}

float N(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float4 Output(float3 rgb, float alpha, float4 vertex)
{
    alpha = saturate(alpha * Strength * vertex.a);
    return float4(rgb * vertex.rgb * alpha, alpha);
}

// Thin salt strands: iso-lines through two warped noise layers, stretched flat, drifting slowly and broken up by
// a mask so they read as strands rather than contours, with the odd glint of a crystal.
float4 BrinePass(float4 vertex : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 w = World(uv);
    float t = Time * Speed;
    float2 p = w / (Scale * float2(900.0, 420.0));

    float2 warp = float2(N(p * 0.5 + float2(t * 0.020, 0.0)), N(p * 0.5 + float2(0.31, t * 0.015))) - 0.5;
    float n1 = N(p + warp * 0.35 + float2(t * 0.010, 0.0));
    float n2 = N(p * 1.9 + warp * 0.5 + float2(-t * 0.013, 0.17));
    float mask = smoothstep(0.42, 0.62, N(p * 0.8 + float2(0.71 - t * 0.008, 0.13)));

    float s1 = 1.0 - saturate(abs(n1 - 0.5) * 22.0);
    float s2 = 1.0 - saturate(abs(n2 - 0.5) * 28.0);
    float strand = max(s1 * s1, s2 * s2 * 0.6) * (0.2 + 0.8 * mask);

    float glint = step(0.9975, Hash(w + floor(Time * 1.5)));

    return Output(lerp(ColorB, ColorA, saturate(strand + glint)), saturate(strand + glint), vertex);
}

// Thick oil: broad, slow, heavily warped swirls whose crests carry a muted thin-film sheen.
float4 OilPass(float4 vertex : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 w = World(uv);
    float t = Time * Speed;
    float2 p = w / (Scale * 1400.0);

    float2 warp = float2(N(p + float2(t * 0.006, 0.0)), N(p + float2(0.43, -t * 0.005))) - 0.5;
    float n = N(p * 1.6 + warp * 0.9);

    float body = smoothstep(0.3, 0.7, n);
    float sheen = smoothstep(0.0, 1.0, saturate(1.0 - abs(n - 0.6) * 4.0));

    float3 film = 0.5 + 0.5 * cos(6.28318 * (n * 2.2 + float3(0.0, 0.33, 0.67)) + t * 0.3);
    film = lerp(float3(0.35, 0.35, 0.35), film, 0.55);
    float3 rgb = (lerp(ColorB, ColorA, body) + film * sheen * 0.5) * Tint;

    return Output(rgb, 0.3 * body + 0.22 * sheen, vertex);
}

// Caustic acid: a drifting web of contour lines through two cell layers, and bubbles rising in columns, each with a rim,
// a faint body and a highlight.
float4 AcidPass(float4 vertex : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 w = World(uv);
    float t = Time * Speed;

    float c1 = tex2D(CellSampler, w / (Scale * 260.0) + float2(t * 0.020, t * 0.013)).r;
    float c2 = tex2D(CellSampler, w / (Scale * 190.0) + float2(-t * 0.017, t * 0.021)).r;
    float caustic = max(1.0 - saturate(abs(c1 - 0.6) * 12.0), (1.0 - saturate(abs(c2 - 0.6) * 12.0)) * 0.8);
    caustic *= caustic;

    const float columnWidth = 16.0;
    const float cellHeight = 36.0;

    float column = floor(w.x / columnWidth);
    float rise = 16.0 + Hash(float2(column, 3.7)) * 28.0;
    float y = w.y + Time * Speed * rise;

    float row = floor(y / cellHeight);
    float h = Hash(float2(column, row));
    float radius = 2.5 + Hash(float2(row, column * 1.7)) * 3.5;
    float2 local = float2(w.x - column * columnWidth, y - row * cellHeight);
    float2 centre = float2(radius + 1.0 + h * (columnWidth - radius * 2.0 - 2.0) + sin(y * 0.15 + h * 6.28) * 1.0, cellHeight * 0.5);

    float d = length(local - centre);
    float present = step(0.6, h);
    float rim = (step(d, radius) - step(d, radius - 2.0)) * present;
    float fill = step(d, radius - 2.0) * present;
    float spot = step(length(local - centre + radius * 0.4), 1.5) * present;

    float3 rgb = lerp(Tint * ColorA, float3(1.0, 1.0, 1.0), saturate(0.2 + 0.4 * rim + spot));

    return Output(rgb, caustic * 0.5 + rim * 0.85 + fill * 0.2 + spot, vertex);
}

technique Brine
{
    pass BrineSurface
    {
        PixelShader = compile ps_3_0 BrinePass();
    }
}

technique Oil
{
    pass OilSurface
    {
        PixelShader = compile ps_3_0 OilPass();
    }
}

technique Acid
{
    pass AcidSurface
    {
        PixelShader = compile ps_3_0 AcidPass();
    }
}
