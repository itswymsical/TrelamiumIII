// Debug view: the noise that textures the body, shown wherever there's any field.
//
//@stage ViewNoise phase=Finish needs=density

void $Stage(inout Px px)
{
    if (px.density <= 0.0)
        discard;

    px.color = float4(px.noise, px.noise, px.noise, 1.0);
    px.light = float3(1.0, 1.0, 1.0);
}
