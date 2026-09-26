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


    float travel = Hermite(1.0 - saturate(abs(band)));


    float depth = Hermite(saturate(1.0 - abs(at.y - uCentre.y) / max(uReach, 1e-4)));

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
