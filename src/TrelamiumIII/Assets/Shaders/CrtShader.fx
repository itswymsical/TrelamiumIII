//   uIntensity   0..1 charter intensity; blends curvature, lines, and grille
//   uCurve       barrel distortion amount (CURVE knob, mapped in C#)
//   uTime        seconds (flicker)
//   uResolution  frame size in pixels (scanline / grille frequency)

sampler2D ImageSampler : register(s0);

float  uIntensity;
float  uCurve;
float  uTime;
float2 uResolution;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Barrel curvature, eased in by intensity.
    float2 centered = uv - 0.5;
    float r2 = dot(centered, centered);
    float2 curved = 0.5 + centered * (1.0 + uCurve * r2);
    float2 cuv = lerp(uv, curved, uIntensity);

    // Outside the bent tube face -> black bezel.
    float inBounds = step(0.0, cuv.x) * step(cuv.x, 1.0)
                   * step(0.0, cuv.y) * step(cuv.y, 1.0);

    float4 c = tex2D(ImageSampler, cuv);

    // Scanlines: one dark band per output line pair.
    float scan = 0.5 + 0.5 * sin(cuv.y * uResolution.y * 3.14159);
    c.rgb *= lerp(1.0, 0.72 + 0.28 * scan, uIntensity);

    // RGB aperture grille: 3px phosphor stripes.
    float m = fmod(cuv.x * uResolution.x, 3.0);
    float3 grille = float3(
        m < 1.0 ? 1.0 : 0.72,
        m >= 1.0 && m < 2.0 ? 1.0 : 0.72,
        m >= 2.0 ? 1.0 : 0.72);
    c.rgb *= lerp(float3(1.0, 1.0, 1.0), grille, uIntensity * 0.6);

    // Brightness compensation for the masked light, plus a faint flicker.
    c.rgb *= 1.0 + 0.22 * uIntensity;
    c.rgb *= 1.0 + sin(uTime * 120.0) * 0.012 * uIntensity;

    // Soft edge falloff near the tube border.
    float2 edge = abs(cuv - 0.5) * 2.0;
    float edgeFade = 1.0 - smoothstep(0.92, 1.0, max(edge.x, edge.y)) * uIntensity * 0.8;

    c.rgb *= inBounds * edgeFade;
    return c * col;
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
