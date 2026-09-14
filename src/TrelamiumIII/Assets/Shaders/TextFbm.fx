// Fills text glyphs with drifting FBM noise mapped through a three-colour ramp, continuous across glyphs.
//
// s0 is the font page. The text painter sets these per glyph so page UV maps back to text pixels:
//   uImageSize0     font page size, pixels
//   uSourceRect     glyph rectangle in the page, pixels (x, y, w, h)
//   uGlyphPosition  glyph top-left in text pixels, unscaled
//
//   uTime           seconds
//   uNoiseScale     noise UV per text pixel; lower gives larger features
//   uDrift          noise scroll, UV per second
//   uWarp           domain-warp strength, UV
//   uContrast       stretch around the FBM mean before the ramp
//   uBias           added before the stretch, to recentre a noise texture whose mean is not 0.5
//   uBands          posterisation steps; 0 keeps the ramp smooth
//   uShadowColor / uBodyColor / uHighlightColor   ramp stops, low to high

sampler2D GlyphSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float2 uImageSize0;
float4 uSourceRect;
float2 uGlyphPosition;

float uTime;
float uNoiseScale;
float2 uDrift;
float uWarp;
float uContrast;
float uBias;
float uBands;

float3 uShadowColor;
float3 uBodyColor;
float3 uHighlightColor;

float SampleN(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

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

    // The octave weights sum to 0.9375; dividing keeps the result centred on the texture's own mean.
    return sum / 0.9375;
}

float4 PixelShaderFunction(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float coverage = tex2D(GlyphSampler, uv).a;

    float2 textPixel = uGlyphPosition + (uv * uImageSize0 - uSourceRect.xy);
    float2 p = textPixel * uNoiseScale + uDrift * uTime;

    // Two decorrelated low-frequency taps bend whole billows rather than shredding them.
    float2 warp = float2(SampleN(p * 0.37 + uTime * 0.021), SampleN(p * 0.37 + float2(5.2, 1.3) - uTime * 0.017));
    p += (warp - 0.5) * uWarp;

    float n = saturate((Fbm(p) - 0.5 + uBias) * uContrast + 0.5);

    if (uBands > 0.5)
        n = saturate(floor(n * uBands) / max(uBands - 1.0, 1.0));

    float3 ramp = lerp(uShadowColor, uBodyColor, saturate(n * 2.0));
    ramp = lerp(ramp, uHighlightColor, saturate(n * 2.0 - 1.0));

    // Premultiplied: the vertex colour carries tint and alpha, the font page carries coverage.
    return float4(ramp * color.rgb, color.a) * coverage;
}

technique TextFbm
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
