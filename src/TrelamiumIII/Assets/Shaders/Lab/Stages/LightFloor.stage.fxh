// A floor under the light recovered from the field. Raise it for a material that stays legible in an unlit cave, and leave it at
// zero for one that goes dark with the room.
//
//@stage LightFloor phase=Shade needs=light
//@param Floor color 0,0,0

void $Stage(inout Px px)
{
    px.light = max(px.light, $Floor);
}
