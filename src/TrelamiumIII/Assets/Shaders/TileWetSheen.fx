// Per-tile overlay for a wet surface.
//
// Runs over one sprite draw per tile, where s0 is the tile's own sheet and the source rectangle is
// its frame, so the alpha here is already the tile's exact silhouette -- slopes and half blocks
// included -- and nothing needs masking by hand.
//
// The vertex colour carries the wash: its rgb is the tint the tile is darkened by and its alpha is
// how far the soak has progressed, both premultiplied by the caller. On top of that a noise
// lookup, sharpened hard so only its peaks survive, adds the specular glint that separates "wet"
// from "painted dark blue". The lookup is in texture space rather than world space, which ties the
// glint pattern to the tile's frame -- neighbouring tiles with different frames catch the light
// differently, which is what wet stone actually does.

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
float SheenSharpness;   // higher = fewer, tighter highlights

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
