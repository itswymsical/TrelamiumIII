// A seal on the terrain, read off a seal type's canvas. Only struck tiles are stamped, so it shows
// where something hit it: ripples and shockwaves from SealField impulses, over a glow that builds
// with repeated hits. Frame inputs come from TileCanvas; shading is decided per CellSize world px.

sampler2D CanvasSampler : register(s0);     // r coverage, g charge; weighted sums

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

#define MAX_IMPULSES 16

float4 ImpulseA[MAX_IMPULSES];  // world x, world y, reach px, strength
float4 ImpulseB[MAX_IMPULSES];  // progress 0..1, unused, kind (0 ripple, 1 shockwave), unused

float Time;
float2 CanvasScreenSize;    // screen px the canvas covers
matrix ScreenToWorld;       // screen px -> world px, snapped to the drawn tile grid
matrix WorldToScreen;
float2 MaskOrigin;          // world px of the mask's top-left texel
float2 MaskTexel;           // 1 / mask size
float CellSize;             // world px per shading decision; 2 is one tile art pixel

float3 PrimaryColor;
float3 SecondaryColor;
float3 CoreColor;           // crest of a strong wave
float NoiseScale;           // FBM cycles per tile
float2 NoiseDrift;          // FBM scroll, uv/sec
float NoisePush;            // world px the FBM is pushed outward per unit of wave height
float RippleWavelength;     // world px between ripple crests
float Bands;                // brightness steps
float Opacity;

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

// Signed height of one impulse at world; push gathers the outward displacement it causes.
float Wave(float4 a, float4 b, float2 world, inout float2 push)
{
    float2 delta = world - a.xy;
    float d = length(delta);
    float p = b.x;
    float fade = 1.0 - p;
    float h;

    if (b.z < 0.5)
    {
        // Crests trailing a front that runs out to the reach.
        float x = d - a.z * p;
        float ahead = exp(-max(x, 0.0) * max(x, 0.0) * 4.0 / (RippleWavelength * RippleWavelength));
        float behind = exp(min(x, 0.0) / (RippleWavelength * 1.5));

        h = sin(x / RippleWavelength * 6.2832) * ahead * behind * fade * fade;
    }
    else
    {
        // One band easing out to the reach, over a flash at the centre.
        float front = a.z * (1.0 - fade * fade);
        float x = (d - front) / lerp(6.0, 20.0, p);

        h = exp(-x * x) * fade * sqrt(fade) + exp(-d * d / (a.z * a.z * 0.03 + 1.0)) * saturate(1.0 - p * 4.0);
    }

    h *= a.w * step(d, a.z * 1.2);
    push += delta / max(d, 0.001) * h;

    return h;
}

float4 SealPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 world = WorldAt(uv);
    float2 cellWorld = CellCentre(world);
    float2 cell = CanvasUv(cellWorld);
    float4 canvas = tex2Dlod(CanvasSampler, float4(cell, 0.0, 0.0));

    // Vanilla leaves unlit tiles out of the mask. Seals don't spill, so coverage past ~0.6 is inside solid tiles.
    float4 lightSum = tex2Dlod(LightSampler, float4(cell, 0.0, 0.0));
    float light = dot(lightSum.gba / max(lightSum.r, 0.0001), float3(0.333, 0.333, 0.333));
    float unlitSolid = step(0.6, canvas.r) * step(light, 0.03);

    if (max(Terrain(world), unlitSolid) < 0.5)
        discard;

    float seal = smoothstep(0.2, 0.5, canvas.r);

    if (seal <= 0.0)
        discard;

    float h = 0.0;
    float2 push = float2(0.0, 0.0);

    for (int i = 0; i < MAX_IMPULSES; i++)
        h += Wave(ImpulseA[i], ImpulseB[i], cellWorld, push);

    float noise = Fbm((cellWorld - push * NoisePush) / 16.0 * NoiseScale + NoiseDrift * Time);
    float ring = saturate(abs(h) * 1.4);

    // More of the pattern lights as charge builds, so a struck seal shimmers rather than filling flat.
    float charge = saturate(canvas.g);
    float glow = charge * (0.2 + 0.8 * smoothstep(0.5 - charge * 0.3, 0.75, noise));

    float intensity = saturate(seal * (ring * (0.55 + 0.9 * noise) + glow));
    intensity = floor(intensity * Bands) / Bands;

    if (intensity <= 0.0)
        discard;

    float3 color = lerp(PrimaryColor, SecondaryColor, noise);
    color = lerp(color, CoreColor, saturate(ring * 1.5 - 0.6));

    return float4(color * intensity * Opacity, 0.0);
}

technique SealField
{
    pass P0
    {
        PixelShader = compile ps_3_0 SealPass();
    }
}
