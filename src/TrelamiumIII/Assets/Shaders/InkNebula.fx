//   uTime        seconds
//   uAspect      (width/height, 1) so the disc and the swirl read circular
//   uCenter      0..1 singularity position
//   uScale       base FBM frequency
//   uPivot       median of the raw FBM sum for the bound noise texture, see Fbm()
//   uExpand      histogram stretch applied to the raw FBM, see the note at Fbm()
//   uWarp        domain-warp strength (turbulence)
//   uSwirl       winding strength; the rotation angle is uSwirl / (r + uSwirlCore)
//   uSwirlCore   softening on that divide, so the very centre doesn't shear to mush
//   uSpin        rigid rotation rate layered on top of the shear (rad/sec)
//   uDensity     coverage midpoint of the ink body
//   uContrast    half-width of the body's feather band
//   uBodyLevel   how much light the ink MASS emits.
//   uSheen       specular strength (the wet-chrome highlight)
//   uRelief      how steeply the height field tilts its normals; the "bumpiness" dial
//   uReliefEps   which SCALE of the field the gloss follows
//   uGloss       specular exponent; higher = tighter, harder highlight
//   uRim         grazing-angle highlight strength along crests
//   uRimPower    exponent on that rim term
//   uLightDir    key light direction; z is toward the viewer
//   uWeb         cellular webbing strength
//   uWebPower    exponent on the Voronoi tap; higher = thinner, sharper walls
//   uReach       how fast the ink thins with distance from the core
//   uHalo        floor on that falloff, so the far field isn't empty
//   uHorizon     event-horizon radius, aspect-corrected UV (0.5 = half-height)
//   uRing        photon-ring brightness
//   uRingWidth   photon-ring thickness
//   uDisc        accretion-band brightness
//   uDiscRadius  accretion-band centre radius
//   uDiscWidth   accretion-band thickness
//   uDiscFlatten how far the disc is squashed vertically; 1 = face on
//   uBeam        relativistic beaming, 0..1; brightens the +x limb of the disc
//   uVignette    corner falloff, 0..1
//   uOpacity     master scalar
//   uInkLow      colour of empty void (near black)
//   uInkHigh     colour of a full specular highlight (near white)

// s0 is the sprite SpriteBatch binds for us.
sampler2D ImageSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

texture2D uCellTex;
sampler2D CellSampler = sampler_state
{
    Texture = <uCellTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float  uTime;
float2 uAspect;
float2 uCenter;
float  uScale;
float  uPivot;
float  uExpand;
float  uWarp;
float  uSwirl;
float  uSwirlCore;
float  uSpin;
float  uDensity;
float  uContrast;
float  uBodyLevel;
float  uSheen;
float  uRelief;
float  uReliefEps;
float  uGloss;
float  uRim;
float  uRimPower;
float3 uLightDir;
float  uWeb;
float  uWebPower;
float  uReach;
float  uHalo;
float  uHorizon;
float  uRing;
float  uRingWidth;
float  uDisc;
float  uDiscRadius;
float  uDiscWidth;
float  uDiscFlatten;
float  uBeam;
float  uVignette;
float  uOpacity;
float3 uInkLow;
float3 uInkHigh;

float N(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

// There are four octaves, a fifth lands ~16x the base frequency and, once the domain warp has
// stretched it, reads as marbled wire rather than ink.
//
// Averaging octaves collapses the histogram: the raw sum lands in a band roughly 0.15
// wide, so every threshold downstream would need hundredth-scale tuning to do anything
// at all. It is recentred and stretched back out here, once, which lets uDensity /
// uContrast / uSheenWidth be ordinary 0..1 knobs.
//
// uPivot is the MEDIAN OF THE RAW SUM for whichever texture is bound, and it is a
// uniform rather than a constant because it is a property of that texture, not of this
// shader. FbmNoise2 sits at 0.682
float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += N(p) * amp;
        p = p * 2.03 + 0.17;
        amp *= 0.5;
    }

    return saturate((sum - uPivot) * uExpand + 0.5);
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 d = (uv - uCenter) * uAspect;
    float r = length(d);

    // Shear grows toward the centre, so the arms wind tighter the deeper they fall.
    // uSpin adds a rigid term on top; without it the far field is completely static,
    // because 1/r has effectively bottomed out by the time it reaches the corners.
    float twist = uSwirl / (r + uSwirlCore) + uTime * uSpin;
    float ca = cos(twist);
    float sa = sin(twist);
    float2 q = float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);

    // The drift is small and irrational-ish in both axes so the field never visibly
    // repeats against the swirl.
    float2 p = q * uScale + float2(0.017, -0.011) * uTime;

    // Domain warp: two decorrelated taps of the same field, sampled well below the base
    // frequency so the warp bends whole billows instead of shredding them.
    float2 w = float2(N(p * 0.23 + uTime * 0.013),
                      N(p * 0.23 - uTime * 0.011 + 4.19));
    p += (w - 0.5) * uWarp;

    // The ink is a SURFACE, not a density.
    //
    // This is the whole trick of the material. Ferrofluid is recognisable because it is
    // black and glossy, and gloss is a property of a surface NORMAL
    //
    // So the FBM is treated as a height field and lit. Two extra taps give a forward
    // difference for the slope; the difference is deliberately NOT divided by the epsilon,
    // which keeps it in a small range that uRelief can scale on a sane 0..10 dial.
    //
    // uReliefEps is the single most consequential knob in the shader and is worth
    // understanding before touching it. It sets which SCALE of the field the gloss
    // follows.
    float eps = max(uReliefEps, 1e-3);

    float h = Fbm(p);
    float hx = Fbm(p + float2(eps, 0.0));
    float hy = Fbm(p + float2(0.0, eps));

    float2 slope = float2(hx, hy) - h;
    float3 normal = normalize(float3(-slope * uRelief, 1.0));
    float3 light = normalize(uLightDir);

    // Tight, bright, and moving over the surface as the field flows underneath it.
    float spec = pow(saturate(dot(normal, light)), max(uGloss, 1.0)) * uSheen;

    // Grazing angles catch light all along a crest
    float rim = pow(1.0 - saturate(normal.z), max(uRimPower, 1.0)) * uRim;

    // Cellular walls, nudged by the same warp so the beading travels with the billows
    // instead of sliding through them.
    float cell = tex2D(CellSampler, q * uScale * 0.55 + (w - 0.5) * 0.12).r;
    float web = pow(saturate(cell), max(uWebPower, 1.0)) * uWeb;

    // The mass is a SILHOUETTE.
    float soft = max(uContrast, 0.001);
    float body = smoothstep(uDensity - soft, uDensity + soft, h);

    // Webbing only exists where there is ink to bead up
    // the Voronoi walls hang in the empty void looking like a wire mesh laid over the frame.
    float v = saturate(body * uBodyLevel + spec + rim + web * body);

    // Concentration toward the core. A galaxy is brightest where the mass is, and
    // without this the ink is an even wash with no depth to it at all.
    v *= lerp(saturate(uHalo), 1.0, exp(-r * uReach));

    // --- singularity ------------------------------------------------------------
    // 0 inside the horizon, 1 clear of it. Everything above is multiplied by this, so
    // the hole is a true absence rather than a black sprite laid over the top.
    float horizon = smoothstep(uHorizon, uHorizon * 1.35, r);

    // Photon ring: a thin gaussian just outside the horizon.
    float ringD = (r - uHorizon * 1.18) / max(uRingWidth, 1e-4);
    float ring = exp(-ringD * ringD) * uRing;

    // Accretion band. Measured on an ellipse, not a circle: a perfectly round annulus
    // reads as a lens flare around the hole, and it is the foreshortening that tells the
    // eye it is looking at a flat disc edge-on rather than a glow.
    float2 de = float2(d.x, d.y * max(uDiscFlatten, 1.0));
    float discD = (length(de) - uDiscRadius) / max(uDiscWidth, 1e-4);

    // Beaming: the limb rotating toward the viewer is brighter. Asymmetry is most of
    // what makes the disc read as spinning rather than sitting still.
    float beam = 1.0 + uBeam * (d.x / max(r, 1e-4));

    // Modulated by the height field so the disc inherits the ink's own structure and
    // reads as the same material being shredded, rather than as a clean painted ring.
    float disc = exp(-discD * discD) * uDisc * beam * (0.55 + 0.45 * h);

    v = (v + ring + disc) * horizon;

    // Corner falloff, measured in the same aspect-corrected space so it stays circular.
    float vigR = length((uv - 0.5) * uAspect) / max(0.5 * length(uAspect), 1e-4);
    v *= 1.0 - uVignette * smoothstep(0.35, 1.0, vigR);

    float mask = tex2D(ImageSampler, uv).a;
    float3 ink = lerp(uInkLow, uInkHigh, saturate(v));

    // Opaque: this is the base layer of its render target, and everything after it
    // composites additively on top.
    return float4(ink * uOpacity * mask * col.a, 1.0);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
