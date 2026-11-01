// Passes the frame through unchanged. An active scene filter is what makes vanilla render the frame into a
// target instead of straight to the backbuffer, and HighlandsBackdrop needs a target it can read back.
//
// COLOR0 is ignored on purpose: FilterManager.EndCapture draws the captured frame with
// Main.ColorOfTheSkies as the vertex colour whenever any filter is active.

sampler2D ImageSampler : register(s0);

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    return tex2D(ImageSampler, uv);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
