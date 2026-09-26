//   uCenter     0..1 ring centre in TARGET uv, not screen uv -- texel (0,0) of the tile target
//               is world position Main.sceneTilePos, not Main.screenPosition
//   uRadius     current ring radius, as a fraction of target WIDTH
//   uThickness  half-width of the band, same units
//   uAmplitude  peak displacement, same units
//   uMix        0..1 overall strength, carrying the wave's fade-out
//   uAspect     (1, height/width) so the ring is circular in texels rather than in uv
//

sampler2D ImageSampler : register(s0);

float2 uCenter;
float  uRadius;
float  uThickness;
float  uAmplitude;
float  uMix;
float2 uAspect;

static const float PI = 3.14159265;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 d = (uv - uCenter) * uAspect;
    float dist = length(d);

    float thickness = max(uThickness, 1e-5);
    float band = (dist - uRadius) / thickness;

    float w = 1.0 - saturate(abs(band));
    w = w * w * (3.0 - 2.0 * w);

    float push = sin(band * PI) * w * uAmplitude * saturate(uMix);

    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 0.0);

    float2 src = uv + (dir * push) / uAspect;

    return tex2D(ImageSampler, src);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
