//   uTime        seconds
//   uSpeed       curtain drift speed
//   uScale       base march scale (the reference's `scale`)
//   uHeight      march extent (the reference's `height`) — how deep the sky goes
//   uBrightness  overall gain
//   uReact       0..1 audio boost added to brightness this frame
//   uHorizon     0..1 UV row the horizon sits on (curtains rise above it)
//   uSpread      view spread — lower is a longer lens, curtains stretch taller
//   uColorLow    gradient at the near end of the march
//   uColorMid    gradient midpoint
//   uColorHigh   gradient at the far end
//   uResolution  pixel size (aspect correction)

sampler2D SceneSampler : register(s0);

float  uTime;
float  uSpeed;
float  uScale;
float  uHeight;
float  uBrightness;
float  uReact;
float  uHorizon;
float  uSpread;
float3 uColorLow;
float3 uColorMid;
float3 uColorHigh;
float2 uResolution;

#define STEPS 16

// GLSL mat2(vec2(c,s), vec2(-s,c)) is column-major; the row-major HLSL matrix with
// the same literal order, used as mul(M, v), gives the identical rotation.
float2x2 mm2(float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2x2(c, s, -s, c);
}

float tri(float x)
{
    return clamp(abs(frac(x) - 0.5), 0.01, 0.49);
}

float2 tri2(float2 p)
{
    return float2(tri(p.x) + tri(p.y), tri(p.y + tri(p.x)));
}

// Triangle-wave fractal with per-octave domain warp
float trinoise2d(float2 p, float t)
{
    float z = 1.8;
    float z2 = 2.5;
    float rz = 0.0;

    p = mul(mm2(p.x * 0.06), p);
    float2 bp = p;
    float2x2 rot = mm2(t);

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float2 dg = tri2(bp * 1.85) * 0.75;
        dg = mul(rot, dg);
        p -= dg / z2;

        bp *= 1.3;
        z2 *= 0.45;
        z *= 0.42;

        p *= 1.21 + (rz - 1.0) * 0.02;
        rz += tri(p.x + tri(p.y)) * z;

        p = mul(float2x2(-0.95534, -0.29552, 0.29552, -0.95534), p);
    }

    // max() guards pow() against a zero base on the first pixel of a fresh frame.
    return clamp(1.0 / pow(max(rz * 29.0, 1e-4), 1.3), 0.0, 0.55);
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 scene = tex2D(SceneSampler, uv);

    // Rebuild a view ray. SpriteBatch puts v = 0 at the TOP, so "up into the sky" is
    // -v; negating it gives a ray with y > 0 above the horizon, matching the original.
    float2 p = uv - float2(0.5, uHorizon);
    p.x *= uResolution.x / max(uResolution.y, 1.0);
    float3 dir = normalize(float3(p.x * uSpread, -p.y * uSpread, 1.0));

    // Below the horizon there is nothing to accumulate. -0.02 rather than 0 so the
    // curtains don't end in a hard line where they meet the ground.
    if (dir.y < -0.02) return scene;

    // Depth jitter, hashed on screen position. The reference's smooth hash of dir.xz is
    // near-constant along an iso-line, which at STEPS this low prints the march bands
    // straight onto the frame as concentric arcs. A white-noise hash trades those arcs
    // for fine grain, which is far less objectionable on a soft additive glow.
    float jitter = frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
    float stepScale = uHeight / STEPS;

    float t = uTime * uSpeed;
    float4 acc = 0.0;
    float4 avg = 0.0;

    [unroll]
    for (int i = 0; i < STEPS; i++)
    {
        // max() keeps pow's base non-negative
        // ds can only go negative if Depth is
        // driven below zero by an automation, and a NaN there would kill the frame.
        float ds = max(((float)i + jitter) * stepScale, 0.0);
        float depth = (uScale + pow(ds, 1.4) * 0.002) / (dir.y * 2.0 + 0.4);
        float3 pos = depth * dir;

        float n = trinoise2d(pos.zx, t);

        // exp2 falloff thins the far curtains; the smoothstep hides the near clip.
        float w = exp2(-ds * 0.065 - 2.5) * smoothstep(0.0, 5.0, ds);

        float g = saturate(ds / max(uHeight * 0.45, 1e-4));
        float3 grad = g < 0.5 ? lerp(uColorLow, uColorMid, g * 2.0)
                              : lerp(uColorMid, uColorHigh, (g - 0.5) * 2.0);

        float4 c = float4(grad * n, n);
        avg = lerp(avg, c, 0.5);
        acc += avg * w * stepScale;
    }

    // Fade out as the ray flattens toward the horizon.
    acc *= saturate(dir.y * 15.0 + 0.4);

    float3 outc = acc.rgb * (uBrightness + uReact);
    return float4(scene.rgb + outc, scene.a);   // additive, composited in-shader
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
