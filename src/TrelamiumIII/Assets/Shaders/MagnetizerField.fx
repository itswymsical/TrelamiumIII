// The screen half of the Lodestone Magnetizer's intake: one full-screen pass that reads the beam
// field target and wobbles the frame inside it.
//
//   uFieldTex   RGBA from MagnetizerBeam.fx. RGB is premultiplied emitted light, A is the mask
//   uNoiseTex   tiling noise for the wobble
//
// WHAT THIS USED TO DO, AND WHY IT NO LONGER DOES
//
// This pass also carried a refraction lens: it treated the mask's alpha as a height field, took its
// gradient, and displaced the frame along it so the rock around the cone was visibly dragged into
// it. That worked, in the sense that it did what it said. It was also too much. A magnetizer is a
// mining tool used at close range against a wall of tiles, and bending those tiles
//
// The shape of the intake is now carried by the sine trail, which is a pixelated ribbon drawn as
// primitives, and by the ore metaballs and block chips travelling up it. Those are all opaque,
// chunky, vanilla-looking things: they read as an intake without touching the frame behind them at
// all. What is left here is the one screen effect that earns its place, because nothing drawn on
// top of the frame can fake it:
//
//   uMix            0..1 effect weight; must resolve to an exact passthrough at 0
//   uWobble         peak displacement in UV, at full mask strength
//   uWobbleScale    FBM frequency
//   uWobbleDrift    uv/sec scroll of the FBM
//   uTime           seconds
//   uWorldOffset    screenPosition / screenHeight, to lock the FBM to the world
//   uAspect         screen w/h, so the FBM is not stretched
//   uGlow           gain on the field's emitted light
//   uLightResponse  0..1 how far the added light tracks the brightness behind it

sampler2D ImageSampler : register(s0);

texture2D uFieldTex;
sampler2D FieldSampler = sampler_state
{
    Texture = <uFieldTex>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
};

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float  uMix;

float  uWobble;
float  uWobbleScale;
float2 uWobbleDrift;
float  uTime;
float2 uWorldOffset;
float  uAspect;
float2 uZoom;

float  uGlow;
float  uLightResponse;

float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    // Stated, not left to the compiler to work out. A tex2D inside a loop is a gradient instruction
    // with a varying iteration count as far as fxc is concerned, so it unrolls this anyway and warns
    // about having done so; asking for it up front is the same code without the noise.
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += tex2D(NoiseSampler, p).r * amp;
        p = p * 2.02 + 0.13;
        amp *= 0.5;
    }

    return sum;
}

// Screen uv -> world position, in units of screen heights. See GasFbm.fx for why the zoom has to
// be undone here: without it the noise carries a (1 - uZoom) * cameraCentre error that moves with
// the camera, so the shimmer slides across the world whenever the player pans while zoomed.
float2 WorldUv(float2 uv)
{
    float2 aspect = float2(uAspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / uZoom + halfExtent + uWorldOffset;
}

float4 FieldPass(float2 uv : TEXCOORD0) : COLOR0
{
    float4 bg = tex2D(ImageSampler, uv);

    // The filter stays registered for the whole session and FilterManager keeps running a
    // deactivated pass for about a second while it ramps down, so idle has to be free and it has to
    // be exact.
    if (uMix <= 0.001)
        return bg;

    float4 field = tex2D(FieldSampler, uv);
    float strength = saturate(field.a);

    // Nothing here reaches beyond the cone, so everything outside it is handed straight back rather
    // than paying for the noise below. The glow is premultiplied, so it is zero wherever the mask is
    // and there is nothing to add either.
    if (strength <= 0.002 && dot(field.rgb, 1.0) <= 0.002)
        return bg;

    // World-locked and drifting, so the shimmer churns in place while the camera moves across it
    // rather than swimming along with the screen. Same convention as GasFbm.fx.
    float2 p = WorldUv(uv) * uWobbleScale + uWobbleDrift * uTime;

    // 0.47 rather than 0.5: the noise texture's mean sits slightly below centre, and centring on its
    // actual mean is what keeps the warp from carrying a constant bias in one direction -- which
    // would read as the whole cone sliding rather than shimmering.
    float2 wobble = float2(Fbm(p) - 0.47, Fbm(p + 7.31) - 0.47);

    // uZoom because the offset is in SCREEN uv while uWobble is authored against the cone, which the
    // camera magnifies. Left unscaled, a zoomed-in cone refracts proportionally less and flattens.
    float3 warped = tex2D(ImageSampler, uv + wobble * uWobble * uZoom * strength).rgb;

    // How bright the world already is behind this pixel of field.
    //
    // Light that is ADDED has to be scaled by this or the beam cannot hold together across a frame
    // with mixed lighting: a flat addition is invisible against daylit sky and a bright wash against
    // unlit stone, so the same intake reads as two different materials depending on what it happens
    // to be crossing. Scaling by luminance holds it at a constant RATIO to its surroundings instead.
    //
    // sqrt rather than raw luminance because the raw value is far too aggressive underground, where a
    // linear response would delete the beam in exactly the place a mining tool is used.
    float lum = dot(warped, float3(0.299, 0.587, 0.114));
    float response = lerp(1.0, sqrt(saturate(lum)), saturate(uLightResponse));

    // Already premultiplied by the beam pass, so this is a straight add rather than a lerp toward a
    // colour -- the intake emits light, it does not replace what is behind it.
    float3 lit = warped + field.rgb * uGlow * response;

    return float4(lerp(bg.rgb, lit, saturate(uMix)), bg.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 FieldPass();
    }
}
