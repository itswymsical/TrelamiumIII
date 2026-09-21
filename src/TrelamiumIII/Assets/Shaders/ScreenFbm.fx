// Full-screen FBM mist for realm transitions and loading screens, revealed through a noisy iris around uFocus.
// s0 alpha masks the result, so MagicPixel fills the rect. Output is premultiplied; the iris rim adds with alpha 0.
//
//   uTime          seconds
//   uAspect        (width/height, 1)
//   uFocus         iris and swirl centre, 0..1
//   uScale         base FBM frequency
//   uZoom          push toward uFocus; the field is enlarged by exp(uZoom)
//   uPivot         median of the raw FBM sum for the bound noise texture, see Fbm()
//   uExpand        histogram stretch applied after uPivot
//   uWarp          domain-warp strength
//   uSwirl         winding strength; the angle is uSwirl / (r + uSwirlCore)
//   uSwirlCore     softening on that divide
//   uSpin          rigid rotation rate, rad/sec
//   uDrift         field scroll, noise units/sec
//   uDensity       body coverage midpoint
//   uContrast      half-width of the body's feather band
//   uBands         ramp steps; 0 keeps the ramp continuous
//   uCoverage      0 clear, 1 fully covered
//   uInward        1 closes from the screen edge toward uFocus, 0 grows out from uFocus
//   uEdgeNoise     FBM displacement of the iris edge, in normalised radius
//   uEdgeSoft      feather on the iris edge
//   uEdgeWidth     width of the emissive band along the edge
//   uEdgeColor     that band's colour
//   uVignette      corner darkening, 0..1
//   uVoidColor / uLowColor / uHighColor   ramp stops, low to high
//   uGlowColor     added over the densest body, scaled by uGlow

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

float  uTime;
float2 uAspect;
float2 uFocus;
float  uScale;
float  uZoom;
float  uPivot;
float  uExpand;
float  uWarp;
float  uSwirl;
float  uSwirlCore;
float  uSpin;
float2 uDrift;
float  uDensity;
float  uContrast;
float  uBands;

float  uCoverage;
float  uInward;
float  uEdgeNoise;
float  uEdgeSoft;
float  uEdgeWidth;
float3 uEdgeColor;

float  uVignette;

float3 uVoidColor;
float3 uLowColor;
float3 uHighColor;
float3 uGlowColor;
float  uGlow;

float N(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

// Four octaves, recentred on the texture's median and stretched back out so uDensity and uContrast work as 0..1 dials.
float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += N(p) * amp;
        p = p * 2.03 + 0.17;
        amp *= 0.5;
    }

    return saturate((sum - uPivot) * uExpand + 0.5);
}

float Band(float v)
{
    return uBands > 0.5 ? floor(v * uBands + 0.5) / uBands : v;
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 d = (uv - uFocus) * uAspect;
    float r = length(d);

    float twist = uSwirl / (r + uSwirlCore) + uTime * uSpin;
    float ca = cos(twist);
    float sa = sin(twist);
    float2 q = float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);

    float2 p = q * uScale * exp(-uZoom) + uDrift * uTime;

    // Two decorrelated low-frequency taps bend whole billows rather than shredding them.
    float2 w = float2(N(p * 0.23 + uTime * 0.011), N(p * 0.23 - uTime * 0.009 + 4.19));
    p += (w - 0.5) * uWarp;

    float h = Fbm(p);
    float soft = max(uContrast, 0.001);
    float body = smoothstep(uDensity - soft, uDensity + soft, h);

    float3 color = lerp(uVoidColor, uLowColor, Band(saturate(h * 1.4)));
    color = lerp(color, uHighColor, Band(body));
    color += uGlowColor * (uGlow * Band(body * body * body));

    float vigR = length((uv - 0.5) * uAspect) / max(0.5 * length(uAspect), 1e-4);
    color *= 1.0 - uVignette * smoothstep(0.35, 1.0, vigR);

    // Radius normalised to the corner farthest from the focus, so coverage 1 reaches every pixel wherever the focus is.
    float2 far = max(uFocus, 1.0 - uFocus) * uAspect;
    float rn = r / max(length(far), 1e-4);
    float t = lerp(rn, 1.0 - rn, uInward);

    // The margin keeps coverage 0 fully clear and 1 fully covered once the edge is displaced and feathered.
    float margin = 0.5 * uEdgeNoise + uEdgeSoft + uEdgeWidth;
    float front = lerp(-margin, 1.0 + margin, uCoverage);
    float e = t + (h - 0.5) * uEdgeNoise;

    float mask = 1.0 - smoothstep(front - uEdgeSoft, front + uEdgeSoft, e);
    float rim = saturate(1.0 - abs(e - front) / max(uEdgeWidth, 1e-4));

    float source = col.a * tex2D(ImageSampler, uv).a;
    float alpha = mask * source;

    return float4(color * alpha + uEdgeColor * (rim * rim * source), alpha);
}

technique ScreenFbm
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
