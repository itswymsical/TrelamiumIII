// The one shader every accumulated field composites through: gas, liquid, plasma, ink.
//
//   Threshold      field value where the material exists (after noise erosion) || WARNING: a high threshold will make the noise cover the screen
//   FieldGain      stamp overdrive; a byte vertex colour cannot carry it, so it lands here
//   DepthRange     field units from Threshold to a fully shaded interior || larger = flatter body
//   NoiseStrength  how hard the FBM eats the field edge (0 = clean metaballs) || WARNING: a high threshold will make the noise cover the screen
//   NoiseScale     FBM frequency; Drift scrolls it, WorldOffset locks it to world || lower = bigger, higher = noisier
//   NoiseStretch   anisotropic FBM: (1, <1) elongates features vertically so the body
//                  streaks like runs and drips instead of boiling. (1, 1) = isotropic
//   Bands          this determines how smooth/how much AA is applied to the nois || anything under 8 looks very sharp so i tend to stay above that
//   Shadow/Body/CoreColor   yur colors
//   OutlineColor/OutlineAlpha  the 2px rim
//   HighlightColor/HighlightAlpha  cells whose UPWARD neighbor falls outside the cut are a
//                  liquid's surface and draw a bright top line (every vanilla liquid has one);
//                  side and bottom edges keep the ink outline. HighlightAlpha of 0 turns the
//                  surface line off entirely and is what makes this a gas rather than a liquid.

sampler2D FieldSampler : register(s0);

// The attribute half of the field: flow in RG (bias-encoded), bind in B, variant in A. Every
// channel holds coverage * value and is recovered by dividing against coverage -- see FieldStamp.
texture2D uAttrTex;
sampler2D AttrSampler = sampler_state
{
    Texture = <uAttrTex>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
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

float Time;
float2 FieldTexel;      // 1 / field-target size; one texel = 2 screen px
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h
float2 Zoom;            // Main.GameViewMatrix.Zoom

float Threshold;
float FieldGain;        // stamp overdrive; a byte vertex colour cannot carry it, so it lands here
float DepthRange;       // field units from Threshold to a fully shaded interior
float NoiseScale;
float NoiseStrength;
float2 NoiseDrift;      // uv/sec scroll of the FBM field; liquids usually drift downward
float2 NoiseStretch;    // per-axis frequency; (1, 0.45) = tall streaky features, (1, 1) = none

float Bands;
float BodyAlpha;
float BodyAlphaFloor;   // alpha of the thinnest shaded cell, as a fraction of BodyAlpha

float3 ShadowColor;     // thin film / sparse edges
float3 BodyColor;
float3 CoreColor;       // deep pooled center

float3 OutlineColor;
float OutlineAlpha;

float3 HighlightColor;  // the bright surface line on upward-facing edges
float HighlightAlpha;   // 0 = no surface line; the material reads as gas rather than liquid

// Ambient floor under the light recovered from the field, NOT the light itself. A layer that
// should stay legible in an unlit cave raises it; a layer that should go dark with the room
// leaves it near zero.
float3 WorldLight;

// How far the local flow drags the noise lookup, in noise-space units. 0 leaves the pattern
// world-locked and never touches the attribute target, which is the default for every layer.
float FlowStrength;

// How much the recovered temperature pulls the palette toward the Hot* ramp. 0 ignores it. The rim
// heats toward HotShadowColor and the surface line toward HotCoreColor.
float TemperatureBlend;
float3 HotShadowColor;
float3 HotBodyColor;
float3 HotCoreColor;

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

float2 WorldUv(float2 uv)
{
    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
}

// Raw summed coverage at a cell. Everything else on both targets is weighted by this, so it is
// also the divisor that recovers them.
float Coverage(float2 uv)
{
    return tex2D(FieldSampler, uv).r;
}

// Field density at a (quantized) uv. RED is coverage, exactly as it always was.
float Density(float2 uv)
{
    float f = Coverage(uv) * FieldGain;
    return f * f;
}

// Coverage-weighted average velocity of the blobs on this cell, back in -1..1. The bias survives
// the averaging because the divide normalises by the same weight it was scaled by.
float2 FieldFlow(float2 uv, float coverage)
{
    float2 biased = tex2D(AttrSampler, uv).rg / max(coverage, 0.0001);

    return biased * 2.0 - 1.0;
}

// Coverage-weighted average temperature of the blobs on this cell, 0..1 against the layer's range.
float FieldTemperature(float2 uv, float coverage)
{
    return tex2D(FieldSampler, uv).b / max(coverage, 0.0001);
}

// Brightness of the world under the blobs covering this cell.
float3 FieldLight(float2 uv)
{
    float2 field = tex2D(FieldSampler, uv).rg;
    float brightness = field.g / max(field.r, 0.0001);

    return max(brightness.xxx, WorldLight);
}

float3 Ramp(float3 shadow, float3 body, float3 core, float shade)
{
    float3 color = lerp(shadow, body, saturate(shade * 2.0));
    return lerp(color, core, saturate(shade * 2.0 - 1.0));
}

float4 FieldPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    float2 p = WorldUv(cell) * NoiseScale;
    p = p * NoiseStretch + NoiseDrift * Time;

    p -= FieldFlow(cell, Coverage(cell)) * FlowStrength;

    float fbm = Fbm(p);

    float erosion = NoiseStrength * (fbm - 0.5);
    float d = Density(cell) - erosion;
    if (d < Threshold)
        discard;

    float3 light = FieldLight(cell);
    float heat = saturate(FieldTemperature(cell, Coverage(cell)) * TemperatureBlend);

    float2 tx = float2(FieldTexel.x, 0.0);
    float2 ty = float2(0.0, FieldTexel.y);

    float upD = Density(cell - ty) - erosion;

    if (HighlightAlpha > 0.0 && upD < Threshold)
        return float4(lerp(HighlightColor, HotCoreColor, heat) * light * HighlightAlpha, HighlightAlpha);

    float sideMin = min(Density(cell + ty), min(Density(cell - tx), Density(cell + tx))) - erosion;

    if (min(sideMin, upD) < Threshold)
        return float4(lerp(OutlineColor, HotShadowColor, heat) * light * OutlineAlpha, OutlineAlpha);

    float t = 1.0 - exp(-(d - Threshold) / max(DepthRange, 0.001));
    float shade = saturate(t * (0.55 + 0.9 * fbm));
    shade = floor(shade * Bands) / max(Bands - 1.0, 1.0);

    float3 color = lerp(
        Ramp(ShadowColor, BodyColor, CoreColor, shade),
        Ramp(HotShadowColor, HotBodyColor, HotCoreColor, shade),
        heat);

    float alpha = BodyAlpha * lerp(BodyAlphaFloor, 1.0, shade);
    return float4(color * light * alpha, alpha);
}

technique GasFbm
{
    pass P0
    {
        PixelShader = compile ps_3_0 FieldPass();
    }
}
