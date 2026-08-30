// A ground heave that moves whole tiles, not pixels.
//
// TileShockwave.fx displaces every pixel independently, which is why it reads as the frame being
// warped: tile edges bend, and a 16x16 block of dirt stops being square. This one evaluates the
// wave ONCE per tile and gives every pixel in that tile the same offset, so blocks stay rigid and
// the terrain shifts in discrete chunks -- the ground breaking up rather than the lens bending.
//
// Three things have to line up for that to hold:
//
//   1. The quantisation grid must be the WORLD tile grid, not an arbitrary 16px grid in the
//      target. Target texel (0,0) sits at Main.sceneTilePos, which is almost never a multiple of
//      16, so uGridPhase carries that misalignment in and every floor() below is corrected by it.
//      Without this the "blocks" would be 16px chunks straddling tile boundaries -- half of one
//      dirt block moving with half of its neighbour, which looks like a glitch, not a shockwave.
//
//   2. The offset is snapped to uSnap texels. At uSnap 1 the terrain slides on the texel grid --
//      whole pixels, never a fraction, so nothing is ever resampled between texels and the pixel
//      art stays intact while the motion itself is smooth. Raising it to 16 makes a tile display
//      exactly the tile some whole number of rows away: a true block swap, but the ground then
//      jumps in 16px steps and reads as stair-stepping rather than movement.
//
//      uBlend decides how rigid a block is. At 0 every pixel of a tile takes the offset of the
//      tile's centre, so blocks are perfectly square and neighbours meet at hard seams. At 1 the
//      wave is evaluated per pixel and the whole thing collapses back to a smooth warp. In between,
//      a block mostly holds its shape but eases toward its neighbours across its own width, which
//      is what closes the seams without dissolving the grid.
//
//   3. The wave travels HORIZONTALLY and lifts VERTICALLY. That is what makes it a ground wave
//      rather than an expanding ring, and it is why uReach exists: without a vertical falloff the
//      entire column shears, including sky and the cavern roof forty tiles up, which reads as the
//      camera tearing rather than the earth moving.
//
//   uSize       target dimensions in texels
//   uCentre     impact point in target texels
//   uGridPhase  world tile grid alignment within the target, 0..16 on each axis
//   uRadius     how far the front has travelled from the impact, texels
//   uThickness  half-width of the moving band, texels
//   uAmplitude  peak lift at the crest, texels
//   uReach      vertical falloff distance from the impact height, texels
//   uCycles     1 = a single hump; 3 = a hump with a trough either side
//   uSnap       offset quantisation, texels (1 = texel grid, 16 = whole tiles)
//   uBlend      0 = rigid blocks, 1 = smooth warp; how far a block eases toward its neighbours
//   uMix        0..1 overall strength, carrying the wave's fade
//

sampler2D ImageSampler : register(s0);

float2 uSize;
float2 uCentre;
float2 uGridPhase;
float  uRadius;
float  uThickness;
float  uAmplitude;
float  uReach;
float  uCycles;
float  uSnap;
float  uBlend;
float  uMix;

static const float TILE = 16.0;
static const float HALF_PI = 1.5707963;

float Hermite(float x)
{
    return x * x * (3.0 - 2.0 * x);
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uv * uSize;

    // The centre of the world tile this pixel belongs to, in target texels.
    float2 tile = floor((px + uGridPhase) / TILE) * TILE + (TILE * 0.5) - uGridPhase;

    // Where the wave is actually sampled. Pinning this to the tile centre is what makes a block
    // rigid; letting it drift back toward px lets the block ease across its own width and meet its
    // neighbours instead of stepping against them. This one lerp is the entire rigid/smooth axis.
    float2 at = lerp(tile, px, saturate(uBlend));

    // Horizontal band, symmetric about the impact so the wave runs both ways from a slam.
    float distance = abs(at.x - uCentre.x);
    float band = (distance - uRadius) / max(uThickness, 1e-4);

    // Closes on BOTH sides of the crest, so ground ahead of the wave and ground it has already
    // passed are equally untouched. A one-sided falloff would leave everything behind the front
    // permanently displaced.
    float travel = Hermite(1.0 - saturate(abs(band)));

    // Vertical falloff. Squared-off with the same curve so the slab thins out with depth instead
    // of ending on a hard seam.
    float depth = Hermite(saturate(1.0 - abs(at.y - uCentre.y) / max(uReach, 1e-4)));

    // cos over the band: 1 at the crest, 0 at both rims for any odd uCycles, with uCycles - 1
    // sign changes in between. So uCycles is a ripple count that never breaks the seam at the rim.
    float shape = travel * depth * cos(band * HALF_PI * uCycles);

    // Negative is up: target texels count downward, and a shockwave lifts.
    float lift = -shape * uAmplitude * saturate(uMix);

    float snap = max(uSnap, 1.0);
    lift = floor(lift / snap + 0.5) * snap;

    // px sits at a pixel centre (i + 0.5) for a 1:1 blit, and lift is a whole number of texels, so
    // the source lands on a pixel centre too. No sampling ambiguity, no half-texel creep.
    float2 source = px - float2(0.0, lift);

    return tex2D(ImageSampler, source / uSize);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
