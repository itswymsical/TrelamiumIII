// One layer of noise eating at the field's edge. Layers sum. Pivot is the value of this noise that erodes nothing, and it isn't the
// same for every texture. ShadeShare is how far this layer takes over the noise that textures the body's shading.
//
//@stage NoiseLayer phase=Shape
//@param Scale float 0.2 range=0.01..8
//@param Strength float 0.32 range=-1..1
//@param Drift float2 0.05,-0.09 range=-1..1
//@param Stretch float2 1,1 range=0.05..4
//@param Pivot float 0.5 range=0..1
//@param ShadeShare float 1 range=0..1
//@param Octaves int 4 range=1..6
//@param Noise noise Milky_2

void $Stage(inout Px px)
{
    float2 p = px.p * $Scale;
    p = p * $Stretch + $Drift * Time + px.q;

    float n = LabFbm($Noise, p, $Octaves);

    px.erosion += $Strength * (n - $Pivot);
    px.noise = lerp(px.noise, n, $ShadeShare);
}
