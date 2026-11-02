using System.Collections.Generic;

namespace TrelamiumIII.Content.DevTools.VfxLab.Generation;

/// <summary> When a stage runs. Recipes reorder stages within a phase, never across one, which rules out most nonsense stacks. </summary>
internal enum StagePhase
{
    /// <summary> Where the noise looks: warp, flow. </summary>
    Coords,

    /// <summary> What eats at the material's edge: noise layers. </summary>
    Shape,

    /// <summary> Where the material exists. </summary>
    Cut,

    /// <summary> What the body looks like: shading, colour, light. </summary>
    Shade,

    /// <summary> What its edge looks like, over the body. </summary>
    Edge,

    /// <summary> Last word on the pixel: debug views. </summary>
    Finish,
}

internal enum ParamKind
{
    Float,
    Float2,

    /// <summary> Linear rgb, a <c>float3</c> in the shader. </summary>
    Color,

    /// <summary> Written into the source as a literal, so changing it recompiles. Integer uniforms don't survive every shader backend. </summary>
    Int,

    /// <summary> A tiling texture, and the wrapping sampler stages read it through. </summary>
    Noise,
}

/// <param name="Default"> The numbers for a numeric kind, and empty for a texture. </param>
/// <param name="DefaultTexture"> The texture for a <see cref="ParamKind.Noise" />. </param>
/// <param name="Min"> What the inspector offers. Only <see cref="ParamKind.Int" /> is held to it, as an octave count is a loop in the shader. </param>
internal sealed record ParamDefinition(string Name, ParamKind Kind, float[] Default, string? DefaultTexture, float Min, float Max);

/// <summary> One <c>.stage.fxh</c>: a step of the shader, and the parameters the inspector and the recipe know it by. </summary>
/// <param name="Body"> The HLSL, with <c>$Stage</c> and <c>$Param</c> tokens still in it. </param>
/// <param name="Source"> The file, as compile errors should name it. </param>
/// <param name="BodyLine"> The file's line that <paramref name="Body" /> starts on. </param>
internal sealed record StageDefinition(
    string Name,
    StagePhase Phase,
    IReadOnlySet<string> Needs,
    IReadOnlyList<ParamDefinition> Params,
    string Body,
    string Source,
    int BodyLine);

/// <summary> One <c>.host.fxh</c>: the shader around the stages, for one kind of thing a shader can draw on. </summary>
/// <param name="Name"> What a recipe's <c>surface</c> names. </param>
/// <param name="Provides"> What stages on this host may ask for with <c>needs=</c>. </param>
/// <param name="Before"> Up to the stages: the frame uniforms and helpers. </param>
/// <param name="Between"> From the stages to where they're called. </param>
/// <param name="After"> From where they're called to the technique. </param>
/// <param name="SamplerCount"> The samplers the template declares itself, against the limit of sixteen. </param>
internal sealed record HostDefinition(
    string Name,
    IReadOnlySet<string> Provides,
    string Before,
    int BeforeLine,
    string Between,
    int BetweenLine,
    string After,
    int AfterLine,
    string Source,
    int SamplerCount);
