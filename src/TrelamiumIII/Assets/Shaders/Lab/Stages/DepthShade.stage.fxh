// How deep into the body this pixel is, stepped into bands. Larger Range flattens the body; under eight Bands reads very sharp.
//
//@stage DepthShade phase=Shade
//@param Range float 1 range=0.01..4
//@param Bands float 4 range=1..32

void $Stage(inout Px px)
{
    float t = 1.0 - exp(-px.depth / max($Range, 0.001));
    float shade = saturate(t * (0.55 + 0.9 * px.noise));

    px.shade = floor(shade * $Bands) / max($Bands - 1.0, 1.0);
}
