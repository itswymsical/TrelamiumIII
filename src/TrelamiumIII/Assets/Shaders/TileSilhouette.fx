// Stamps a tile sprite into a field as pure shape plus payload.
//
// Tile silhouettes are the right stamp for a terrain mask -- the sprite's alpha is the real outline
// of a slope, a half block or a merged edge, which no flat quad can reproduce. Its COLOUR, though,
// is the block's own pigment, and a field that keeps it can only ever be read for coverage.
//
// So: take the alpha from the texture and the colour from the vertex. Whatever the caller tinted
// the draw with lands in the field's RGB, per stamp, at no extra cost and with no second surface.
// That is where per-tile data rides -- heat and char, for the burn.
//
// Everything stays premultiplied, because the field blends premultiplied. The incoming vertex
// colour already is (a Color scaled by a float scales all four channels), and multiplying the whole
// thing by one more scalar keeps it that way, so a reader recovers the payload as rgb / a.

sampler2D uImage0 : register(s0);

float4 StampPass(float4 color : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float coverage = tex2D(uImage0, uv).a;

    return color * coverage;
}

technique TileSilhouette
{
    pass P0
    {
        PixelShader = compile ps_2_0 StampPass();
    }
}
