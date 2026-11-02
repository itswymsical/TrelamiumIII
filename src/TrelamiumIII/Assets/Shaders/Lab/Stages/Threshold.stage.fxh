// Where the material exists. Keep Level above the most the noise can erode: the erosion subtracts, so below that the shader draws
// material out of the noise alone wherever there's no field at all.
//
//@stage Threshold phase=Cut needs=density
//@param Level float 0.22 range=0..2

void $Stage(inout Px px)
{
    px.level = $Level;
    px.depth = px.density - px.erosion - $Level;

    if (px.depth < 0.0)
        discard;
}
