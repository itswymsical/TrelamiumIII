//   uPhase      0..1 sweep position (host-driven)
//   uRotation   streak angle, degrees
//   uWidth      streak width
//   uSmoothness edge softness
//   uBrightness streak gain
//   uDistortion 1..2 edge-gradient bias (fades the streak toward the corners)
//   uAlpha      max opacity (already x fade)
//   col (COLOR0) streak colour

float uPhase;
float uRotation;
float uWidth;
float uSmoothness;
float uBrightness;
float uDistortion;
float uAlpha;

#define DEG2RAD 0.01745329252

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 c = uv - 0.5;
    float grad = 1.0 - max(abs(c.x), abs(c.y)) * uDistortion;   // 1 at centre -> dim edges

    float a = uRotation * DEG2RAD;
    float ca = cos(a);
    float sa = sin(a);
    float rx = c.x * ca + c.y * sa + 0.5;                       // rotated x, recentred

    float t = -2.0 + 4.0 * frac(uPhase);                        // sweep across -2..2
    float streak = sqrt(abs((rx + t) * grad));

    float s = clamp(uSmoothness, 0.001, 1.0);
    float lo = uWidth - s;
    float hi = uWidth + s;
    float v = (streak - hi) / (lo - hi);                        // 1 inside the streak, 0 outside
    v *= uBrightness;
    v = saturate(min(v, uAlpha));

    return float4(col.rgb * v, v);                              // premultiplied alpha
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
