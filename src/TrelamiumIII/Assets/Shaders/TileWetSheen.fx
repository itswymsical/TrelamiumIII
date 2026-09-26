sampler2D uImage0 : register(s0);

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
float NoiseScale;
float2 NoiseDrift;

float3 SheenColor;
float SheenStrength;
float SheenSharpness;

float4 WetPass(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 tile = tex2D(uImage0, uv);

    if (tile.a <= 0.01)
        discard;

    float2 p = uv * NoiseScale + NoiseDrift * Time;
    float sheen = pow(saturate(tex2D(NoiseSampler, p).r), SheenSharpness) * SheenStrength;

    float alpha = color.a * tile.a;

    // color.rgb is premultiplied already, so it is masked by the tile's coverage rather than
    // multiplied by the full alpha a second time.
    float3 rgb = color.rgb * tile.a + SheenColor * sheen * alpha;

    return float4(rgb, alpha);
}

technique TileWetSheen
{
    pass P0
    {
        PixelShader = compile ps_3_0 WetPass();
    }
}
