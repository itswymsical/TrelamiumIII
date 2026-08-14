using System;
using System.Collections.Generic;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///  A sequence of dialogue lines, defined in code. Each line's text is pulled from the localization.
/// </summary>
public sealed class DialogueSequence
{
    public string Key { get; }
    public IReadOnlyList<LineSpec> Lines { get; }

    public DialogueSequence(string key, IReadOnlyList<LineSpec> lines)
    {
        Key = key;
        Lines = lines;
    }
}

/// <summary>
///   One line of a <see cref="DialogueSequence"/>. The text is pulled from
///   <c>{SequenceKey}.Line{N}.Text</c>, where N is the 1-based position in the sequence.
/// </summary>
public sealed class LineSpec
{
    public string SpeakerId { get; init; } = "Unknown";
    public int Emote { get; init; }
    public float Speed { get; init; } = 1f;

    /// <summary>
    ///   Affects bound to the line's bracketed anchors, in order of appearance. A <c>[]</c> point
    ///   anchor pairs with a point affect (e.g. <see cref="Pause"/>); a <c>[span]</c> anchor pairs
    ///   with a span affect (e.g. <see cref="Shake"/>, <see cref="Wave"/>, <see cref="Slow"/>).
    /// </summary>
    public IReadOnlyList<DialogueAffect> Affects { get; init; } = [];

    /// <summary>Response options shown after the text finishes. Each can branch, run a callback, or both.</summary>
    public IReadOnlyList<ChoiceSpec> Choices { get; init; } = [];
}

/// <summary>
///   A response option. Its label is resolved from <c>{SequenceKey}.Line{N}.Choice{M}.Text</c>;
///   the branch and/or callback are defined here in code.
/// </summary>
public sealed class ChoiceSpec
{
    /// <summary>Dialogue key to start when chosen. May be null for a callback-only choice.</summary>
    public string BranchKey { get; init; }

    /// <summary>Callback run when chosen, before any branch starts.</summary>
    public Action OnSelected { get; init; }
}
