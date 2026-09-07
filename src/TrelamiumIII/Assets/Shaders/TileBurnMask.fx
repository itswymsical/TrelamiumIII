// Composites burning terrain: the fire half of a burn.
//
// Split off from TileConditionMask because the two passes want different things out of the same
// surface. The residues read one channel -- coverage -- and colour the whole field from two
// uniforms. A burn cannot: every tile is at its own point in its own fire, and one BodyColor for
// the screen is exactly why the old pass could not convey a tile scorching.
//
// So the field carries per-tile data, stamped through TileSilhouette.fx:
//
//   A  coverage   the tile's true silhouette, scaled by strength and by how hot the tile still is
//   R  heat       0..1, climbs as the fire takes and falls as it dies
//
// Both are premultiplied, so heat is recovered as r / a.
//
// Heat then drives a blackbody ramp -- ember red, through orange, to a pale yellow-white core --
// eroded by world-locked FBM so a run of burning blocks is one patch with holes in it rather than a
// row of identical squares. Cells near the erosion cut become the licking edge.
//
// The char is NOT here. A black mask composited in this plane reads as a sticker laid over the
// block, because it shares the fire's eroded edge and sits in the fire's plane; the block itself is
// darkened by the condition's overlay pass instead, which keeps the terrain's own outline and
// detail and simply takes its light away. Coverage here fades out with heat, so by the time a tile
// is fully charred this pass is contributing nothing to it and the handover is invisible.

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
float2 FieldTexel;      // 1 / field-target size; one texel = PixelScale screen px
float2 WorldOffset;     // screenPosition / screenHeight
float Aspect;           // screen w/h
float2 Zoom;            // Main.GameViewMatrix.Zoom

float Threshold;
float NoiseScale;
float NoiseStrength;
float2 NoiseDrift;
float EdgeWidth;

// Blackbody ramp, coolest to hottest. The fourth is the flash at the very centre of a raging tile.
float3 EmberColor;
float3 CoolColor;
float3 HotColor;
float3 FlashColor;

float BodyAlpha;
float EdgeAlpha;

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

// Screen uv -> world position, in units of screen heights. The zoom has to be undone because the
// field bakes it into its own transform; without this the char pattern slides across the terrain
// whenever the player pans while zoomed.
float2 WorldUv(float2 uv)
{
    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    return (uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
}

// Heat -> colour, across four stops. Piecewise rather than a single lerp so the hot end can be
// narrow: a fire spends most of its life orange and only briefly goes white, and an even ramp
// spreads that flash across half the range where it reads as washed out rather than as intense.
float3 HeatRamp(float heat)
{
    float3 low = lerp(EmberColor, CoolColor, saturate(heat / 0.45));
    float3 mid = lerp(CoolColor, HotColor, saturate((heat - 0.45) / 0.37));
    float3 high = lerp(HotColor, FlashColor, saturate((heat - 0.82) / 0.18));

    float3 color = (heat < 0.45) ? low : mid;

    return (heat < 0.82) ? color : high;
}

float4 BurnPass(float2 uv : TEXCOORD0) : COLOR0
{
    // Snap to the field-texel grid, so every decision below happens once per field texel and lands
    // on the same chunky pixel grid the rest of the mod's effects use.
    float2 cell = (floor(uv / FieldTexel) + 0.5) * FieldTexel;

    float4 field = tex2D(FieldSampler, cell);
    float coverage = field.a;

    if (coverage <= 0.004)
        discard;

    // Un-premultiply to recover what the stamp actually meant. Guarded because a texel on the
    // feathered rim of a silhouette can carry almost no alpha at all.
    float heat = saturate(field.r / max(coverage, 0.0039));

    // World-locked and drifting: the flame boils in place while the camera pans across it, instead
    // of swimming with the screen.
    float2 p = WorldUv(cell) * NoiseScale + NoiseDrift * Time;
    float fbm = Fbm(p);

    // Centred on zero so the erosion digs and bulges either way, which keeps Threshold meaning
    // "coverage level" rather than drifting with NoiseStrength.
    float d = coverage - NoiseStrength * (fbm - 0.5);

    if (d < Threshold)
        discard;

    float depth = saturate((d - Threshold) / max(EdgeWidth, 0.001));

    // The rim of the eroded patch runs hotter than its middle. That is where a real fire is
    // brightest -- the edge is the flame front, the interior is what it has already been through --
    // and it is what turns a flat tinted blob into something with a licking outline.
    float rimHeat = saturate(heat + (1.0 - depth) * 0.35);

    float3 scorch = HeatRamp(rimHeat);

    // Fire is its own emission and takes none of the room's light -- a burning tile is lit in an
    // unlit cave. The char that follows it is terrain, and is drawn as terrain by the overlay pass
    // rather than composited here, so nothing in this pass reads world light at all.
    float alpha = lerp(EdgeAlpha, BodyAlpha, depth);

    return float4(scorch * alpha, alpha);
}

technique TileBurnMask
{
    pass P0
    {
        PixelShader = compile ps_3_0 BurnPass();
    }
}
