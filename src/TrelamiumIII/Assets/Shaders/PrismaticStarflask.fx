sampler2D FieldSampler : register(s0);

float Threshold = 0.30;
float EdgeWidth = 0.22;
float Time;
float2 WorldOffset;

// Cloudy body (dust)
float3 DustTint = float3(0.92, 0.88, 1.00);
float DustBrightness = 0.45;
float CoreBloom = 0.5;

// Wispy edge breakup (procedural value noise)
float WispScale = 4.0;
float WispSpeed = 0.06;
float WispStrength = 0.40;

// Cell-based stars
float StarCellScale = 18.0;
float StarSize = 0.045;
float StarBrightness = 2.6;
float StarTwinkleSpeed = 1.2;
float StarSaturation = 1.0;
float StarSparkleLength = 1.4; // cross-streak length multiplier

// Rim
float RimWidth = 0.06;
float RimIntensity = 0.35;
float3 RimColor = float3(0.85, 0.78, 1.0);

float3 hsv2rgb(float3 hsv)
{
    float3 p = abs(frac(hsv.xxx + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return hsv.z * lerp(float3(1, 1, 1), saturate(p - 1.0), hsv.y);
}

float hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + float2(1, 0));
    float c = hash21(i + float2(0, 1));
    float d = hash21(i + float2(1, 1));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float4 PrismaticPass(float2 uv : TEXCOORD0) : COLOR0
{
    float field = tex2D(FieldSampler, uv).r;
    float curvedField = pow(saturate(field), 1.3);
    float2 worldUV = uv + WorldOffset;

    // Modulate the field with low-frequency noise so the silhouette goes ragged
    float wisp = valueNoise(worldUV * WispScale + Time * WispSpeed);
    float wispField = curvedField * lerp(1.0 - WispStrength, 1.0 + WispStrength * 0.3, wisp);

    float surface = smoothstep(Threshold - EdgeWidth, Threshold + EdgeWidth, wispField);
    if (surface <= 0.001)
        discard;

    float t = saturate((wispField - Threshold) / 0.5);

    // --- Body: desaturated dust with a soft inner bloom ---
    float3 color = DustTint * DustBrightness * lerp(0.5, 1.0, t);

    float bloom = pow(saturate(curvedField * 1.8), 5.0);
    color = lerp(color, float3(1.0, 0.97, 1.0), bloom * CoreBloom);

    // --- Stars: each cell owns one star with its own hue, phase, brightness ---
    float2 sUV = worldUV * StarCellScale;
    float2 cellId = floor(sUV);
    float2 cellUV = frac(sUV);

    float starHue = hash21(cellId + 0.13);
    float twinklePhase = hash21(cellId + 0.71) * 6.2831;
    float presence = hash21(cellId + 0.37);
    float2 starOffset = float2(hash21(cellId + 0.91), hash21(cellId + 1.27));

    float2 starPos = 0.2 + starOffset * 0.6;
    float2 toStar = cellUV - starPos;
    float dist = length(toStar);

    // Round soft core
    float coreFalloff = 1.0 - smoothstep(0.0, StarSize, dist);
    coreFalloff = pow(coreFalloff, 2.0);

    // 4-point cross gleam (exponential falloff perpendicular to each axis)
    float crossH = exp(-abs(toStar.y) / (StarSize * 0.15)) * exp(-abs(toStar.x) / (StarSize * StarSparkleLength));
    float crossV = exp(-abs(toStar.x) / (StarSize * 0.15)) * exp(-abs(toStar.y) / (StarSize * StarSparkleLength));
    float gleam = max(crossH, crossV);

    // Twinkle  per-star phase, raised to a power so most stars sit dim and a few burn
    float twinkle = 0.5 + 0.5 * sin(Time * StarTwinkleSpeed + twinklePhase);
    twinkle = pow(twinkle, 3.0);

    // Bias presence so most cells are dim, a minority are bright
    float starWeight = pow(presence, 2.5) * twinkle * t;

    float3 starRGB = hsv2rgb(float3(starHue, StarSaturation, 1.0));
    color += starRGB * (coreFalloff + gleam * 0.6) * starWeight * StarBrightness;

    // --- Rim ---
    float rimMask = 1.0 - smoothstep(0.0, RimWidth, abs(wispField - Threshold));
    color = lerp(color, RimColor, rimMask * RimIntensity);

    float alpha = surface * lerp(0.18, 0.50, t);
    return float4(color * alpha, alpha);
}

technique PrismaticStarflask
{
    pass P0
    {
        PixelShader = compile ps_3_0 PrismaticPass();
    }
}