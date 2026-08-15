// Whiteout, then the frame returns through a spectral haze.
//
// Two effects in one pass because they are two halves of one moment: the flash blows the frame
// out, and the aberration is what the eye is left with while it recovers. Splitting them across
// two Filters.Scene passes would double the full-screen sampling cost for no visual gain.
//
//   uFlash   0..1 how far the frame is pushed toward white
//   uHaze    0..1 how far apart the colour channels are pulled
//   uSpread  peak channel offset at uHaze 1, as a fraction of the half-screen
//
// The haze re-samples the frame LEVELS times along the from-centre direction, each slice weighted
// by a rainbow triangle (R..G..B) and the total normalised. Offsets grow with distance from the
// centre, so the middle of the screen stays readable and the fringing builds toward the edges --
// which is what a real lens does, and what keeps this from reading as a blur.
//
// At uHaze 0 every slice samples the same texel and the weighted average collapses back to the
// original colour exactly, so the shader is a true identity when idle. That matters: this filter
// is left registered for the whole session and only its uniforms go to zero.

// COLOR0 is ignored on purpose. FilterManager.EndCapture draws the captured frame with
// Main.ColorOfTheSkies as the vertex colour whenever any filter is active, and with Color.White
// only when none is. So col is the sky tint, not white, and `* col` would crush the entire screen
// toward black anywhere the sky is dark -- space, underground, night -- including at uFlash 0.

sampler2D ImageSampler : register(s0);

float uFlash;
float uHaze;
float uSpread;

#define LEVELS 5

float3 SpectrumWeight(float t)
{
    // t 0..1 across the slices -> triangle weights peaking at R, then G, then B.
    float t0 = 3.0 * t - 1.5;
    return saturate(float3(-t0, 1.0 - abs(t0), t0));
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 fromCentre = uv - 0.5;
    float spread = saturate(uHaze) * uSpread;

    float3 sum = 0.0;
    float3 weightSum = 0.0;

    [unroll]
    for (int i = 0; i < LEVELS; i++)
    {
        float t = (float)i / (LEVELS - 1.0);
        float3 weight = SpectrumWeight(t);

        weightSum += weight;

        // (t - 0.5) * 2 maps the slice index to -1..1, so red pulls one way and blue the other
        // about an unmoved green. Centring it is what keeps the image from drifting as uHaze rises.
        sum += weight * tex2D(ImageSampler, uv - fromCentre * spread * (t - 0.5) * 2.0).rgb;
    }

    float3 scene = sum / max(weightSum, 0.0001);

    // Lerp toward white rather than adding it: adding would clip the bright end first and let the
    // frame reach full white in the highlights well before the shadows, which reads as a bloom
    // rather than a blowout.
    return float4(lerp(scene, 1.0, saturate(uFlash)), 1.0);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
