// Debug view: which way the field is moving. Red is rightward and green is downward; grey-yellow is still.
//
//@stage ViewFlow phase=Finish needs=density,flow

void $Stage(inout Px px)
{
    if (px.density <= 0.0)
        discard;

    px.color = float4(px.flow * 0.5 + 0.5, 0.2, 1.0);
    px.light = float3(1.0, 1.0, 1.0);
}
