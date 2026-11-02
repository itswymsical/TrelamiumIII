// Bends the lookup of every noise layer after it with two decorrelated taps of its own noise, which turns flat FBM into something
// that curls. Pivot is the noise's middle: off-centre, it slides the whole pattern as Amount rises.
//
//@stage DomainWarp phase=Coords needs=world
//@param Amount float 0.2 range=0..2
//@param Scale float 1 range=0.05..8
//@param Drift float2 0,0 range=-1..1
//@param Pivot float 0.47 range=0..1
//@param Octaves int 2 range=1..6
//@param Noise noise Milky_2

void $Stage(inout Px px)
{
    float2 p = px.p * $Scale + $Drift * Time;
    float2 warp = float2(LabFbm($Noise, p, $Octaves), LabFbm($Noise, p + float2(0.37, 0.71), $Octaves));

    px.p += (warp - $Pivot) * $Amount;
}
