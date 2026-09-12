// Snaps coverage to fully on or fully off, so what gets drawn has no partial pixels left in it.
//
// Thresholding before the magnification is what turns a scaled font back into a bitmap font.
//
// Safe to run over a whole panel batch rather than the text alone. Everything else drawn here is
// MagicPixel at full alpha, which lands above any sensible threshold and passes through unchanged.
//
//   uThreshold  coverage at which a pixel counts as ink; 0.5 is the neutral choice, lower fattens

sampler2D ImageSampler : register(s0);

float uThreshold;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float coverage = tex2D(ImageSampler, uv).a;

    // The vertex colour is returned rather than the sampled texel because the atlas carries no
    // colour worth keeping.
    // A font page is white shaped by its alpha, and MagicPixel is white
    // full stop. Both want the tint they were asked for, at full strength or not at all.
    return col * step(uThreshold, coverage);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
