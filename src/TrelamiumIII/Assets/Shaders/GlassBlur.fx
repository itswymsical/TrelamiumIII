// Whole-frame frosted-glass blur for chart screen effects (ScreenFxRenderer).
// Separable 9-tap gaussian (same linear-sampled weights as VisBlur.fx), run
// horizontal then vertical. The vertical pass adds the iOS-glass finish:
// slight desaturation, brightness lift, and animated frost grain.
//
//   uTexelSize  1 / (width, height)
//   uDirection  (1,0) horizontal or (0,1) vertical (vertical applies the finish)
//   uIntensity  0..1 charter intensity; drives radius and frost strength
//   uFrost      frost knob (ChartEffect.Param * 2): grain amount, 1 = neutral
//   uTime       seconds (drifts the grain; vertical pass only)
//   uNoiseTex   tiling noise source (vertical pass only)

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

float2 uTexelSize;
float2 uDirection;
float  uIntensity;
float  uFrost;
float  uTime;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float radius = 1.0 + uIntensity * 6.0;
    float2 tap = uTexelSize * uDirection * radius;
    float2 o1 = tap * 1.3846153846;
    float2 o2 = tap * 3.2307692308;

    float4 sum = tex2D(ImageSampler, uv) * 0.2270270270;
    sum += (tex2D(ImageSampler, uv + o1) + tex2D(ImageSampler, uv - o1)) * 0.3162162162;
    sum += (tex2D(ImageSampler, uv + o2) + tex2D(ImageSampler, uv - o2)) * 0.0702702703;

    // Glass finish on the second (vertical) pass only.
    float finish = step(0.5, uDirection.y);

    float luma = dot(sum.rgb, float3(0.299, 0.587, 0.114));
    float grain = tex2D(NoiseSampler, uv * 24.0 + uTime * 0.05).r - 0.5;

    float3 glass = lerp(sum.rgb, float3(luma, luma, luma), 0.15 * uIntensity);
    glass = glass * (1.0 + 0.10 * uIntensity) + 0.03 * uIntensity;
    glass += grain * 0.04 * uIntensity * uFrost;

    sum.rgb = lerp(sum.rgb, glass, finish);
    return sum * col;
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
