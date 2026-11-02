// Drags every noise layer along the local flow, so a moving cloud's texture moves with it. Zero leaves the noise world-locked.
//
//@stage FlowAdvect phase=Coords needs=flow
//@param Strength float 0.2 range=0..2

void $Stage(inout Px px)
{
    px.q -= px.flow * $Strength;
}
