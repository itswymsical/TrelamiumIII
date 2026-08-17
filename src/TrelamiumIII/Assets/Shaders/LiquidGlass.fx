//   uScreenTex  the blurred backdrop (full-screen frost)
//   uSrcRect    panel region in backdrop UV (x, y, w, h)
//   uAspect     panel half-extents, longest side = 1 (rounded-rect SDF)
//   uRadius     corner radius in the same units as uAspect
//   uColor      glass tint (multiplies the backdrop)
//   uBrightness / uBend / uChromatic / uGrain / uCurve / uRim / uTime  look knobs

texture2D uScreenTex;
sampler2D Frost = sampler_state
{
    Texture = <uScreenTex>;
    AddressU = Clamp; AddressV = Clamp;
    MinFilter = Linear; MagFilter = Linear;
};

float4 uSrcRect;
float2 uAspect;
float  uRadius;
float3 uColor;
float  uBrightness;
float  uBend;
float  uChromatic;
float  uGrain;
float  uCurve;
float  uRim;
float  uOilyness;   // iridescent oily film (0..2)
float  uTime;

float hash(float2 p) { return frac(sin(dot(p, float2(9.82131, 58.234))) * 45312.1324); }

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // Rounded-rect panel shape / SDF
    float2 pc = (uv - 0.5) * 2.0 * uAspect;                       // -aspect..aspect
    float2 q = abs(pc) - (uAspect - uRadius);
    float sdf = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - uRadius;   // < 0 inside
    float aa = fwidth(sdf) + 1e-4;
    float mask = saturate(0.5 - sdf / aa);                       // 1 inside -> 0 (AA rim)
    if (mask <= 0.0) return 0.0;

    float inside = saturate(-sdf / max(uRadius, 1e-3));          // 1 a radius deep, 0 at rim
    float rim = 1.0 - inside;                                    // strongest at the rim

    // Refraction: bend the sample outward near the rim, with a chromatic split.
    float2 dir = length(pc) > 1e-4 ? normalize(pc) : float2(0.0, 0.0);
    float2 warp = dir * rim * rim * uBend;
    float2 baseUV = uSrcRect.xy + uv * uSrcRect.zw + warp * uSrcRect.zw * 0.06;
    float2 chroma = dir * rim * uChromatic * 0.02 * uSrcRect.zw;

    float3 screen;
    screen.r = tex2D(Frost, baseUV + chroma).r;
    screen.g = tex2D(Frost, baseUV).g;
    screen.b = tex2D(Frost, baseUV - chroma).b;

    // Animated grain.
    float grain = hash(uv + uTime * 0.03) * 2.0 - 1.0;
    screen *= 1.0 + grain * uGrain;

    // Edge lights: a soft band hugging the rounded edge + a brighter rim.
    float curve = sin(saturate(rim) * 3.14159265) * uCurve;
    float rimLight = pow(rim, 3.0) * uRim;

    float3 outc = screen * uColor + uBrightness + curve + rimLight;

    // Oily iridescent film (from the frosted-glass spatial shader): a sinusoidal sheen
    // off a domed pseudo-normal, biased to R/G so it reads faintly blue/violet.
    float2 nrm = (uv - 0.5) * 2.0;
    outc.rg -= (sin(nrm * 10.0) * 0.5 + 0.5) * (0.1 * uOilyness);

    outc = saturate(outc);
    return float4(outc * mask, mask);                            // premultiplied
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
