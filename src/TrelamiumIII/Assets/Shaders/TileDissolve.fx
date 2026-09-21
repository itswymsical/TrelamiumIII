// Dissolves one sprite through noise snapped to the sprite's own texel grid, with a flat
// coloured band along the cut. Applied once per draw so each piece has its own progress.
//
//   uTexelSize    1 / source texture size in pixels
//   uNoiseScale   noise uv advanced per source texel
//   uNoiseOffset  per-piece offset into the noise
//   uProgress     0 = intact, 1 = fully gone (band included)
//   uEdge         width of the band, in noise units
//   uEdgeColor    band colour; unlit, premultiplied by the sprite's alpha here

sampler2D SpriteSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Point;
    MagFilter = Point;
};

float2 uTexelSize;
float  uNoiseScale;
float2 uNoiseOffset;
float  uProgress;
float  uEdge;
float3 uEdgeColor;

float4 Dissolve(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(SpriteSampler, uv);

    float2 texel = floor(uv / uTexelSize);
    float n = tex2D(NoiseSampler, texel * uNoiseScale + uNoiseOffset).r;

    // Remapped so 0 keeps every pixel and 1 removes every pixel, band included.
    float cut = uProgress * (1.0 + uEdge) - uEdge;

    if (n < cut)
        discard;

    if (n < cut + uEdge)
        return float4(uEdgeColor * base.a, base.a) * color.a;

    return base * color;
}

technique TileDissolve
{
    pass P0
    {
        PixelShader = compile ps_3_0 Dissolve();
    }
}
