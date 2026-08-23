//   * The silhouette comes from art (Lightsource.png, a sector with its apex at the muzzle) rather
//     than from a procedural envelope, so the intake's shape is drawn rather than tuned.
//   * The output has two independent halves. RGB is the light the intake EMITS, premultiplied.
//     Alpha is how strongly it BENDS the world behind it. They are not the same distribution --
//     the glow wants to live in the strands, the bending wants to cover the whole cone -- and
//     packing them into one texture is what lets a single filter pass do both jobs.

// The cone art is a crop out of a larger sheet, so the incoming uv.x arrives as roughly 0.10..0.68
// rather than 0..1. Everything below reads u as a fraction of the beam's length, so left unnormalised
// it does not merely shift -- it breaks in ways that look like bad tuning rather than a bug. The end
// feathers stop applying at all, because smoothstep(1.0, 0.9, u) is exactly 1 for every u below 0.9.
// The neck stops applying, because u never gets near 0. And the strands run only 57% of their cycles
// across the beam, so a short cone resolves into a featureless wash instead of a bundle. So:
//
//   local = (uv - uSourceOffset) / uSourceSize    0..1 across the crop
//   u     = local.x                              0 at the apex/muzzle, 1 at the cap/aim point
//   v     = local.y                              0..1 across the quad, uAxis on the axis
//
// The strands do not run in v. They run in a cone-local transverse coordinate:
//
//   t = (v - uAxis) / halfWidth(u),   halfWidth(u) = 0.5 * u
//
// so t is -1 at the lower lip and +1 at the upper lip whatever the local width is. That is what
// makes the bundle FAN with the cone instead of running down it as a parallel ribbon of fixed
// thickness -- and a bundle that fans is the difference between an intake and a pipe.
//
// The flow signs are also deliberate. This is a vacuum, so everything travels toward decreasing u,
// which for a phase of the form (u * density + time * rate) means a POSITIVE rate. The old shader
// ran them negative and the beam read as something being fired.
//
//   uTime           animation clock
//   uWaves          strand wave count across the whole beam; the caller grows it sub-linearly with
//                   length, for the reason documented on BEAM_WAVE_EXPONENT
//   uStrandCount    1..8 strands
//   uAmplitude      bundle radius, in t units (1.0 reaches the cone lip)
//   uThickness      strand half-width in t units, before depth scaling
//   uDetune         how far apart the strands' frequencies are pushed; 0 collapses to a helix
//   uFlow           strand travel speed; positive draws them toward the muzzle
//   uSwellFreq      belt-waves along the beam
//   uSwellAmount    0..1 how much the belt-wave pinches the bundle
//   uBeadDensity    beads per unit length
//   uBeadFlow       bead travel speed, deliberately not uFlow
//   uBeadSharp      higher = tighter dots, lower = continuous strand
//   uBeadAmount     0..1 how much the beads modulate brightness
//   uWispScale      wisp texture repeats along the beam
//   uWispAmount     0..1 how much the wisps break up the strands
//   uCoreWidth      half-width of the soft axial core, in t units
//   uCoreStrength   core gain
//   uNeck           fraction of the beam given to the muzzle neck
//   uFade           end feather, in u
//   uSourceOffset   the crop's top-left in texture UV; see the normalisation note above
//   uSourceSize     the crop's size in texture UV
//   uAxis           where the cone's axis sits in the crop, 0..1
//   uShapeGain      normaliser for the cone art's peak alpha
//   uShapeWarp      how far the silhouette lookup is displaced, in crop UV
//   uShapeWarpScale frequency of that displacement along the beam
//   uFieldBody      how much of the cone's own coverage becomes lens strength
//   uFieldStrand    how much the strands add to lens strength
//   uColorCore      colour on the axis
//   uColorEdge      colour at the strands; the caller feeds the target tile's colour here
//   uGlow           gain on the emitted colour
//   uIntensity      0..1 spool-up envelope

sampler2D uImage0 : register(s0);   // the cone art; only its alpha is read

texture2D uWispTex;
sampler2D WispSampler = sampler_state
{
    Texture = <uWispTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float  uTime;
float  uWaves;
float  uStrandCount;
float  uAmplitude;
float  uThickness;
float  uDetune;
float  uFlow;

float  uSwellFreq;
float  uSwellAmount;

float  uBeadDensity;
float  uBeadFlow;
float  uBeadSharp;
float  uBeadAmount;

float  uWispScale;
float  uWispAmount;

float  uCoreWidth;
float  uCoreStrength;

float  uNeck;
float  uFade;

float2 uSourceOffset;
float2 uSourceSize;
float  uAxis;

float  uShapeGain;
float  uShapeWarp;
float  uShapeWarpScale;

float  uFieldBody;
float  uFieldStrand;

float3 uColorCore;
float3 uColorEdge;
float  uGlow;
float  uIntensity;

static const float TAU = 6.28318531;

float4 BeamPass(float2 uv : TEXCOORD0) : COLOR0
{
    // Crop-local, 0..1 across the art the quad was cut from. See the normalisation note in the
    // header -- the raw TEXCOORD0 is in the whole sheet's UV and is NOT 0..1 here.
    float2 local = saturate((uv - uSourceOffset) / uSourceSize);

    float u = local.x;
    float v = local.y;

    float travel = uTime * uFlow;

    // The silhouette warps itself. Two noise taps displace the lookup into the cone art, so the
    // outline boils along with the interior rather than staying a clean stretched sprite laid over
    // all that motion -- a hard, smooth edge is what made the cone read as one big soft decal
    // however much was going on inside it. Indexed by v rather than by the cone-local t below,
    // because t is unbounded at the apex and would tear the noise apart there.
    float2 warp = float2(
        tex2D(WispSampler, float2(u * uShapeWarpScale - uTime * 0.06, v * 0.6 + 0.11)).r,
        tex2D(WispSampler, float2(u * uShapeWarpScale * 0.7 + uTime * 0.045, v * 0.5 + 0.63)).r) - 0.5;

    // Back into sheet UV, so the displacement is a fraction of the CROP rather than of the sheet.
    float2 shapeUv = uv + warp * uShapeWarp * uSourceSize;

    // The art carries the silhouette. Gained because the source sector peaks well short of opaque,
    // and everything downstream wants a coverage of 1 inside the cone rather than whatever the
    // brush happened to lay down.
    float shape = saturate(tex2D(uImage0, shapeUv).a * uShapeGain);

    if (shape <= 0.0)
        return 0.0;

    // Cone-local transverse coordinate: -1 at the lower lip, +1 at the upper, whatever the local
    // width is. The floor keeps t finite at the apex, where the true half-width is zero.
    float halfWidth = max(0.5 * u, 0.04);
    float t = (v - uAxis) / halfWidth;

    // The neck. The cone art opens from a point, but a bundle of strands converging on a
    // mathematical point reads as an error rather than as a nozzle, so the strands are held closed
    // for the first fraction of the beam and open out behind it.
    float neck = smoothstep(0.0, max(uNeck, 1e-4), u);

    // Belt-wave: a slow travelling swell that pinches and opens the whole bundle along its length.
    // This is the difference between a spinning coil and a length of ribbon being whipped -- the
    // silhouette itself has to change along the beam, not just its contents.
    float swell = 1.0 - uSwellAmount * (0.5 + 0.5 * sin(u * TAU * uSwellFreq + travel * 0.5));

    float amp = uAmplitude * neck * swell;

    float strands = 0.0;
    float shade   = 0.0;

    [unroll]
    for (int k = 0; k < 8; k++)
    {
        // Lets the caller run any strand count without a second technique.
        float active = step((float)k, uStrandCount - 0.5);

        float fk    = (float)k / max(uStrandCount, 1.0);
        float phase = TAU * fk;

        // Detuned per strand, so no two share a period and the bundle never lines up the same way
        // twice. With uDetune at 0 every strand runs the same frequency and this collapses back
        // into the corkscrew it replaced.
        float f1 = uWaves * (1.0 + uDetune * fk);
        float f2 = uWaves * 0.41 * (1.0 + uDetune * 1.8 * fk);

        float a = u * f1 * TAU + travel + phase;
        float b = u * f2 * TAU - travel * 0.63 + phase * 1.7;

        // Two incommensurate sines: the path wanders instead of closing into a circle.
        float centre = amp * (0.72 * sin(a) + 0.28 * sin(b));

        // Near side of the bundle when positive, far side when negative.
        float front = cos(a) * 0.5 + 0.5;

        // A strand coming toward the viewer is both wider and brighter, which is what gives a flat
        // set of sines any sense of winding through depth.
        float w = uThickness * (0.55 + 0.45 * front);
        float d = (t - centre) / max(w, 1e-4);

        // Gaussian rather than a smoothstep edge: filaments of air want a soft shoulder, and the
        // overlap between neighbouring strands then reads as depth instead of as a seam.
        float fil = exp(-d * d) * active;

        strands += fil;
        shade   += fil * (0.4 + 0.6 * front);
    }

    shade = shade / max(strands, 1e-4);
    strands = saturate(strands);

    // Beads travel on their own clock, so what the eye tracks is particles being carried along the
    // strands rather than the strands themselves sliding. Running them at uFlow would lock the two
    // together and the whole thing would go back to looking like one rigid object in motion.
    float beadPhase = u * uBeadDensity + uTime * uBeadFlow;
    float bead = pow(saturate(0.5 + 0.5 * sin(beadPhase * TAU)), uBeadSharp);

    // Two scrolling taps at different rates. One alone just slides across the beam; two beating
    // against each other is what breaks the strands into filaments that curl and part. Both are
    // indexed by t rather than v, so the grain fans with the cone alongside the strands.
    float wispA = tex2D(WispSampler, float2(u * uWispScale + travel * 0.15, t * 0.25 + 0.25)).r;
    float wispB = tex2D(WispSampler, float2(u * uWispScale * 0.6 - travel * 0.09, t * 0.4 + 0.1)).r;
    float wisp = saturate(wispA * 0.65 + wispB * 0.55);

    float detail = lerp(1.0, bead, uBeadAmount) * lerp(1.0, wisp, uWispAmount);

    // Soft body down the axis so the strands have something to wind around. Left out of the bead
    // and wisp modulation so the beam keeps a continuous thread even when the strands break up.
    float core = exp(-pow(t / max(uCoreWidth, 1e-4), 2.0)) * uCoreStrength * neck;

    // Feathered at both ends -- the muzzle so it does not cut a hard edge against the gun, the far
    // end so the intake dissolves instead of stopping on a line.
    float ends = smoothstep(0.0, uFade, u) * smoothstep(1.0, 1.0 - uFade, u);

    float body = (strands * shade * detail + core) * shape * ends;

    // The two halves of the output, and the reason this target is RGBA rather than a single mask.
    //
    // Glow is where the intake is BRIGHT, and that is the strands: a cone filled with an even wash
    // of light reads as a decal laid over the tiles, which is exactly what the original additive
    // overlay looked like.
    //
    // Lens is where the intake BENDS, and that has to be the whole cone, because the field shader
    // differentiates this channel to find its refraction normal -- a mask that only exists where
    // the strands are would produce a normal per filament and shatter the world into ribbons
    // instead of pouring it through a lens. The strands still contribute a little on top, which is
    // what makes the bending flow rather than sit still.
    float3 colour = lerp(uColorCore, uColorEdge, saturate(strands));

    float glow = saturate(body * uGlow) * uIntensity;
    float lens = saturate(shape * ends * uFieldBody + body * uFieldStrand) * uIntensity;

    return float4(colour * glow, lens);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 BeamPass();
    }
}
