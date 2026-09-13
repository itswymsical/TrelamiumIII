matrix transformMatrix;

float4 bodyColor;
float4 edgeColor;
float edgeWidth;
float opacity;

texture sampleTexture;
sampler2D samplerTex = sampler_state { texture = <sampleTexture>; magfilter = LINEAR; minfilter = LINEAR; mipfilter = LINEAR; AddressU = wrap; AddressV = clamp; };

struct VertexShaderInput
{
    float4 Position : POSITION;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    output.Position = mul(input.Position, transformMatrix);
    output.TexCoords = input.TexCoords;
    output.Color = input.Color;

    return output;
}

// TexCoords.x: arc position, wrapped. TexCoords.y: 0 at the outer edge, 1 at the inner edge.
// Color.r: fade along the trail, 0 at the tail and 1 at the blade. Output is premultiplied.
float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
    float across = input.TexCoords.y;
    float inward = 1.0 - across;

    float body = inward * inward * smoothstep(0.0, 0.06, across);
    float edge = saturate(1.0 - abs(across - edgeWidth) / edgeWidth);

    float detail = tex2D(samplerTex, input.TexCoords).r;

    return (bodyColor * body + edgeColor * (edge * edge)) * (detail * input.Color.r * opacity);
}

technique Technique1
{
    pass PrimitivesPass
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
};
