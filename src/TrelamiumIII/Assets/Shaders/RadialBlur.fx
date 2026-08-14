// this radial (zoom) blur smears the frame along the ray from a focal point outward,
// with an optional tangential component that turns it into a spin/whirl blur. The
// classic "drop hits, screen rushes at you" post pass.
//
// The focal point stays sharp and the streak grows toward the edges (uFalloff), so
// the CD/playfield can sit in the clear centre while the background tears outward.
//
//   uCenter     0..1 focal point (0.5,0.5 = frame centre)
//   uStrength   radial reach, as a fraction of the centre->pixel vector
//   uSpin       tangential reach (same units) - 0 = pure zoom, >0 = whirl
//   uFalloff    how fast the streak ramps up away from the focus (0 = uniform)
//   uMix        0..1 dry/wet (already x the element's lane fade)
//   uAspect     (width/height, 1) so the falloff disc reads circular
//
// TAPS is a compile-time constant: ps_3_0 + MojoShader can't be trusted with a
// uniform loop bound, so quality is fixed and the artist tunes reach instead.

sampler2D ImageSampler : register(s0);

float2 uCenter;
float  uStrength;
float  uSpin;
float  uFalloff;
float  uMix;
float2 uAspect;

#define TAPS 12

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 src = tex2D(ImageSampler, uv);

    float2 d = uv - uCenter;
    float2 dd = d * uAspect;

    // 0 at the focus, ~1 at the frame edge (half-diagonal in aspect-corrected space).
    float r = saturate(length(dd) / max(0.5 * length(uAspect), 1e-4));
    float ramp = pow(max(r, 1e-4), max(uFalloff, 0.0));

    // Per-tap step: inward along the ray (so samples trail back toward the focus),
    // plus a perpendicular slice for the whirl.
    float2 radial = -d * (uStrength * ramp) / TAPS;
    // Perpendicular taken in aspect-corrected space, then projected back to UV so the
    // whirl arc stays circular on a wide target.
    float2 tangent = (float2(-dd.y, dd.x) / uAspect) * (uSpin * ramp) / TAPS;

    // Per-pixel sub-step offset. Without it, 12 discrete taps leave visible concentric
    // ghost rings in a heavy smear; dithering trades those for fine grain, which the
    // downstream blur/bloom swallow.
    float dither = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);

    float2 stepv = radial + tangent;
    float4 sum = 0.0;
    float wsum = 0.0;

    [unroll]
    for (int i = 0; i < TAPS; i++)
    {
        // Linear taper fade!!!
        // the near taps dominate, so the streak has a bright head and a
        // fading tail instead of an even ghost. Weight uses the un-dithered index so
        // the taper stays stable pixel to pixel.
        float w = 1.0 - (float)i / TAPS;
        sum += tex2D(ImageSampler, uv + stepv * ((float)i + dither)) * w;
        wsum += w;
    }

    float4 blurred = sum / max(wsum, 1e-4);
    return lerp(src, blurred, saturate(uMix)) * col;
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
