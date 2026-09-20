// Skill tree backdrop: a drifting FBM nebula through a three-colour ramp, a twinkling starfield and up
// to four metaball blobs fed by held nodes. s0 masks the whole result, so a white pixel fills the rect.
//
//   uTime         seconds
//   uAspect       (width/height, 1) so the swirl and the blobs read circular
//   uPan          nebula scroll in aspect space; the blobs are screen-anchored and ignore it
//   uScale        base FBM frequency
//   uPivot        median of the raw FBM sum for the bound noise texture, see Fbm()
//   uExpand       histogram stretch applied after uPivot
//   uWarp         domain-warp strength
//   uSwirl        winding strength; the rotation angle is uSwirl / (r + uSwirlCore)
//   uSwirlCore    softening on that divide
//   uSpin         rigid rotation rate layered on the shear, rad/sec
//   uDensity      coverage midpoint of the nebula body
//   uContrast     half-width of the body's feather band
//   uStarScale    starfield cells per aspect unit
//   uStarCut      fraction of cells left empty, 0..1
//   uStarPower    exponent on a star's distance falloff; higher gives smaller, harder points
//   uTwinkle      twinkle depth, 0..1
//   uBlob0..3     (xy centre in aspect space, z radius, w strength); strength 0 disables the slot
//   uBlobColor0..3  emission colour of each blob
//   uBlobFloor    summed field value at which blob cores turn solid
//   uBlobSoft     feather on that threshold
//   uVignette     corner falloff, 0..1
//   uOpacity      master scalar, drives the panel's open/close fade
//   uVoidColor / uNebulaLow / uNebulaHigh   ramp stops, low to high
//   uStarColor    star tint

sampler2D ImageSampler : register(s0);

texture2D uNoiseTex;
sampler2D NoiseSampler = sampler_state
{
    Texture = <uNoiseTex>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
};

float  uTime;
float2 uAspect;
float2 uPan;
float  uScale;
float  uPivot;
float  uExpand;
float  uWarp;
float  uSwirl;
float  uSwirlCore;
float  uSpin;
float  uDensity;
float  uContrast;

float  uStarScale;
float  uStarCut;
float  uStarPower;
float  uTwinkle;

float4 uBlob0;
float4 uBlob1;
float4 uBlob2;
float4 uBlob3;
float3 uBlobColor0;
float3 uBlobColor1;
float3 uBlobColor2;
float3 uBlobColor3;
float  uBlobFloor;
float  uBlobSoft;

float  uVignette;
float  uOpacity;

float3 uVoidColor;
float3 uNebulaLow;
float3 uNebulaHigh;
float3 uStarColor;

float N(float2 p)
{
    return tex2D(NoiseSampler, p).r;
}

// Four octaves, recentred on the bound texture's median and stretched back out: averaging octaves
// collapses the histogram into a band far too narrow for uDensity / uContrast to be 0..1 dials.
float Fbm(float2 p)
{
    float sum = 0.0;
    float amp = 0.5;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        sum += N(p) * amp;
        p = p * 2.03 + 0.17;
        amp *= 0.5;
    }

    return saturate((sum - uPivot) * uExpand + 0.5);
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// One star per grid cell, placed and graded by the cell's own hash. A noise texture thresholded high
// gives clumps rather than isolated points, so the field is generated instead of sampled.
//
// The falloff is deliberately wide: the backdrop is flattened onto a pixel grid afterwards, and a star
// narrower than a cell would blink in and out of it as the field pans.
float Stars(float2 p)
{
    float2 cell = floor(p);
    float pick = Hash(cell);

    float bright = saturate((pick - uStarCut) / max(1.0 - uStarCut, 1e-4));

    float2 at = float2(Hash(cell + 11.3), Hash(cell + 27.7));
    float d = length(p - cell - at);
    float star = pow(saturate(1.0 - d * 2.0), max(uStarPower, 1.0)) * bright;

    return star * (1.0 - uTwinkle * 0.5 * (1.0 + sin(uTime * (1.5 + pick * 6.0) + pick * 30.0)));
}

// Squared falloff kernel, zero outside the radius. Summing these and thresholding the total is what
// makes two nearby blobs bulge into one surface instead of overlapping as discs.
float BlobWeight(float4 blob, float2 d)
{
    float2 t = (d - blob.xy) / max(blob.z, 1e-4);
    float k = saturate(1.0 - dot(t, t));

    return k * k * blob.w;
}

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 d = (uv - 0.5) * uAspect;
    float2 n = d + uPan;
    float r = length(n);

    // Shear grows toward the centre so the arms wind tighter there; uSpin keeps the far field, where
    // 1/r has bottomed out, from standing completely still.
    float twist = uSwirl / (r + uSwirlCore) + uTime * uSpin;
    float ca = cos(twist);
    float sa = sin(twist);
    float2 q = float2(n.x * ca - n.y * sa, n.x * sa + n.y * ca);

    float2 p = q * uScale + float2(0.013, -0.009) * uTime;

    // Two decorrelated low-frequency taps bend whole billows rather than shredding them.
    float2 w = float2(N(p * 0.23 + uTime * 0.011), N(p * 0.23 - uTime * 0.009 + 4.19));
    p += (w - 0.5) * uWarp;

    float h = Fbm(p);
    float soft = max(uContrast, 0.001);
    float body = smoothstep(uDensity - soft, uDensity + soft, h);

    // Two unswirled layers, so the sky sits still behind the moving gas and has some depth to it.
    float star = Stars(n * uStarScale) + Stars(n * uStarScale * 1.45 + 31.7) * 0.5;

    // Gas in front of a star dims it; without this the field reads as a flat sheet of dots.
    star *= 1.0 - body * 0.65;

    float w0 = BlobWeight(uBlob0, d);
    float w1 = BlobWeight(uBlob1, d);
    float w2 = BlobWeight(uBlob2, d);
    float w3 = BlobWeight(uBlob3, d);

    float field = w0 + w1 + w2 + w3;
    float3 blobColor = (uBlobColor0 * w0 + uBlobColor1 * w1 + uBlobColor2 * w2 + uBlobColor3 * w3)
        / max(field, 1e-4);

    float core = smoothstep(uBlobFloor - uBlobSoft, uBlobFloor + uBlobSoft, field);
    float halo = saturate(field);

    float3 color = lerp(uVoidColor, uNebulaLow, saturate(h * 1.4));
    color = lerp(color, uNebulaHigh, body);
    color += uStarColor * star;

    // The gas the blob sits in lights up with it, so a held node reads as illuminating the nebula
    // rather than as a sprite laid over it.
    color += blobColor * (halo * 0.35 + core * 0.75) * (0.45 + 0.55 * h);

    float vigR = length((uv - 0.5) * uAspect) / max(0.5 * length(uAspect), 1e-4);
    color *= 1.0 - uVignette * smoothstep(0.35, 1.0, vigR);

    float alpha = uOpacity * col.a * tex2D(ImageSampler, uv).a;

    // Premultiplied, to composite in the UI's AlphaBlend batch.
    return float4(color * alpha, alpha);
}

technique SkillNebula
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
