namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///   A resolved binding of a <see cref="DialogueAffect"/> to a character span of a line.
///   These are produced by <see cref="DialogueBuilder"/> from the bracketed anchors in the
///   localized text: authors don't construct them directly. The half-open range
///   [<see cref="StartIndex"/>, <see cref="EndIndex"/>) is the set of glyphs the affect covers.
/// </summary>
public readonly struct LineModifier
{
    public int StartIndex { get; }
    public int EndIndex { get; }
    public DialogueAffect Affect { get; }

    public LineModifier(int startIndex, int endIndex, DialogueAffect affect)
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
        Affect = affect;
    }

    /// <summary>True when this binding's span covers <paramref name="index"/>.</summary>
    public bool Covers(int index) => index >= StartIndex && index < EndIndex;

    /// <summary>Runs the affect's reveal pass with this binding's span in context.</summary>
    public void ApplyReveal(DialogueTick tick)
    {
        tick.BeginSpan(StartIndex, EndIndex);
        Affect.OnReveal(tick);
    }

    /// <summary>Runs the affect's draw pass with this binding's span in context.</summary>
    public void ApplyDraw(DialogueRenderContext render)
    {
        render.BeginSpan(StartIndex, EndIndex);
        Affect.OnDraw(render);
    }
}
