sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
float3 uColor;
float3 uSecondaryColor;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;
float2 uDirection;
float uOpacity;
float uTime;
float uIntensity;
float uProgress;
float2 uImageSize1;
float2 uImageSize2;
float2 uImageSize3;
float2 uImageOffset;
float uSaturation;
float4 uSourceRect;
float2 uZoom;

float4 PixelShaderFunction(float2 uv : TEXCOORD0) : COLOR0
{
    float4 tileColour = tex2D(uImage0, uv);

    if (tileColour.a <= 0.01)
        return float4(0, 0, 0, 0);

    float2 noiseUV1 = uv * 2.5 + float2(uTime * 0.18, uTime * 0.09);
    float2 noiseUV2 = uv * 1.8 - float2(uTime * 0.11, uTime * 0.22);

    float noise1 = tex2D(uImage1, noiseUV1).r;
    float noise2 = tex2D(uImage1, noiseUV2).r;
    float noise  = (noise1 + noise2) * 0.5;


    float pulse = sin(uTime * 1.8) * 0.5 + 0.5;

    float2 centered = abs(uv - 0.5) * 2.0;
    float rim       = pow(max(centered.x, centered.y), 2.5);

    float shieldIntensity = (noise * 0.6 + rim * 0.4) * (pulse * 0.35 + 0.65);

    float3 shieldColor = lerp(uColor, uSecondaryColor, noise);
    float4 shield      = float4(shieldColor * shieldIntensity, shieldIntensity * uOpacity);

    return tileColour + shield * 0.7;
}

technique Technique1
{
    pass StructureShieldPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
