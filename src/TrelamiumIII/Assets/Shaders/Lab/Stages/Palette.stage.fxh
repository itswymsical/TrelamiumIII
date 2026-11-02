// Three colours across the shade, and a second three the local temperature turns them toward. TemperatureBlend at zero ignores heat.
//
//@stage Palette phase=Shade needs=heat
//@param Shadow color 0.157,0.157,0.180
//@param Body color 0.353,0.353,0.392
//@param Core color 0.588,0.588,0.627
//@param HotShadow color 0,0,0
//@param HotBody color 0,0,0
//@param HotCore color 0,0,0
//@param TemperatureBlend float 0 range=0..4
//@param BodyAlpha float 0.68 range=0..1
//@param AlphaFloor float 0.75 range=0..1

void $Stage(inout Px px)
{
    px.blend = saturate(px.heat * $TemperatureBlend);

    px.color.rgb = lerp(
        LabRamp($Shadow, $Body, $Core, px.shade),
        LabRamp($HotShadow, $HotBody, $HotCore, px.shade),
        px.blend);

    px.color.a = $BodyAlpha * lerp($AlphaFloor, 1.0, px.shade);
}
