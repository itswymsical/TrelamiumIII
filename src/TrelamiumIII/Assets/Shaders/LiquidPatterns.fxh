// Liquid patterns shared by LiquidSurface.fx (world) and BottleLiquid.fx (item sprites).
// w is a position in pixels; noise is a wrapping noise texture read in its red channel; scale 1 is the world size.

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// Thin salt strands: iso-lines through two warped noise layers, stretched flat, drifting slowly and broken up by
// a mask so they read as strands rather than contours, with the odd glint of a crystal. Returns coverage, 0-1.
float BrineStrands(sampler2D noise, float2 w, float time, float speed, float scale)
{
    float t = time * speed;
    float2 p = w / (scale * float2(900.0, 420.0));

    float2 warp = float2(tex2D(noise, p * 0.5 + float2(t * 0.020, 0.0)).r, tex2D(noise, p * 0.5 + float2(0.31, t * 0.015)).r) - 0.5;
    float n1 = tex2D(noise, p + warp * 0.35 + float2(t * 0.010, 0.0)).r;
    float n2 = tex2D(noise, p * 1.9 + warp * 0.5 + float2(-t * 0.013, 0.17)).r;
    float mask = smoothstep(0.42, 0.62, tex2D(noise, p * 0.8 + float2(0.71 - t * 0.008, 0.13)).r);

    float s1 = 1.0 - saturate(abs(n1 - 0.5) * 22.0);
    float s2 = 1.0 - saturate(abs(n2 - 0.5) * 28.0);
    float strand = max(s1 * s1, s2 * s2 * 0.6) * (0.2 + 0.8 * mask);

    float glint = step(0.9975, Hash(w + floor(time * 1.5)));

    return saturate(strand + glint);
}
