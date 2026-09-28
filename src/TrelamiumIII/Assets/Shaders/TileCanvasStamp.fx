// Splats one tile's sample into both canvas targets as a quadratic B-spline centred on the tile.
// Shifted B-splines at unit spacing sum to exactly 1, so a run of equal tiles reconstructs flat with
// no grid ripple. Blended One/One: both targets hold plain weighted sums.

matrix uWorldViewProjection;

struct VertexIn
{
    float4 Position : POSITION0;
    float4 Sample : COLOR0;     // coverage, data.xyz; premultiplied by the sample weight
    float4 Light : COLOR1;      // sample weight, world light premultiplied by it
    float2 Kernel : TEXCOORD0;  // offset from the tile centre in tiles, -1.5..1.5
};

struct VertexOut
{
    float4 Position : POSITION0;
    float4 Sample : COLOR0;
    float4 Light : COLOR1;
    float2 Kernel : TEXCOORD0;
};

struct CanvasOut
{
    float4 Sample : COLOR0;
    float4 Light : COLOR1;
};

VertexOut StampVS(VertexIn input)
{
    VertexOut output;

    output.Position = mul(input.Position, uWorldViewProjection);
    output.Sample = input.Sample;
    output.Light = input.Light;
    output.Kernel = input.Kernel;

    return output;
}

float BSpline(float x)
{
    x = abs(x);

    float inner = 0.75 - x * x;
    float outer = 0.5 * saturate(1.5 - x) * saturate(1.5 - x);

    return x < 0.5 ? inner : outer;
}

CanvasOut StampPS(VertexOut input)
{
    float k = BSpline(input.Kernel.x) * BSpline(input.Kernel.y);

    CanvasOut output;

    output.Sample = input.Sample * k;
    output.Light = input.Light * k;

    return output;
}

technique TileCanvasStamp
{
    pass P0
    {
        VertexShader = compile vs_3_0 StampVS();
        PixelShader = compile ps_3_0 StampPS();
    }
}
