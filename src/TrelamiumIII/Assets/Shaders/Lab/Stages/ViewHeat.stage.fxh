// Debug view: how hot the field is against its temperature range, from blue through to pale orange.
//
//@stage ViewHeat phase=Finish needs=density,heat

void $Stage(inout Px px)
{
    if (px.density <= 0.0)
        discard;

    px.color = float4(lerp(float3(0.1, 0.2, 0.5), float3(0.9, 0.6, 0.3), saturate(px.heat)), 1.0);
    px.light = float3(1.0, 1.0, 1.0);
}
