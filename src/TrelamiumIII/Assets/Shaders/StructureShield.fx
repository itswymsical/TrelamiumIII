sampler uImage1 : register(s1);
float3 uColor;
float3 uSecondaryColor;
float uOpacity;
float uTime;

float uScale;

float4 PixelShaderFunction(float2 uv : TEXCOORD0) : COLOR0
{
    // Apply uScale to the base UVs before calculating the distinct noise layers
    float2 scaledUV = uv * uScale;
    
    // Two noise samples scrolling in different directions
    float2 noiseUV1 = scaledUV * 2.5 + float2(uTime * 0.18, uTime * 0.09);
    float2 noiseUV2 = scaledUV * 1.8 - float2(uTime * 0.11, uTime * 0.22);

    float noise1 = tex2D(uImage1, noiseUV1).r;
    float noise2 = tex2D(uImage1, noiseUV2).r;
    float noise = (noise1 + noise2) * 0.5;

    // Slow global pulse
    float pulse = sin(uTime * 1.8) * 0.5 + 0.5;

    // Edge rim brighter at tile boundary
    // NOTE: We use the original unscaled 'uv' here so the rim stays locked to the tile edges.
    float2 centered = abs(uv - 0.5) * 2.0;
    float rim = pow(max(centered.x, centered.y), 2.5);

    float intensity = (noise * 0.6 + rim * 0.4) * (pulse * 0.35 + 0.65);

    float3 color = lerp(uColor, uSecondaryColor, noise);

    // Output only the noise overlay, no tile texture.
    // Drawn additively in PostDraw so it sits on top of the
    // already-lit tile without touching vanilla's lighting pass.
    return float4(color * intensity * uOpacity, intensity * uOpacity);
}

technique Technique1
{
    pass StructureShieldPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
