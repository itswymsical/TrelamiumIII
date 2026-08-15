// An expanding ring that displaces what it passes over radially, run across Main.instance
// tileTarget so only the tile layer bends -- players, NPCs and projectiles are drawn afterwards
// in world space and stay perfectly still, which is what sells it as the ground moving rather
// than the camera wobbling.
//
//   band = (dist - uRadius) / uThickness    -1 behind the crest .. 0 at it .. +1 ahead of it
//   w    = hermite(1 - |band|)              1 at the crest, exactly 0 at |band| >= 1
//   push = sin(band * PI) * w               signed: pulls inward behind, pushes outward ahead
//
// sin over -PI..PI is the part that reads as a wave instead of a bulge: it is negative on the
// inside half of the band and positive on the outside half, so the image compresses in front of
// the crest and rarefies behind it, the way a real pressure wave does. It is also exactly 0 at
// band == 0 and band == +-1, so the crest itself and both rims are undisplaced and the band
// joins the still image with no seam.
//
//   uCenter     0..1 ring centre in TARGET uv, not screen uv -- texel (0,0) of the tile target
//               is world position Main.sceneTilePos, not Main.screenPosition
//   uRadius     current ring radius, as a fraction of target WIDTH
//   uThickness  half-width of the band, same units
//   uAmplitude  peak displacement, same units
//   uMix        0..1 overall strength, carrying the wave's fade-out
//   uAspect     (1, height/width) so the ring is circular in texels rather than in uv
//
// Every length above is divided by the target's width on the C# side. That is deliberate: with
// uAspect built on width, one unit of distance in this shader is one target texel on BOTH axes,
// so a radius in world pixels becomes a radius here with a single divide and no second factor.

// COLOR0 is ignored on purpose. This runs as an explicit spriteBatch.Draw with Color.White, so
// col is white and multiplying by it is a no-op -- but if this shader is ever repurposed as a
// Filters.Scene pass, col becomes Main.ColorOfTheSkies and multiplying would crush the whole
// layer toward black at night. Not multiplying keeps it safe in both places.

sampler2D ImageSampler : register(s0);

float2 uCenter;
float  uRadius;
float  uThickness;
float  uAmplitude;
float  uMix;
float2 uAspect;

static const float PI = 3.14159265;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 d = (uv - uCenter) * uAspect;
    float dist = length(d);

    float thickness = max(uThickness, 1e-5);
    float band = (dist - uRadius) / thickness;

    // NOT smoothstep on the raw distance: the window has to close on both sides of the crest so
    // the ring is a travelling band rather than a disc that keeps everything inside it warped.
    float w = 1.0 - saturate(abs(band));
    w = w * w * (3.0 - 2.0 * w);

    float push = sin(band * PI) * w * uAmplitude * saturate(uMix);

    float2 dir = dist > 1e-5 ? d / dist : float2(0.0, 0.0);

    // Back out of the aspect-corrected space to land in uv again. Dividing by uAspect is the
    // exact inverse of the multiply at the top, so a displacement measured in texels arrives as
    // the right fraction of uv on each axis independently.
    float2 src = uv + (dir * push) / uAspect;

    return tex2D(ImageSampler, src);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
