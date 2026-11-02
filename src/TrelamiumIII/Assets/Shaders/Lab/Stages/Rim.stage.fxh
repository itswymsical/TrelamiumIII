// The edge of the material: a bright surface line where the cell above falls outside the cut, and an outline on what's left. A
// HighlightAlpha of zero turns the surface line off, which is what makes this a gas rather than a liquid. Needs a cut to measure from.
//
//@stage Rim phase=Edge needs=neighbours
//@param Outline color 0.094,0.094,0.110
//@param OutlineAlpha float 0.75 range=0..1
//@param HotOutline color 0,0,0
//@param Highlight color 1,1,1
//@param HighlightAlpha float 0 range=0..1
//@param HotHighlight color 0,0,0

void $Stage(inout Px px)
{
    float2 tx = float2(px.texel.x, 0.0);
    float2 ty = float2(0.0, px.texel.y);

    float up = HostDensity(px.uv - ty) - px.erosion;
    float side = min(HostDensity(px.uv + ty), min(HostDensity(px.uv - tx), HostDensity(px.uv + tx))) - px.erosion;

    if ($HighlightAlpha > 0.0 && up < px.level)
        px.color = float4(lerp($Highlight, $HotHighlight, px.blend), $HighlightAlpha);
    else if (min(side, up) < px.level)
        px.color = float4(lerp($Outline, $HotOutline, px.blend), $OutlineAlpha);
}
