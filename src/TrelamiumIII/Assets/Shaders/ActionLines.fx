//   uTime       seconds (scrolls the rumble and the line flicker)
//   uIntensity  0..1 charter intensity; drives warp amplitude and line opacity
//   uDensity    line count band (whole number so the angular seam wraps clean)
//   uNoiseTex   tiling noise source (AssetLibrary.NoiseTexture)

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
float uDensity;

#define INV_TWO_PI 0.15915494

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 dir = uv - 0.5;
    float dist = length(dir) * 2.0;                  // 0 center -> ~1.4 corners
    float angU = atan2(dir.y, dir.x) * INV_TWO_PI + 0.5;
    float2 rdir = dir / max(dist * 0.5, 0.0001);     // safe normalize

    // Radial roar warp: per-angle rumble, strongest at the edges.
    float rumble = tex2D(NoiseSampler, float2(angU * 2.0, uTime * 0.35)).r - 0.5;
    float warpMask = smoothstep(0.15, 0.9, dist);
    float2 warped = uv - rdir * rumble * warpMask * uIntensity * 0.035;

    float4 c = tex2D(ImageSampler, warped);

    // Speed lines: angular streaks (constant along the radius), flickering
    // over time, masked to leave the center readable.
    float n = tex2D(NoiseSampler, float2(angU * uDensity, uTime * 0.6)).r;
    float lines = smoothstep(0.62, 0.95, n);
    float lineMask = smoothstep(0.35, 1.1, dist);
    c.rgb += lines * lineMask * uIntensity * 1.2;

    return c * col;
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
