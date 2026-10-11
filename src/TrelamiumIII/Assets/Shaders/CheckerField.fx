// Placeholder field look: a world-locked two-colour checkerboard inside the cut, with a one-texel rim.
//
//   Threshold     field value where the material exists
//   FieldGain     stamp overdrive, as in GasFbm
//   CheckerScale  checker cells per screen height
//   ColorA/B      the two cell colours; OutlineColor the rim
//   BodyAlpha     opacity of the whole thing

sampler2D FieldSampler : register(s0);

float2 FieldTexel;      // 1 / field-target size
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h
float2 Zoom;            // Main.GameViewMatrix.Zoom

float Threshold;
float FieldGain;
float CheckerScale;

float3 ColorA;
float3 ColorB;
float3 OutlineColor;
float BodyAlpha;

float2 WorldUv(float2 uv)
{
    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
}

float Density(float2 uv)
{
    float f = tex2D(FieldSampler, uv).r * FieldGain;
    return f * f;
}

float4 FieldPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    if (Density(cell) < Threshold)
        discard;

    float2 tx = float2(FieldTexel.x, 0.0);
    float2 ty = float2(0.0, FieldTexel.y);
    float edge = min(min(Density(cell + tx), Density(cell - tx)), min(Density(cell + ty), Density(cell - ty)));

    if (edge < Threshold)
        return float4(OutlineColor * BodyAlpha, BodyAlpha);

    float2 square = floor(WorldUv(cell) * CheckerScale);
    float parity = abs(fmod(square.x + square.y, 2.0));
    float3 color = lerp(ColorA, ColorB, parity);

    return float4(color * BodyAlpha, BodyAlpha);
}

technique CheckerField
{
    pass P0
    {
        PixelShader = compile ps_3_0 FieldPass();
    }
}
