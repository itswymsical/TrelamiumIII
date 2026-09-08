// Whole-frame color grade for chart screen effects (ScreenFxRenderer).
// Blends the frame toward a tinted monochrome of itself — at full intensity
// the scene reads as a single-hue wash while keeping its luminance shape.
//
//   uIntensity  0..1 charter intensity; blend amount
//   uTintColor  grade color (computed from the HUE knob in C#)

sampler2D ImageSampler : register(s0);

float uIntensity;
float3 uTintColor;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(ImageSampler, uv);

    float luma = dot(c.rgb, float3(0.299, 0.587, 0.114));
    // Slight lift so saturated dark tints don't crush the frame.
    float3 graded = uTintColor * luma * 1.15;

    c.rgb = lerp(c.rgb, graded, uIntensity);
    return c * col;
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
