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

// Even hexagon; with the centre tap weighted double this reads as a soft disc rather than the
// ring-shaped bokeh a bare circle of taps would give.
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

    // The clear core is the point of the whole shader, and bailing on it also keeps the tap count
    // off the middle of the screen as well as the outside.
    if (rn >= uBandOuter || rn <= uBandInner)
        return bg;

    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 1.0);

    float t = saturate((rn - uBandInner) / max(uBandOuter - uBandInner, 1e-4));

    float s         = sin(PI * t);
    float thickness = s * s;
    float slope     = sin(TAU * t);

    float rd = rn - uRefract * slope;

    float2 basis = (dir * radius) / uAspect;

    // Chroma splits where the glass is bending, so the crest stays clean and the two lips fringe
    // -- the same places a real lens does it.
    float disp = uDispersion * abs(slope);

    // The bands sweep around the polar axis, which keeps the magnetic reading without drawing a
    // single hard line anywhere.
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

    // Normal of the torus surface: tilted radially by the slope, upright at the crest.
    float3 n = normalize(float3(dir * -slope, 1.0));

    float3 lightDir = normalize(float3(-0.45, -0.62, 0.64));
    float3 halfVec  = normalize(lightDir + float3(0.0, 0.0, 1.0));
    float  ndoth    = saturate(dot(n, halfVec));
    // Tight hotspot plus a broad sheen: one highlight alone reads as plastic.
    float  spec     = (pow(ndoth, 60.0) + pow(ndoth, 6.0) * 0.12) * uSpecular * thickness;

    // The glow is the grab-radius readout, so it rides the same cross-section as the glass. That
    // puts the brightest part of the ring at the thickest part of the lens rather than at an
    // unrelated radius, and takes it to zero at both lips with no edge to alias.
    float glow = pow(thickness, uGlowPower);

    float3 rimColor = lerp(uTint, float3(1.0, 1.0, 1.0), 0.6);

    float3 sheen = 0.5 + 0.5 * sin(float3(0.0, 2.1, 4.2) + t * 9.0 + uTime * 0.4);

    // How bright the world already is behind this pixel of glass.
    //
    // Everything below that adds light has to be scaled by this or the ring cannot hold together
    // across a frame with mixed lighting: a flat +0.05 is invisible against daylit sky and a
    // bright hoop against unlit stone, so the same ring reads as two different materials
    // depending on what it happens to be crossing.
    
    // sqrt rather than raw luminance because the raw value is too aggressive
    float lum      = dot(glass, float3(0.299, 0.587, 0.114));
    float response = lerp(1.0, sqrt(saturate(lum)), saturate(uLightResponse));

    // Tint multiplies rather than lerping toward a fixed colour. 
    float3 tintNorm = uTint / max(dot(uTint, float3(0.299, 0.587, 0.114)), 1e-3);
    glass *= lerp(float3(1.0, 1.0, 1.0), tintNorm, uTintStrength * thickness);

    // Every additive term is gated on thickness, so all of them vanish into the clear core
    // alongside the refraction rather than leaving a tint or a sheen behind on the player.
    glass += sheen * uIridescence * thickness * response;
    glass += rimColor * glow * uGlow * response;
    glass += spec * response;

    // No extra edge feather is needed: at both lips thickness, slope, disp and frost are all
    // zero, so `glass` has already resolved to bg by the time the band ends. uMix alone carries
    // the equip envelope, and at 0 this is an exact passthrough.
    return float4(lerp(bg.rgb, glass, saturate(uMix)), bg.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
