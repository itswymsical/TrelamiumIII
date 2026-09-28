// Whole-frame chromatic aberration

// Multi-slice spectral sampling: the frame is re-sampled LEVELS times along the
// from-center direction, each slice weighted by a rainbow triangle (R..G..B),
// then normalized. Offsets grow with distance from screen center, so the middle
// stays readable while edges fringe.
//
//   uIntensity  0..1 charter intensity; scales the spread
//   uSpread     offset knob (ChartEffect.Param * 2): 0..2x base spread, 1 = neutral
//
// COLOR0 is ignored on purpose, see the note in ScreenFlash.fx. FilterManager.EndCapture draws
// the captured frame with Main.ColorOfTheSkies as the vertex colour while any filter is active,
// so `* col` would darken the whole screen underground and at night.

sampler2D ImageSampler : register(s0);

float uIntensity;
float uSpread;

#define LEVELS 3
#define MAX_SPREAD 0.035   // fraction of half-screen at full intensity, neutral offset

float3 SpectrumWeight(float t)
{
    // t 0..1 across slices -> triangle weights peaking at R, G, B.
    float t0 = 3.0 * t - 1.5;
    return saturate(float3(-t0, 1.0 - abs(t0), t0));
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 dir = uv - 0.5;
    float spread = uIntensity * MAX_SPREAD * uSpread;

    float3 sum = 0.0;
    float3 wsum = 0.0;

    [unroll]
    for (int i = 0; i < LEVELS; i++)
    {
        float t = (float)i / (LEVELS - 1.0);
        float3 w = SpectrumWeight(t);
        wsum += w;
        sum += w * tex2D(ImageSampler, uv - dir * spread * (t - 0.5) * 2.0).rgb;
    }

    return float4(sum / max(wsum, 0.0001), 1.0);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
