// The highlands' distance: a gradient of blur and haze over the sky and surface background layers.
//
// Drawn between the landscape and the world (HighlandsBackdrop), so walls, tiles and everything standing
// on them are drawn crisp over the result and the haze never goes under a tile. It is thickest at the
// horizon and thins up the sky, and stops at the bottom of the surface layer, under which nothing is sky.
//
//   uTexel        one pixel, in uv
//   uHorizon      uv row of the realm's base ground line; off screen once the player has climbed away from it
//   uFalloff      uv distance above the horizon over which haze and blur thin to nothing
//   uSurface      uv row of the bottom of the surface layer
//   uSurfaceFade  uv distance over which they fade out approaching it
//   uHazeColor    what the distance fades toward
//   uHaze         peak blend, at the horizon
//   uBlur         peak blur radius in pixels, at the horizon

sampler2D ImageSampler : register(s0);

float2 uTexel;
float uHorizon;
float uFalloff;
float uSurface;
float uSurfaceFade;
float3 uHazeColor;
float uHaze;
float uBlur;

#define TAPS 16

// A golden-angle spiral on the unit disc: even coverage with no lines for the eye to catch.
static const float2 kTaps[TAPS] =
{
    float2(0.177, 0.000), float2(-0.181, 0.166), float2(0.031, -0.323), float2(0.262, 0.293),
    float2(-0.491, -0.054), float2(0.413, -0.300), float2(-0.128, 0.569), float2(-0.333, -0.513),
    float2(0.653, 0.126), float2(-0.587, 0.378), float2(0.200, -0.713), float2(0.361, 0.690),
    float2(-0.777, -0.215), float2(0.800, -0.308), float2(-0.361, 0.815), float2(-0.246, -0.936)
};

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float distance = saturate(1.0 - (uHorizon - uv.y) / uFalloff);
    distance *= 1.0 - saturate((uv.y - (uSurface - uSurfaceFade)) / max(uSurfaceFade, 0.001));
    distance = distance * distance * (3.0 - 2.0 * distance);

    float2 reach = uTexel * uBlur * distance;
    float3 sum = tex2D(ImageSampler, uv).rgb;

    [unroll]
    for (int i = 0; i < TAPS; i++)
        sum += tex2D(ImageSampler, uv + kTaps[i] * reach).rgb;

    float3 blurred = sum / (TAPS + 1.0);

    return float4(lerp(blurred, uHazeColor, saturate(uHaze * distance)), 1.0);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
