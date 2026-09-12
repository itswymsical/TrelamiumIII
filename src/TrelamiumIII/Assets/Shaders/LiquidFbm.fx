//   NoiseStretch    anisotropic FBM: (1, <1) elongates features vertically so
//                   the body streaks like runs and drips instead of boiling
//   HighlightColor  cells whose UPWARD neighbor falls outside the cut are the
//                   liquid's surface and draw a bright top line (every vanilla
//                   liquid has one); side/bottom edges keep the ink outline

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

float Time;
float2 FieldTexel;      // 1 / field-target size; one texel = 2 screen px
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h
float2 Zoom;            // Main.GameViewMatrix.Zoom

float Threshold;
float NoiseScale;
float NoiseStrength;
float2 NoiseDrift;      // uv/sec scroll; liquids usually drift downward
float2 NoiseStretch;    // per-axis frequency; (1, 0.45) = tall streaky features

float Bands;
float BodyAlpha;

float3 ShadowColor;     // thin film / sparse edges
float3 BodyColor;
float3 CoreColor;       // deep pooled center

float3 OutlineColor;
float OutlineAlpha;

float3 HighlightColor;  // the bright surface line on upward-facing edges
float HighlightAlpha;

// Ambient floor under the light recovered from the field, NOT the light itself. See GasFbm.fx.
float3 WorldLight;

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
// be undone here: without it the noise carries a (1 - Zoom) * cameraCentre error that moves with
// the camera, so the pattern slides across the world whenever the player pans while zoomed.
float2 WorldUv(float2 uv)
{
    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
}

float Density(float2 uv)
{
    float f = tex2D(FieldSampler, uv).r;
    return f * f;
}

float3 FieldLight(float2 uv)
{
    float2 field = tex2D(FieldSampler, uv).rg;
    float brightness = field.g / max(field.r, 0.0001);

    return max(brightness.xxx, WorldLight);
}

float4 LiquidPass(float2 uv : TEXCOORD0) : COLOR0
{
    // Snap to the field-texel grid
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    // World-locked FBM, stretched so the pattern runs vertically like a drip.
    float2 p = WorldUv(cell) * NoiseScale;
    p = p * NoiseStretch + NoiseDrift * Time;
    float fbm = Fbm(p);

    float erosion = NoiseStrength * (fbm - 0.5);
    float d = Density(cell) - erosion;
    if (d < Threshold)
        discard;

    // The light the blobs on THIS cell are standing in, not one value for the whole screen.
    float3 light = FieldLight(cell);

    // One-texel neighbor tests against the same erosion. Up-facing edge is the
    // liquid surface (bright line); everything else edged gets the ink outline.
    float2 tx = float2(FieldTexel.x, 0.0);
    float2 ty = float2(0.0, FieldTexel.y);

    float upD = Density(cell - ty) - erosion;
    if (upD < Threshold)
        return float4(HighlightColor * light * HighlightAlpha, HighlightAlpha);

    float sideMin = min(Density(cell + ty), min(Density(cell - tx), Density(cell + tx))) - erosion;
    if (sideMin < Threshold)
        return float4(OutlineColor * light * OutlineAlpha, OutlineAlpha);

    // Interior: depth-past-threshold shade, textured by the FBM, posterized.
    float t = saturate((d - Threshold) / max(1.0 - Threshold, 0.001));
    float shade = saturate(t * (0.55 + 0.9 * fbm));
    shade = floor(shade * Bands) / max(Bands - 1.0, 1.0);

    float3 color = lerp(ShadowColor, BodyColor, saturate(shade * 2.0));
    color = lerp(color, CoreColor, saturate(shade * 2.0 - 1.0));

    float alpha = BodyAlpha * lerp(0.8, 1.0, shade);
    return float4(color * light * alpha, alpha);
}

technique LiquidFbm
{
    pass P0
    {
        PixelShader = compile ps_3_0 LiquidPass();
    }
}
