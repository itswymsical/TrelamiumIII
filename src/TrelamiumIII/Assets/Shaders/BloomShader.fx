// Single-pass approximate bloom for SpriteBatch.
// 5-tap (cross) Gaussian-ish blur composited additively over the original.
// Best applied to a render target whose alpha mask already isolates the
// thing you want to bloom (e.g. title text drawn on a transparent texture).
//
// uTexelSize = (1 / sourceWidth, 1 / sourceHeight)
// uIntensity = bloom strength multiplier
// uTint      = color tint for the bloom contribution

sampler uImage0 : register(s0);

float2 uTexelSize;
float uIntensity;
float3 uTint;

float4 PixelShaderFunction(float4 col : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uTexelSize;

    float4 c = tex2D(uImage0, uv);
    float4 l = tex2D(uImage0, uv + float2(-px.x, 0));
    float4 r = tex2D(uImage0, uv + float2( px.x, 0));
    float4 u = tex2D(uImage0, uv + float2(0, -px.y));
    float4 d = tex2D(uImage0, uv + float2(0,  px.y));

    // Centre weight 2, edges 1
    // total = 6.
    float4 blur = (c * 2.0 + l + r + u + d) / 6.0;

    float4 bloom = float4(blur.rgb * uTint, blur.a) * uIntensity;
    return (c + bloom) * col;
}

technique Technique1
{
    pass BloomPass
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
