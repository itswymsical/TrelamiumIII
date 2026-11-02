// Debug view: how much field is here, with no cut. Where the stamps are, before noise or threshold have had their say.
//
//@stage ViewCover phase=Finish needs=density

void $Stage(inout Px px)
{
    float d = saturate(px.density * 0.5);

    if (d <= 0.0)
        discard;

    px.color = float4(d, d, d, 1.0);
    px.light = float3(1.0, 1.0, 1.0);
}
