//   Threshold      field value where gas exists (after noise erosion) || WARNING: a high threshold will make the noise cover the screen
//   NoiseStrength  how hard the FBM eats the field edge (0 = clean metaballs) || WARNING: a high threshold will make the noise cover the screen
//   NoiseScale     FBM frequency; Drift scrolls it, WorldOffset locks it to world || lower = bigger, higher = noisier
//   Bands          this determines how smooth/how much AA is applied to the nois || anything under 8 looks very sharp so i tend to stay above that
//   Shadow/Body/CoreColor   yur colors
//   OutlineColor/OutlineAlpha  the 2px rim

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
float2 FieldTexel;      // 1 / field-target size; one texel = 2 screen px
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h
float2 Zoom;            // Main.GameViewMatrix.Zoom

float Threshold;
float NoiseScale;
float NoiseStrength;
float2 NoiseDrift;      // uv/sec scroll of the FBM field

float Bands;
float BodyAlpha;

float3 ShadowColor;
float3 BodyColor;
float3 CoreColor;

float3 OutlineColor;
float OutlineAlpha;

// Ambient floor under the light recovered from the field, NOT the light itself. A layer that
// should stay legible in an unlit cave raises it; a layer that should go dark with the room
// leaves it near zero.
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

// Screen uv -> world position, in units of screen heights.

float2 WorldUv(float2 uv)
{
    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
}

// Field density at a (quantized) uv. RED is coverage, exactly as it always was.
float Density(float2 uv)
{
    float f = tex2D(FieldSampler, uv).r;
    return f * f;
}

// Brightness of the world under the blobs covering this cell.

float3 FieldLight(float2 uv)
{
    float2 field = tex2D(FieldSampler, uv).rg;
    float brightness = field.g / max(field.r, 0.0001);

    return max(brightness.xxx, WorldLight);
}

float4 GasPass(float2 uv : TEXCOORD0) : COLOR0
{
    // snap to the field-texel grid: every decision below happens per 2px cell,
    // which is what makes the result read as chunky vanilla pixels.
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    // world-locked, drifting FBM: the texture boils in place while the camera
    // pans across it, instead of swimming with the screen.
    float2 p = WorldUv(cell) * NoiseScale + NoiseDrift * Time;
    float fbm = Fbm(p);

    // erode the field with the noise, then cut it hard. (fbm - 0.5): erosion digs
    // and bulges around zero so Threshold keeps meaning "field level".
    float erosion = NoiseStrength * (fbm - 0.5);
    float d = Density(cell) - erosion;
    if (d < Threshold)
        discard;

    // The light the blobs on THIS cell are standing in, not one value for the whole screen.
    float3 light = FieldLight(cell);

    // testing one-texel neighbor test against the same erosion: any neighbor
    // falling outside the cut makes this cell the 2px outline.
    float2 tx = float2(FieldTexel.x, 0.0);
    float2 ty = float2(0.0, FieldTexel.y);
    float neighborMin = min(
        min(Density(cell - tx), Density(cell + tx)),
        min(Density(cell - ty), Density(cell + ty))) - erosion;

    if (neighborMin < Threshold)
        return float4(OutlineColor * light * OutlineAlpha, OutlineAlpha);

    // here we shade by depth-past-threshold and have the effect textured by the FBM so the body churns/mixes.
    // then just posterize it into flat bands.
    float t = saturate((d - Threshold) / max(1.0 - Threshold, 0.001));
    float shade = saturate(t * (0.55 + 0.9 * fbm));
    shade = floor(shade * Bands) / max(Bands - 1.0, 1.0);

    float3 color = lerp(ShadowColor, BodyColor, saturate(shade * 2.0));
    color = lerp(color, CoreColor, saturate(shade * 2.0 - 1.0));

    float alpha = BodyAlpha * lerp(0.75, 1.0, shade);
    return float4(color * light * alpha, alpha);
}

technique GasFbm
{
    pass P0
    {
        PixelShader = compile ps_3_0 GasPass();
    }
}
