// The screen half of the Lodestone Magnetizer's intake: one full-screen pass that reads the beam
// field target and wobbles the frame inside it.
//
//   uFieldTex   RGBA from MagnetizerBeam.fx. RGB is premultiplied emitted light, A is the mask
//   uNoiseTex   tiling noise for the wobble

//   uMix            0..1 effect weight; must resolve to an exact passthrough at 0
//   uWobble         peak displacement in UV, at full mask strength
//   uWobbleScale    FBM frequency
//   uWobbleDrift    uv/sec scroll of the FBM
//   uTime           seconds
//   uWorldOffset    screenPosition / screenHeight, to lock the FBM to the world
//   uAspect         screen w/h, so the FBM is not stretched
//   uGlow           gain on the field's emitted light
//   uLightResponse  0..1 how far the added light tracks the brightness behind it

sampler2D ImageSampler : register(s0);

texture2D uFieldTex;
sampler2D FieldSampler = sampler_state
{
    Texture = <uFieldTex>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
};

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float  uMix;

float  uWobble;
float  uWobbleScale;
float2 uWobbleDrift;
float  uTime;
float2 uWorldOffset;
float  uAspect;
float2 uZoom;

float  uGlow;
float  uLightResponse;

float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += tex2D(NoiseSampler, p).r * amp;
        p = p * 2.02 + 0.13;
        amp *= 0.5;
    }

    return sum;
}

float2 WorldUv(float2 uv)
{
    float2 aspect = float2(uAspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / uZoom + halfExtent + uWorldOffset;
}

float4 FieldPass(float2 uv : TEXCOORD0) : COLOR0
{
    float4 bg = tex2D(ImageSampler, uv);

    if (uMix <= 0.001)
        return bg;

    float4 field = tex2D(FieldSampler, uv);
    float strength = saturate(field.a);

    if (strength <= 0.002 && dot(field.rgb, 1.0) <= 0.002)
        return bg;

    float2 p = WorldUv(uv) * uWobbleScale + uWobbleDrift * uTime;

    float2 wobble = float2(Fbm(p) - 0.47, Fbm(p + 7.31) - 0.47);

    float3 warped = tex2D(ImageSampler, uv + wobble * uWobble * uZoom * strength).rgb;

    float lum = dot(warped, float3(0.299, 0.587, 0.114));
    float response = lerp(1.0, sqrt(saturate(lum)), saturate(uLightResponse));

    float3 lit = warped + field.rgb * uGlow * response;

    return float4(lerp(bg.rgb, lit, saturate(uMix)), bg.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 FieldPass();
    }
}
