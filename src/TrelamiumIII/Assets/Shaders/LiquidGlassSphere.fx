// A glass sphere pressed into the frame. Unlike an additive overlay it draws almost no light
// of its own
//
// The 3D read comes from treating the disc as the projection of a sphere. For a pixel at
// normalised radius rn:
//
//   z = sqrt(1 - rn^2)         surface height above the screen plane
//   n = (dir * rn, z)          already unit length, so no normalize needed
//
// z is doing double duty: it is the surface normal's Z, and it is the thickness of glass the
// view ray passes through
//
// The radial remap has two lobes that both vanish at rn == 1, so the disc joins the untouched
// frame with no seam:
//
//   core  = uBulge * rn * (1 - rn^2)                    samples pulled inward  -> magnifies
//   bevel = uBevel * smoothstep(uBevelStart,1,rn) * (1-rn)   samples pushed out -> compresses
//   rd    = rn - core + bevel
//
// The core is the bulge. The bevel is the sloped edge of real glass, which squeezes the
// surroundings into a thin band and is most of why the shape reads as a solid object rather
// than a tinted hole.
//
//   uCenter          0..1 sphere centre
//   uRadius          sphere radius in aspect-corrected UV
//   uAspect          (width/height, 1) so the sphere reads circular
//   uMix             0..1 effect weight
//   uBulge           strength of the magnifying core lobe
//   uBevel           strength of the compressing rim lobe
//   uBevelStart      normalised radius the bevel begins at
//   uDispersion      chromatic split, scaled by how hard the glass is bending
//   uFrost           blur radius at the rim, in units of uRadius
//   uFieldLines      how many frost bands sweep the polar axis
//   uFieldFrost      0..1 how much the bands modulate the frost
//   uTime            seconds-ish, sweeps the bands
//   uRingInner       normalised radius the silhouette ring starts at
//   uRingPower       ring tightness; higher = thinner
//   uRingBrightness  ring gain
//   uSpecular        highlight gain
//   uIridescence     thin-film sheen at grazing angles
//   uTint            glass colour
//   uTintStrength    0..1 how much colour the glass carries

sampler2D ImageSampler : register(s0);

float2 uCenter;
float  uRadius;
float2 uAspect;
float  uMix;

float  uBulge;
float  uBevel;
float  uBevelStart;
float  uDispersion;

float  uFrost;
float  uFieldLines;
float  uFieldFrost;
float  uTime;

float  uRingInner;
float  uRingPower;
float  uRingBrightness;

float  uSpecular;
float  uIridescence;
float3 uTint;
float  uTintStrength;

// Even hexagon; with the centre tap weighted double this reads as a soft disc rather than
// the ring-shaped bokeh a bare circle of taps would give.
static const float2 kTaps[6] =
{
    float2( 1.000,  0.000),
    float2( 0.500,  0.866),
    float2(-0.500,  0.866),
    float2(-1.000,  0.000),
    float2(-0.500, -0.866),
    float2( 0.500, -0.866)
};

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 bg = tex2D(ImageSampler, uv);

    float2 d      = (uv - uCenter) * uAspect;
    float  dist   = length(d);
    float  radius = max(uRadius, 1e-4);
    float  rn     = dist / radius;

    // Everything past the silhouette is untouched frame. Bailing here also keeps the tap
    // count off the rest of the screen.
    if (rn >= 1.0)
        return bg;

    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 1.0);

    float  z = sqrt(saturate(1.0 - rn * rn));
    float3 n = float3(dir * rn, z);

    float core  = uBulge * rn * (1.0 - rn * rn);
    float bevel = uBevel * smoothstep(uBevelStart, 1.0, rn) * (1.0 - rn);
    float rd    = rn - core + bevel;

    float2 basis = (dir * radius) / uAspect;

    float disp = uDispersion * (core + bevel);

    // Frost widens toward the rim so the middle stays legible. The bands sweep around the
    // polar axis, which keeps the magnetic reading without drawing a single line.
    float theta = atan2(d.x, -d.y);
    float lines = 0.5 + 0.5 * sin(theta * uFieldLines + uTime);
    float frost = uFrost * (0.15 + 0.85 * rn * rn) * lerp(1.0, lines, uFieldFrost);
    float2 blur = (frost * radius) / uAspect;

    float2 pR = uCenter + basis * (rd + disp);
    float2 pG = uCenter + basis * rd;
    float2 pB = uCenter + basis * (rd - disp);

    float3 glass = float3(tex2D(ImageSampler, pR).r,
                          tex2D(ImageSampler, pG).g,
                          tex2D(ImageSampler, pB).b) * 2.0;

    [unroll]
    for (int i = 0; i < 6; i++)
    {
        float2 o = kTaps[i] * blur;
        glass.r += tex2D(ImageSampler, pR + o).r;
        glass.g += tex2D(ImageSampler, pG + o).g;
        glass.b += tex2D(ImageSampler, pB + o).b;
    }
    glass /= 8.0;

    // The surface turns away from the viewer at the silhouette, so the glass gathers colour
    // there and stays near-invisible through the middle.
    float fresnel = pow(1.0 - z, 3.0);

    float3 lightDir = normalize(float3(-0.45, -0.62, 0.64));
    float3 halfVec  = normalize(lightDir + float3(0.0, 0.0, 1.0));
    float  ndoth    = saturate(dot(n, halfVec));
    // Tight hotspot plus a broad sheen: one highlight alone reads as plastic.
    float  spec     = (pow(ndoth, 60.0) + pow(ndoth, 6.0) * 0.12) * uSpecular;

    // The ring is the radius indicator, so it is the part that has to stay readable when
    // everything else is nearly transparent. sin() peaks it just inside the silhouette and
    // takes it to zero at both ends of the band, so there is no hard edge to alias.
    float t    = saturate((rn - uRingInner) / max(1.0 - uRingInner, 1e-4));
    // saturate() only to promise the compiler the base is non-negative; t is already 0..1.
    float ring = pow(saturate(sin(t * 3.14159265)), uRingPower);

    float3 rimColor = lerp(uTint, float3(1.0, 1.0, 1.0), 0.6);

    // Iridescent film, same sheen the mod's UI glass carries (LiquidGlass.fx uOilyness) but
    // driven off the real sphere normal instead of a faked dome. Rides fresnel because
    // thin-film interference only shows at grazing angles.
    float3 sheen = 0.5 + 0.5 * sin(float3(0.0, 2.1, 4.2) + (1.0 - z) * 9.0 + uTime * 0.4);

    glass = lerp(glass, uTint, uTintStrength * (0.3 + 0.7 * fresnel));
    glass += sheen * uIridescence * fresnel;
    glass += rimColor * ring * uRingBrightness;
    glass += spec;

    // Fades over the last sliver of the disc so the silhouette itself is not a jagged circle.
    float mask = (1.0 - smoothstep(0.985, 1.0, rn)) * saturate(uMix);

    // col is the sky tint, not white. Ignored on purpose -- see the note at the top.
    return float4(lerp(bg.rgb, glass, mask), bg.a);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
