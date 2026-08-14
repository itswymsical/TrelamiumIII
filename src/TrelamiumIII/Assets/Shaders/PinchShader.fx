// sucks the frame toward a focal point inside a soft disc, with an optional
// swirl so the pull can spiral instead of running straight in. The inverse of
// VisBulge.fx (same radial remap, exponent on the other side of 1).
//
// The remap works on the NORMALISED radius inside the disc:
//
//   rn  = dist / radius            0 at the focus .. 1 at the rim
//   rd  = pow(rn, uPower)          uPower < 1 => samples come from further out
//   src = centre + dir * rd * radius
//
// At the rim rd == rn == 1, so the warp is continuous with the untouched frame
// outside the disc, no seam. uSoftness feathers the derivative too, which kills
// the faint ring a hard-edged disc leaves behind.
//
//   uCenter    0..1 focal point
//   uPower     radial exponent; 1 = identity, <1 = pinch (0.15 is a hard funnel)
//   uRadius    disc radius in aspect-corrected UV (0.5 ~ reaches the frame edge)
//   uSoftness  0.01..1 rim feather
//   uSwirl     radians of twist at the focus, easing to 0 at the rim
//   uMix       0..1 effect weight (already x the element's lane fade)
//   uAspect    (width/height, 1) so the disc reads circular

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
float  uSwirl;
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

    // 1 well inside the disc, 0 at the rim and beyond (smoothstep saturates at rn >= 1).
    float soft = clamp(uSoftness, 0.01, 1.0);
    float edge = 1.0 - smoothstep(1.0 - soft, 1.0, rn);

    float rd = pow(max(rn, 1e-5), max(uPower, 0.01));

    // Twist the direction before re-projecting; fades out on the same envelope, so the
    // rotation itself is small near the rim and the blend below can't cut a chord
    // across the disc.
    float ang = uSwirl * edge;
    float ca = cos(ang);
    float sa = sin(ang);
    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 0.0);
    float2 rot = float2(dir.x * ca - dir.y * sa, dir.x * sa + dir.y * ca);

    float2 warped = uCenter + (rot * (rd * radius)) / uAspect;

    // One weight carries both the rim feather and the outside-the-disc passthrough, so
    // pixels past the rim resolve to EXACTLY uv. Lerping the coordinate (not the colour)
    // also means a partial mix reads as a weaker pinch, not a double-exposed ghost.
    float2 src = lerp(uv, warped, edge * saturate(uMix));
    // col is the sky tint, not white. Ignored on purpose -- see the note at the top.
    return tex2D(ImageSampler, src);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
