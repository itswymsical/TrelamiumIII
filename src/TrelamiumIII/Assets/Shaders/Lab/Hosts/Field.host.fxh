// A FieldLayer's composite: the same field and attribute targets, and the same frame uniforms, GasFbm.fx reads.
// FieldLayer.ApplyScreenParameters and FieldTargetContent.ApplyGrid set those by name, so these names can't change.
//
// Every channel on both targets holds coverage * value, recovered by dividing against coverage. See FieldStamp.
//
//@host Field
//@provides world flow heat age bind variant light density neighbours

sampler2D FieldSampler : register(s0);

texture2D uAttrTex;
sampler2D AttrSampler = sampler_state
{
    Texture = <uAttrTex>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
};

float Time;
float2 FieldTexel;
float2 WorldOffset;
float Aspect;
float2 Zoom;
float FieldGain;

float HostDensity(float2 uv)
{
    float f = tex2Dlod(FieldSampler, float4(uv, 0.0, 0.0)).r * FieldGain;
    return f * f;
}

//@stages

float4 LabPass(float2 raw : TEXCOORD0) : COLOR0
{
    Px px = (Px)0;

    px.uv = (floor(raw / FieldTexel) + 0.5) * FieldTexel;
    px.texel = FieldTexel;

    float2 aspect = float2(Aspect, 1.0);
    float2 halfExtent = aspect * 0.5;

    px.world = (px.uv * aspect - halfExtent) / Zoom + halfExtent + WorldOffset;
    px.p = px.world;

    float4 field = tex2Dlod(FieldSampler, float4(px.uv, 0.0, 0.0));
    float4 attr = tex2Dlod(AttrSampler, float4(px.uv, 0.0, 0.0));
    float coverage = max(field.r, 0.0001);

    px.density = HostDensity(px.uv);
    px.light = (field.g / coverage).xxx;
    px.heat = field.b / coverage;
    px.age = field.a / coverage;
    px.flow = attr.rg / coverage * 2.0 - 1.0;
    px.bind = attr.b / coverage;
    px.variant = attr.a / coverage;

    //@calls

    return float4(px.color.rgb * px.light * px.color.a, px.color.a);
}

technique Lab
{
    pass P0
    {
        PixelShader = compile ps_3_0 LabPass();
    }
}
