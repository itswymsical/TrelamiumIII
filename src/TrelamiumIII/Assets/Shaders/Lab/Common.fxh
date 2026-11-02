// What every shader the VFX Lab generates shares: the state a pixel carries from stage to stage, and the helpers stages lean on.
// Flattened into each generated shader, so a change here reaches exported content only when it's re-exported.
//
// Sample with tex2Dlod throughout. Stages run inside whatever flow control the stack ends up with, and tex2D there raises
// X3570 and X4121. The field and noise textures have no mips, so the result is the same.

struct Px
{
    float2 uv;          // where this pixel reads the host's textures, snapped to the host's grid
    float2 texel;       // one step on that grid
    float2 world;       // world position in screen heights. Never moves
    float2 p;           // noise lookup in world units. Coords stages move it, before each layer applies its scale
    float2 q;           // noise lookup offset in noise units, added after a layer's scale
    float2 flow;        // local velocity, -1..1
    float density;      // the host's field value here
    float heat;         // local temperature, 0..1 of the layer's range
    float age;          // local age, 0..1 of a node's life
    float bind;         // spare channel
    float variant;      // spare channel
    float3 light;       // world brightness here
    float erosion;      // summed by Shape stages; the cut subtracts it from density
    float noise;        // the noise layer that shades the body
    float level;        // where the cut sits
    float depth;        // how far past the cut this pixel is
    float shade;        // 0 at the thin edge of the body, 1 at its core. Banding can overshoot 1, as GasFbm always has
    float blend;        // how far this pixel has turned toward the palette's hot colours
    float4 color;       // straight alpha; the host premultiplies and lights it last
};

float LabFbm(sampler2D s, float2 p, int octaves)
{
    float sum = 0.0;
    float amp = 0.5;

    for (int i = 0; i < octaves; i++)
    {
        sum += tex2Dlod(s, float4(p, 0.0, 0.0)).r * amp;
        p = p * 2.02 + 0.13;
        amp *= 0.5;
    }

    return sum;
}

float3 LabRamp(float3 shadow, float3 body, float3 core, float shade)
{
    float3 color = lerp(shadow, body, saturate(shade * 2.0));
    return lerp(color, core, saturate(shade * 2.0 - 1.0));
}
