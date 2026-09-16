// Stamps one node's soft blob into BOTH field targets in a single pass.
//
// The accumulation pass every FieldLayer runs at CheckMonoliths. Replaces the SpriteBatch draw
// that used to do this, for two reasons:
//
//   1. A sprite vertex carries one colour. Two targets need two, and they have to arrive under
//      exactly the same weight or the division that recovers an attribute from its coverage is
//      measuring two different blobs. Writing both from one vertex makes that structural.
//   2. The blend is ours now. SpriteBatch's additive state multiplies source by source-alpha,
//      which quietly squared the coverage and spent the alpha channel as a weight. Doing the
//      falloff here instead leaves all eight channels free to carry data.

matrix uWorldViewProjection;

texture2D uStamp;
sampler2D StampSampler = sampler_state
{
    Texture = <uStamp>;
};

struct VertexIn
{
    float4 Position : POSITION0;
    float4 Field : COLOR0;
    float4 Attributes : COLOR1;
    float2 Tex : TEXCOORD0;
};

struct VertexOut
{
    float4 Position : POSITION0;
    float4 Field : COLOR0;
    float4 Attributes : COLOR1;
    float2 Tex : TEXCOORD0;
};

struct StampOut
{
    float4 Field : COLOR0;
    float4 Attributes : COLOR1;
};

VertexOut StampVS(VertexIn input)
{
    VertexOut output;

    output.Position = mul(input.Position, uWorldViewProjection);
    output.Field = input.Field;
    output.Attributes = input.Attributes;
    output.Tex = input.Tex;

    return output;
}

StampOut StampPS(VertexOut input)
{
    float4 stamp = tex2D(StampSampler, input.Tex);

    // The stamp textures disagree about where they keep their falloff: RadialGradient and
    // BloomFlare are opaque and carry it in RGB, Bloom is white and carries it in alpha. The
    // product reads correctly for both, which is exactly what the old source-alpha blend was
    // computing by accident. Taking a single scalar rather than per-channel RGB also means a
    // stamp that happens not to be grey can no longer tint one data channel against another.
    float falloff = max(stamp.r, max(stamp.g, stamp.b)) * stamp.a;

    StampOut output;

    output.Field = input.Field * falloff;
    output.Attributes = input.Attributes * falloff;

    return output;
}

technique FieldStamp
{
    pass P0
    {
        VertexShader = compile vs_3_0 StampVS();
        PixelShader = compile ps_3_0 StampPS();
    }
}
