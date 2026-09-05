// Composites a tile-condition coverage mask.
//
// The field at s0 holds tile silhouettes alpha-blended in, so its ALPHA channel is coverage:
// 1 where a tile is fully marked, the tile's own shape where it is sloped or halved, and the
// stamp's intensity where the mark is weak. Colour is ignored; the tile sprites that were stamped
// are only there for their shape.
//
// The whole region is then eroded by world-locked FBM and cut at a hard threshold. That single
// step is what makes this worth doing instead of a 16x16 overlay texture: the noise runs across
// tile seams, so a wall of eight burnt blocks reads as one charred patch with holes in it rather
// than eight copies of the same little square. Cells that land near the cut become the edge --
// embers for a burn, rime for a freeze -- and the body behind them is tinted and lit.
//
//   Threshold      coverage (after erosion) at which the mark exists at all
//   NoiseStrength  how hard the FBM eats into it; ~2x Threshold gives holes through the interior
//   NoiseScale     FBM frequency; NoiseDrift scrolls it, WorldOffset locks it to the world
//   EdgeWidth      depth past the threshold over which edge fades into body
//   Body/EdgeColor the two ends of that ramp; only the body takes WorldLight, so the edge glows

sampler2D FieldSampler : register(s0);

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
float2 FieldTexel;      // 1 / field-target size; one texel = PixelScale screen px
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h

float Threshold;
float NoiseScale;
float NoiseStrength;
float2 NoiseDrift;
float EdgeWidth;

float3 BodyColor;
float3 EdgeColor;
float BodyAlpha;
float EdgeAlpha;
float3 WorldLight;

float SampleN(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    for (int i = 0; i < 4; i++)
    {
        sum += SampleN(p) * amp;
        p = p * 2.02 + 0.13;
        amp *= 0.5;
    }

    return sum;
}

float4 MaskPass(float2 uv : TEXCOORD0) : COLOR0
{
    // Snap to the field-texel grid, so every decision below happens once per field texel and the
    // result lands on the same chunky pixel grid the rest of the mod's effects use.
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    float coverage = tex2D(FieldSampler, cell).a;

    if (coverage <= 0.004)
        discard;

    // World-locked and drifting: the char boils in place while the camera pans across it, instead
    // of swimming with the screen.
    float2 p = (cell * float2(Aspect, 1.0) + WorldOffset) * NoiseScale + NoiseDrift * Time;
    float fbm = Fbm(p);

    // Centred on zero so the erosion digs and bulges either way, which keeps Threshold meaning
    // "coverage level" rather than drifting with NoiseStrength.
    float d = coverage - NoiseStrength * (fbm - 0.5);

    if (d < Threshold)
        discard;

    float depth = saturate((d - Threshold) / max(EdgeWidth, 0.001));

    float3 color = lerp(EdgeColor, BodyColor, depth);
    float alpha = lerp(EdgeAlpha, BodyAlpha, depth);

    // The body is terrain and takes the room's light; the edge is the condition's own emission and
    // does not, so embers stay lit in an unlit cave.
    color *= lerp(float3(1.0, 1.0, 1.0), WorldLight, depth);

    return float4(color * alpha, alpha);
}

technique TileConditionMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 MaskPass();
    }
}
