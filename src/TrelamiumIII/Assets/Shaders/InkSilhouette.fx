// Turns a coverage field of stamped sprites into a single inky silhouette that is being
// dragged somewhere. Three things happen in one pass, in this order, and the order is the
// whole effect:
//
//   1. WOBBLE. The lookup UV is displaced by a world-locked FBM field before anything else
//      reads it. This warps the SILHOUETTE, not just what is inside it -- the outline is the
//      first thing the eye resolves, and a clean outline reads as a sprite however much is
//      churning within it. Same reasoning, and the same world-locking convention, as
//      MagnetizerField.fx's uShapeWarp.
//
//   2. RADIAL SMEAR. The warped field is then sampled repeatedly along the ray toward
//      uCenter, with a tangential slice so the smear winds rather than merely stretching.
//      This is what says the mass is being PULLED: a static silhouette with a moving
//      interior looks like a decal, and coverage that trails back toward the drain looks
//      like something losing its grip on itself.
//
//   3. HARD CUT plus RIM. The smeared coverage is thresholded with no smoothstep, and a
//      one-texel neighbour test against the same threshold paints the rim. Cutting after
//      the smear rather than before is what fuses separate stamps into one silhouette --
//      two pieces whose own coverage never touches will still both trail into the gap
//      between them and cross the threshold there.
//
// Body shading is posterised into flat bands. There is no interior detail to resolve in
// ink: void, mass, rim.
//
//   uFieldTexel    1 / field-target size; one texel = uPixelScale screen px
//   uAspect        screen w/h, so the smear disc and the noise read circular
//   uWorldOffset   screenPosition / screenHeight
//   uTime          seconds
//   uWobble        peak UV displacement of the silhouette lookup
//   uWobbleScale   FBM frequency for that displacement
//   uWobbleDrift   uv/sec scroll of the FBM field
//   uCenter        0..1 focus the coverage is smeared toward (the vortex)
//   uSmear         radial reach, as a fraction of the centre->pixel vector
//   uSpin          tangential reach, same units; 0 = pure stretch, >0 = winds
//   uFalloff       how fast the smear ramps up away from the focus (0 = uniform)
//   uThreshold     coverage level where ink exists
//   uBodyAlpha     opacity of the mass
//   uBands         posterise step count for the body shade
//   uShadowColor   sparse ink
//   uBodyColor     dense ink
//   uCoreColor     deepest ink
//   uRimColor      the rim along the cut
//   uRimAlpha      opacity of that rim
//   uOpacity       master envelope, so a host can fade the whole thing
//
// TAPS is a compile-time constant: ps_3_0 under MojoShader cannot be trusted with a
// uniform loop bound, so quality is fixed and the reach is what gets tuned. Same
// constraint RadialBlur.fx documents.

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

float2 uFieldTexel;
float2 uWorldOffset;
float  uAspect;
float2 uZoom;
float  uTime;

float  uWobble;
float  uWobbleScale;
float2 uWobbleDrift;

float2 uCenter;
float  uSmear;
float  uSpin;
float  uFalloff;

float  uThreshold;
float  uBodyAlpha;
float  uBands;

float3 uShadowColor;
float3 uBodyColor;
float3 uCoreColor;
float3 uRimColor;
float  uRimAlpha;
float  uOpacity;

#define TAPS 8

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

// Screen uv -> world position, in units of screen heights. See GasFbm.fx for why the zoom has to
// be undone here: without it the noise carries a (1 - uZoom) * cameraCentre error that moves with
// the camera, so the pattern slides across the world whenever the player pans while zoomed.
float2 WorldUv(float2 uv)
{
    float2 aspect = float2(uAspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / uZoom + halfExtent + uWorldOffset;
}

// World-locked wobble offset for a uv. Boils in place while the camera pans, instead of
// swimming along with the screen.
float2 Warp(float2 uv)
{
    float2 p = WorldUv(uv) * uWobbleScale + uWobbleDrift * uTime;

    float wx = Fbm(p);
    float wy = Fbm(p + 7.31);

    // -0.47 rather than -0.5: the FBM sum sits slightly above centre for the textures this
    // is bound to, and an uncentred warp drifts the whole silhouette off its stamps.
    //
    // Scaled by uZoom because the result is a displacement in SCREEN uv while uWobble is authored
    // against the silhouette, which the camera magnifies. Left unscaled, a zoomed-in silhouette
    // gets a proportionally smaller wobble and visibly stiffens.
    return (float2(wx, wy) - 0.47) * uWobble * uZoom;
}

// Coverage at a uv, smeared toward uCenter. The taper gives the trail a bright head and a
// fading tail rather than an even ghost, so it reads as motion with a direction.
float Coverage(float2 uv)
{
    float2 d = uv - uCenter;
    float2 dd = d * float2(uAspect, 1.0);

    // 0 at the focus, ~1 at the frame edge in aspect-corrected space.
    float r = saturate(length(dd) / max(0.5 * length(float2(uAspect, 1.0)), 1e-4));
    float ramp = pow(max(r, 1e-4), max(uFalloff, 0.0));

    // Inward along the ray, so samples trail back toward the drain, plus a perpendicular
    // slice for the wind. The perpendicular is taken in aspect-corrected space and projected
    // back so the arc stays circular on a wide target.
    float2 radial  = -d * (uSmear * ramp) / TAPS;
    float2 tangent = (float2(-dd.y, dd.x) / float2(uAspect, 1.0)) * (uSpin * ramp) / TAPS;

    float2 stepv = radial + tangent;

    float sum = 0.0;
    float wsum = 0.0;

    [unroll]
    for (int i = 0; i < TAPS; i++)
    {
        float w = 1.0 - (float)i / TAPS;
        sum += tex2D(FieldSampler, uv + stepv * (float)i).r * w;
        wsum += w;
    }

    return sum / max(wsum, 1e-4);
}

float4 SilhouettePass(float2 uv : TEXCOORD0) : COLOR0
{
    // Snap to the field grid, so every decision below happens once per field texel and the
    // result lands on that grid as chunky pixels rather than smooth curves.
    float2 cell = (floor(uv / uFieldTexel) + 0.5) * uFieldTexel;

    float2 warp = Warp(cell);

    float d = Coverage(cell + warp);

    if (d < uThreshold)
        discard;

    // One-texel neighbour test, each neighbour warped by the same offset so the rim follows
    // the wobbled outline rather than the unwobbled one underneath it.
    float2 tx = float2(uFieldTexel.x, 0.0);
    float2 ty = float2(0.0, uFieldTexel.y);

    float neighborMin = min(
        min(Coverage(cell - tx + warp), Coverage(cell + tx + warp)),
        min(Coverage(cell - ty + warp), Coverage(cell + ty + warp)));

    if (neighborMin < uThreshold)
        return float4(uRimColor * uRimAlpha * uOpacity, uRimAlpha * uOpacity);

    // Shade by depth past the threshold, posterised into flat bands.
    float t = saturate((d - uThreshold) / max(1.0 - uThreshold, 0.001));
    float shade = floor(t * uBands) / max(uBands - 1.0, 1.0);

    float3 color = lerp(uShadowColor, uBodyColor, saturate(shade * 2.0));
    color = lerp(color, uCoreColor, saturate(shade * 2.0 - 1.0));

    float alpha = uBodyAlpha * uOpacity;

    return float4(color * alpha, alpha);
}

technique InkSilhouette
{
    pass P0
    {
        PixelShader = compile ps_3_0 SilhouettePass();
    }
}
