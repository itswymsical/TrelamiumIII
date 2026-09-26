//   Threshold      coverage (after erosion) at which the mark exists at all
//   NoiseStrength  how hard the FBM eats into it; ~2x Threshold gives holes through the interior
//   NoiseScale     FBM frequency; NoiseDrift scrolls it, WorldOffset locks it to the world
//   EdgeWidth      depth past the threshold over which edge fades into body
//   Body/EdgeColor the two ends of that ramp; only the body takes WorldLight, so the edge glows

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

float3 BodyColor;
float3 EdgeColor;
float BodyAlpha;
float EdgeAlpha;
float3 WorldLight;

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

// Screen uv -> world position, in units of screen heights. See GasFbm.fx for why the zoom has to
// be undone here: without it the noise carries a (1 - Zoom) * cameraCentre error that moves with
// the camera, so the char pattern slides across the terrain whenever the player pans while zoomed.
float2 WorldUv(float2 uv)
{
    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
}

float4 MaskPass(float2 uv : TEXCOORD0) : COLOR0
{
    // Snap to the field-texel grid, so every decision below happens once per field texel and the
    // result lands on the same chunky pixel grid the rest of the mod's effects use.
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    float coverage = tex2D(FieldSampler, cell).a;

    if (coverage <= 0.004)
        discard;

    // World-locked and drifting: the char boils in place while the camera pans across it, instead
    // of swimming with the screen.
    float2 p = WorldUv(cell) * NoiseScale + NoiseDrift * Time;
    float fbm = Fbm(p);

    // Centred on zero so the erosion digs and bulges either way, which keeps Threshold meaning
    // "coverage level" rather than drifting with NoiseStrength.
    float d = coverage - NoiseStrength * (fbm - 0.5);

    if (d < Threshold)
        discard;

    float depth = saturate((d - Threshold) / max(EdgeWidth, 0.001));

    float3 color = lerp(EdgeColor, BodyColor, depth);
    float alpha = lerp(EdgeAlpha, BodyAlpha, depth);

    // The body is terrain and takes the room's light; the edge is the condition's own emission and
    // does not, so embers stay lit in an unlit cave.
    color *= lerp(float3(1.0, 1.0, 1.0), WorldLight, depth);

    return float4(color * alpha, alpha);
}

technique TileConditionMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 MaskPass();
    }
}
