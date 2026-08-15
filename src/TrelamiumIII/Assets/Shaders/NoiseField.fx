// Drifting volumetric haze that genuinely OCCLUDES what's behind it
// (unlike the additive effects, which can only brighten). Good for veiling the playfield
// on a breakdown.
//
// Composites over s0 in-shader with a straight lerp rather than relying on a blend state,
// so one shader serves both call sites unchanged: the visualizer element's
// VisualizerPipeline.PostProcess and the chart effect's ScreenFxRenderer.PostProcess
// (both bind the frame to s0 and draw Opaque). uOpacity is the contribution scalar each
// side folds its own weight into. The element its lane fade, the chart effect its
// envelope. Same trick BloomShader already uses to serve both paths.
//
// Built on the tiling noise texture through an explicit wrap sampler, the VisNoise /
// VisEnergyPortal approach so the FBM tiles past 0..1 instead of edge-clamping into
// a smear. Two decorrelated low-frequency taps domain-warp the lookup, which is what
// turns flat FBM into something that curls.
//
//   uTime       seconds
//   uScale      base FBM frequency
//   uRise       upward scroll speed (smoke climbs)
//   uDrift      sideways scroll speed
//   uWarp       domain-warp strength (turbulence)
//   uCoverage   density threshold MIDPOINT, the noise value that lands at 50% alpha
//   uSoftness   width of the feather band centred on that midpoint (= inverse contrast)
//   uGround     0..1 bottom-anchoring: 0 = even haze, 1 = hugs the lower edge
//   uShade      how dark the thin wisps go relative to the tint (0..1)
//   uOpacity    max alpha (already x the element's lane fade)
//   uReact      0..1 audio boost, billows the smoke on hits
//   uAspect     (width/height, 1) so the curls don't stretch on a wide target
//   uTint       smoke colour

sampler2D SceneSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float  uTime;
float  uScale;
float  uRise;
float  uDrift;
float  uWarp;
float  uCoverage;
float  uSoftness;
float  uGround;
float  uShade;
float  uOpacity;
float  uReact;
float2 uAspect;
float3 uTint;

float SampleN(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

// Four octaves, not five. The fifth lands ~16x the base frequency, and once the domain
// warp stretches it the result reads as marbled wire rather than smoke.
float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += SampleN(p) * amp;
        p = p * 2.02 + 0.13;
        amp *= 0.5;
    }
    return sum;
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Aspect-correct so a wide target doesn't smear the curls horizontally. Scrolling
    // the field DOWN (+v over time) makes the pattern appear to climb, because
    // SpriteBatch puts v = 0 at the top.
    float2 p = float2(uv.x * uAspect.x, uv.y) * uScale;
    p += float2(uDrift, uRise) * uTime;

    // Domain warp: two decorrelated taps of the same field, sampled well below the base
    // frequency so the warp bends whole billows instead of shredding them.
    float2 w = float2(SampleN(p * 0.22 + uTime * 0.02),
                      SampleN(p * 0.22 - uTime * 0.015 + 4.17));
    p += (w - 0.5) * uWarp;

    float n = Fbm(p);

    // Audio billow: a hit effectively lowers the threshold, so more of the field crosses
    // into visible smoke instead of the same shape just getting brighter.
    float cover = saturate(uCoverage - uReact * 0.12);
    float soft = max(uSoftness, 0.001);

    // The band is CENTRED on uCoverage, so softness changes contrast without also
    // sliding the density so the two knobs stay independent in the editor.
    //
    // Scale matters more than it looks: four octaves at halving amplitude average four
    // samples of a mean-0.5 field, so n lands in a narrow ~0.47 +/- 0.04 band. A feather
    // wider than a few hundredths spans the entire distribution and flattens the smoke
    // into featureless mist.
    float a = smoothstep(cover - soft * 0.5, cover + soft * 0.5, n);

    // Bottom-anchoring: v = 1 is the lower edge, so weight toward it. uGround = 0
    // leaves the haze even across the frame.
    float ground = lerp(1.0, smoothstep(-0.15, 1.0, uv.y), saturate(uGround));
    a *= ground;

    a = saturate(a * uOpacity);

    // Thin wisps read darker than the dense core, a cheap stand-in for self-shadowing.
    float3 smoke = uTint * lerp(saturate(uShade), 1.0, saturate(n * 1.4));

    float4 scene = tex2D(SceneSampler, uv);
    return float4(lerp(scene.rgb, smoke, a), scene.a);   // occludes, composited in-shader
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
