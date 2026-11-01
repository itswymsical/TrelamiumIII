// Temperate highland air: a faint haze over whatever the light reaches. The mist itself is particles
// rising off the ground (HighlandsWeather), drawn through world light; this only softens the frame.
//
//   uFogColor, uFogDensity    haze tint and its blend at full intensity
//   uDrift                    world-pixel offset the wind has carried the haze, accumulated on the CPU
//                             so a change in wind speed never makes it jump
//
// uScreenPosition, uScreenResolution, uZoom and uIntensity are written by ScreenShaderData.Apply.
//
// The haze is weighted by the frame's own brightness, since a full-screen pass has no light map: fog
// with no light on it is invisible, so unlit terrain stays black instead of glowing through it.
//
// COLOR0 is ignored on purpose: FilterManager.EndCapture draws the captured frame with
// Main.ColorOfTheSkies as the vertex colour whenever any filter is active.

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

float uIntensity;
float2 uScreenPosition;
float2 uScreenResolution;
float2 uZoom;

float3 uFogColor;
float uFogDensity;
float2 uDrift;

// Brightness at which the haze reaches its full blend; anything darker gets proportionally less.
#define LIT_LUMA 0.45

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(ImageSampler, uv);

    // The capture is the zoomed frame, scaled about the centre of the screen.
    float2 world = uScreenPosition + uScreenResolution * 0.5 + (uv - 0.5) * uScreenResolution / uZoom;
    float broad = tex2D(NoiseSampler, (world + uDrift) * float2(0.0007, 0.0016)).r;

    float luma = dot(scene.rgb, float3(0.299, 0.587, 0.114));
    float lit = saturate(luma / LIT_LUMA);
    float haze = uFogDensity * (0.8 + 0.4 * broad) * lit;

    return float4(lerp(scene.rgb, uFogColor, saturate(haze * uIntensity)), scene.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
