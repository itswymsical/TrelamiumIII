// ForceFieldShader.fx
// Magnetic force field with genuine 3D sphere illusion.
//
// The depth reads as 3D through three layered techniques:
//   1. Normal reconstruction  treating the 2D circle as a sphere surface
//   2. Fresnel rim  bright where the surface curves away, dark at center
//   3. Dipole field lines  arcing from pole to pole like a real magnetic field

sampler2D uImage0 : register(s0);

float2 uPlayerPosition;
float2 uScreenResolution;
float uTime;
float uFieldRadius;

// Field line density  higher = more lines arcing across the sphere
float uRippleFrequency;

// How sharp the Fresnel rim falloff is  higher = tighter rim glow
float uFalloffPower;

// Pulse animation speed and intensity
float uPulseSpeed;
float uPulseIntensity;

float uBaseOpacity;
float uOpacity;
float4 uColor;
float2 uTextureScroll; // unused now but kept for API compat

float4 ForceFieldPass(float2 uv : TEXCOORD0) : COLOR0
{
    float2 screenPos = uv * uScreenResolution;
    float2 offset    = screenPos - uPlayerPosition;
    float  dist      = length(offset);

    // Outside the field  fully transparent
    if (dist >= uFieldRadius)
        discard;

    float norm = dist / uFieldRadius; // 0 at center, 1 at edge

    // --- Sphere normal reconstruction ---
    // Treat the circle as the projection of a sphere.
    // Z depth from the sphere equation gives us a proper surface normal.
    float  sphereZ  = sqrt(max(0.0, 1.0 - norm * norm));
    float2 normDir  = (norm > 0.001) ? normalize(offset) : float2(0, 0);
    float3 normal   = normalize(float3(normDir * norm, sphereZ));

    // Gives the surface a consistent shading direction.
    float3 lightDir = normalize(float3(0.4, -0.6, 0.8));
    float  diffuse  = saturate(dot(normal, lightDir));

    // --- Fresnel rim ---
    // View direction is straight into the screen (0, 0, 1).
    // Fresnel is strong where the normal faces away from the viewer 
    // exactly at the sphere's silhouette edge.
    float3 viewDir  = float3(0.0, 0.0, 1.0);
    float  fresnel  = pow(1.0 - saturate(dot(normal, viewDir)), uFalloffPower);

    // --- Magnetic dipole field lines ---
    // Real dipole field lines follow: r = sin²(θ) in polar coords.
    // We approximate this by modulating along the polar angle from the
    // vertical axis, creating arcs that flow from top pole to bottom pole.
    float  theta        = atan2(offset.x, -offset.y);               // angle from top pole
    float  phi          = acos(saturate(dot(normal, float3(0,1,0)))); // colatitude on sphere
    float  fieldLine    = sin(theta * uRippleFrequency + uTime * 1.2);
    float  fieldRing    = sin(phi   * uRippleFrequency - uTime * uPulseSpeed);
    float  dipole       = saturate(fieldLine * fieldRing * 0.5 + 0.5);
    dipole              = pow(dipole, 2.0); // sharpen the lines

    // --- Equatorial energy ring ---
    // The equator of a magnetic field is always the most energetic region.
    // A bright band around the middle reinforces the 3D sphere reading.
    float equator = 1.0 - abs(dot(normal, float3(0, 1, 0)));
    equator       = pow(equator, 4.0);

    // --- Pulse ---
    // Global brightness breathes in and out.
    float pulse = 1.0 + sin(uTime * uPulseSpeed * 0.5) * uPulseIntensity;

    // --- Compose intensity ---
    // Fresnel drives the overall shape  bright rim, transparent center.
    // Dipole lines and equatorial ring add detail on top.
    // Diffuse shading gives one side slightly more presence than the other.
    float intensity = (fresnel * 0.6 + dipole * 0.25 + equator * 0.3)
                    * (0.6 + diffuse * 0.4)
                    * pulse;

    // --- Color ---
    // Inner sphere (low fresnel): the dominant field color, tinted darker
    // Rim (high fresnel): brighter, pushed toward white for the glow quality
    float3 baseColor = uColor.rgb;
    float3 rimColor  = lerp(baseColor, float3(1.0, 1.0, 1.0), 0.5);
    float3 color     = lerp(baseColor * 0.4, rimColor, fresnel);

    // Dipole lines shift slightly toward white where they peak
    color = lerp(color, float3(1.0, 1.0, 1.0), dipole * 0.3);

    // Equatorial ring brightens toward the field color
    color = lerp(color, baseColor * 2.0, equator * 0.4);

    // Edge fade  alpha falls off sharply at the boundary so there's
    // no hard circle cutoff, just a soft atmospheric edge
    float edgeFade = smoothstep(1.0, 0.85, norm);
    float alpha    = intensity * edgeFade * uOpacity;

    return float4(color * alpha, alpha);
}

technique ForceField
{
    pass P0
    {
        PixelShader = compile ps_3_0 ForceFieldPass();
    }
}
