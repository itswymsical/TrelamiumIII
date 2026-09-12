// Whole-frame UV warp for screen effects
//   uTime       seconds (scrolls the noise field)
//   uIntensity  0..1 charter intensity; max displacement ~2.5% of screen
//   uScale      warp noise frequency; 3 is neutral, higher is finer and more nauseating
//   uNoiseTex

sampler2D ImageSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float uTime;
float uIntensity;
float uScale;

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

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = uv * uScale;
    float wx = Fbm(p + float2(0.10, 0.07) * uTime);
    float wy = Fbm(p + float2(-0.08, 0.12) * uTime + 7.31);

    float2 warp = (float2(wx, wy) - 0.47) * uIntensity * 0.05;
    return tex2D(ImageSampler, uv + warp);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
