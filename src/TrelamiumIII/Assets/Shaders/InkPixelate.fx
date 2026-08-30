// Snaps the frame to a square grid and fills each cell with the AVERAGE of what was
// under it, which is the difference between pixel art and static.
//
// Point-upscaling is also the cheaper option, and this deliberately isn't it: the
// source is rendered at full resolution and then thrown away down to the grid. That is
// the cost of not aliasing.
//
//   uTexel      (1/width, 1/height) of the SOURCE surface
//   uPixelSize  cell edge, in source pixels; <= 1 is a no-op (skip the pass instead)

sampler2D ImageSampler : register(s0);

float2 uTexel;
float  uPixelSize;

#define TAPS 4

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 cell = max(uPixelSize, 1.0) * uTexel;

    // Floor to the cell origin so every pixel inside a cell resolves to the same set of
    // taps, and therefore to the same colour. Without this the "grid" would slide with
    // the sample point and the whole effect would just read as a blur.
    float2 origin = floor(uv / cell) * cell;

    float4 sum = 0.0;

    [unroll]
    for (int y = 0; y < TAPS; y++)
    {
        [unroll]
        for (int x = 0; x < TAPS; x++)
        {
            // Sample the centre of each lattice square, not its corner, so the taps sit
            // symmetrically inside the cell and the average isn't biased up-left.
            float2 offset = (float2(x, y) + 0.5) / TAPS;
            sum += tex2D(ImageSampler, origin + offset * cell);
        }
    }

    return (sum / (TAPS * TAPS)) * col;
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
