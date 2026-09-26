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
float2 FieldTexel;      // 1 / field-target size; one texel = PixelScale screen px
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h
float2 Zoom;            // Main.GameViewMatrix.Zoom

float Threshold;
float NoiseScale;
float NoiseStrength;
float2 NoiseDrift;
float EdgeWidth;

// Blackbody ramp, coolest to hottest. The fourth is the flash at the very centre of a raging tile.
float3 EmberColor;
float3 CoolColor;
float3 HotColor;
float3 FlashColor;

float BodyAlpha;
float EdgeAlpha;

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

float3 HeatRamp(float heat)
{
    float3 low = lerp(EmberColor, CoolColor, saturate(heat / 0.45));
    float3 mid = lerp(CoolColor, HotColor, saturate((heat - 0.45) / 0.37));
    float3 high = lerp(HotColor, FlashColor, saturate((heat - 0.82) / 0.18));

    float3 color = (heat < 0.45) ? low : mid;

    return (heat < 0.82) ? color : high;
}

float4 BurnPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    float4 field = tex2D(FieldSampler, cell);
    float coverage = field.a;

    if (coverage <= 0.004)
        discard;

    float heat = saturate(field.r / max(coverage, 0.0039));

    float2 p = WorldUv(cell) * NoiseScale + NoiseDrift * Time;
    float fbm = Fbm(p);

    float d = coverage - NoiseStrength * (fbm - 0.5);

    if (d < Threshold)
        discard;

    float depth = saturate((d - Threshold) / max(EdgeWidth, 0.001));

    float rimHeat = saturate(heat + (1.0 - depth) * 0.35);

    float3 scorch = HeatRamp(rimHeat);

    float alpha = lerp(EdgeAlpha, BodyAlpha, depth);

    return float4(scorch * alpha, alpha);
}

technique TileBurnMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 BurnPass();
    }
}
