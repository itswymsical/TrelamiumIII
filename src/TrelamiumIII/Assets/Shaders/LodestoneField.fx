sampler2D ImageSampler : register(s0);

float2 uCenter;
float  uRadius;
float2 uAspect;
float  uMix;

float  uBandInner;
float  uBandOuter;
float  uRefract;
float  uDispersion;

float  uFrost;
float  uFieldLines;
float  uFieldFrost;
float  uTime;

float  uGlow;
float  uGlowPower;
float  uLightResponse;

float  uSpecular;
float  uIridescence;
float3 uTint;
float  uTintStrength;

static const float PI  = 3.14159265;
static const float TAU = 6.28318531;

static const float2 kTaps[6] =
{
    float2( 1.000,  0.000),
    float2( 0.500,  0.866),
    float2(-0.500,  0.866),
    float2(-1.000,  0.000),
    float2(-0.500, -0.866),
    float2( 0.500, -0.866)
};

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 bg = tex2D(ImageSampler, uv);

    float2 d      = (uv - uCenter) * uAspect;
    float  dist   = length(d);
    float  radius = max(uRadius, 1e-4);
    float  rn     = dist / radius;

    if (rn >= uBandOuter || rn <= uBandInner)
        return bg;

    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 1.0);

    float t = saturate((rn - uBandInner) / max(uBandOuter - uBandInner, 1e-4));

    float s         = sin(PI * t);
    float thickness = s * s;
    float slope     = sin(TAU * t);

    float rd = rn - uRefract * slope;

    float2 basis = (dir * radius) / uAspect;

    float disp = uDispersion * abs(slope);

    float theta = atan2(d.x, -d.y);
    float lines = 0.5 + 0.5 * sin(theta * uFieldLines + uTime);
    float frost = uFrost * thickness * lerp(1.0, lines, uFieldFrost);
    float2 blur = (frost * radius) / uAspect;

    float2 pR = uCenter + basis * (rd + disp);
    float2 pG = uCenter + basis * rd;
    float2 pB = uCenter + basis * (rd - disp);

    float3 glass = float3(tex2D(ImageSampler, pR).r,
                          tex2D(ImageSampler, pG).g,
                          tex2D(ImageSampler, pB).b) * 2.0;

    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float2 o = kTaps[i] * blur;
        glass.r += tex2D(ImageSampler, pR + o).r;
        glass.g += tex2D(ImageSampler, pG + o).g;
        glass.b += tex2D(ImageSampler, pB + o).b;
    }
    glass /= 8.0;

    float3 n = normalize(float3(dir * -slope, 1.0));

    float3 lightDir = normalize(float3(-0.45, -0.62, 0.64));
    float3 halfVec  = normalize(lightDir + float3(0.0, 0.0, 1.0));
    float  ndoth    = saturate(dot(n, halfVec));
    
    float  spec     = (pow(ndoth, 60.0) + pow(ndoth, 6.0) * 0.12) * uSpecular * thickness;

    float glow = pow(thickness, uGlowPower);

    float3 rimColor = lerp(uTint, float3(1.0, 1.0, 1.0), 0.6);

    float3 sheen = 0.5 + 0.5 * sin(float3(0.0, 2.1, 4.2) + t * 9.0 + uTime * 0.4);

    float lum      = dot(glass, float3(0.299, 0.587, 0.114));
    float response = lerp(1.0, sqrt(saturate(lum)), saturate(uLightResponse));

    float3 tintNorm = uTint / max(dot(uTint, float3(0.299, 0.587, 0.114)), 1e-3);
    glass *= lerp(float3(1.0, 1.0, 1.0), tintNorm, uTintStrength * thickness);

    glass += sheen * uIridescence * thickness * response;
    glass += rimColor * glow * uGlow * response;
    glass += spec * response;

    return float4(lerp(bg.rgb, glass, saturate(uMix)), bg.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}