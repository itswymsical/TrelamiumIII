// fisheye magnification inside a soft disc, as if a lens were pressed
// against the frame. Shares the radial remap in PinchShader.fx but on the other side
// of 1 (uPower > 1 pulls samples in toward the focus = magnify), and adds a
// chromatic fringe at the rim, which is what a real lens does and what sells the
// glass illusion on a beat hit.
//
//   rn  = dist / radius            0 at the focus .. 1 at the rim
//   rd  = pow(rn, uPower)          uPower > 1 => samples come from nearer the focus
//   src = centre + dir * rd * radius
//
// rd == rn == 1 at the rim, so the disc joins the untouched frame seamlessly.
//
//   uCenter    0..1 lens centre
//   uPower     radial exponent; 1 = identity, >1 = bulge (4 is a fat fisheye)
//   uRadius    lens radius in aspect-corrected UV (0.5 ~ reaches the frame edge)
//   uSoftness  0.01..1 rim feather
//   uFringe    chromatic dispersion at the rim (0 = clean)
//   uMix       0..1 effect weight (already x the element's lane fade)
//   uAspect    (width/height, 1) so the lens reads circular

// DO NOT MULTIPLY BY COLOR0 while this is used as a Filters.Scene pass.
// FilterManager.EndCapture draws the captured frame with Main.ColorOfTheSkies as the vertex
// colour whenever any filter is active, and with Color.White only when none is. So COLOR0 is
// the sky tint, not white, and `* col` crushes the ENTIRE screen toward black anywhere the sky
// is dark -- space, glowing mushroom, underground, night -- and does so even at uMix 0.
// If this shader is ever reused for a plain sprite draw, tint through a uniform instead.

sampler2D ImageSampler : register(s0);

float2 uCenter;
float  uPower;
float  uRadius;
float  uSoftness;
float  uFringe;
float  uMix;
float2 uAspect;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 d = (uv - uCenter) * uAspect;
    float dist = length(d);
    float radius = max(uRadius, 1e-4);

    // NOT saturated. Clamping rn to 1 would make every pixel past the rim resolve to
    // the rim itself, smearing that one ring radially across the whole surround.
    float rn = dist / radius;

    // 1 well inside the lens, 0 at the rim and beyond (smoothstep saturates at rn >= 1).
    float soft = clamp(uSoftness, 0.01, 1.0);
    float edge = 1.0 - smoothstep(1.0 - soft, 1.0, rn);

    float rd = pow(max(rn, 1e-5), max(uPower, 0.01));

    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 0.0);
    float2 basis = (dir * radius) / uAspect;

    // One weight carries both the rim feather and the outside-the-lens passthrough, so
    // pixels past the rim resolve to EXACTLY uv.
    float w = edge * saturate(uMix);

    // Dispersion grows toward the rim (0 at the centre, like real glass) and rides the
    // same weight so it vanishes with the lens.
    float f = uFringe * 0.05 * saturate(rn) * w;

    float2 baseUv = lerp(uv, uCenter + basis * rd, w);
    float2 spread = basis * rd * f;

    float4 g = tex2D(ImageSampler, baseUv);
    float r = tex2D(ImageSampler, baseUv + spread).r;
    float b = tex2D(ImageSampler, baseUv - spread).b;

    // col is the sky tint, not white. Ignored on purpose -- see the note at the top.
    return float4(r, g.g, b, g.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
