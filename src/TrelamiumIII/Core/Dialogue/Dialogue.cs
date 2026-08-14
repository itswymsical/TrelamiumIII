using System;
using System.Collections.Generic;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///   Builder for a <see cref="DialogueSequence"/>. Lines auto-number to match the
///   localization keys (the first <see cref="Line"/> maps to <c>Line1</c>, and so on); each
///   <see cref="Choice"/> attaches to the most recent line and maps to <c>Choice1</c>, <c>Choice2</c>, ...
///   <code>
///   Dialogue.Define("LostKeep.Answer")
///       .Line("Guide", emote: 1, speed: 1.1f)
///       .Line("Guide", emote: 0, speed: 1.1f)
///           .Choice(branch: "LostKeep.Answer_Lie")
///           .Choice(onSelected: () => Flag = true)
///       .Register();
///   </code>
/// </summary>
public sealed class Dialogue
{
    private readonly string key;
    private readonly List<LineSpec> lines = [];

    // The line currently being assembled, held open so Choice() can attach before the next Line().
    private string pendingSpeaker;
    private int pendingEmote;
    private float pendingSpeed;
    private DialogueAffect[] pendingAffects;
    private List<ChoiceSpec> pendingChoices;
    private bool hasPending;

    private Dialogue(string key) => this.key = key;

    /// <summary>Begins a sequence for the given key, e.g. "JourneysBegin.ChoiceTest".</summary>
    public static Dialogue Define(string key) => new(key);

    /// <summary>
    ///   Adds a line. Text comes from <c>{Key}.Line{N}.Text</c>; <paramref name="affects"/> bind to that
    ///   line's bracketed anchors (<c>[]</c> / <c>[span]</c>) in order of appearance.
    /// </summary>
    public Dialogue Line(string speaker, int emote = 0, float speed = 1f, params DialogueAffect[] affects)
    {
        Flush();
        pendingSpeaker = speaker;
        pendingEmote = emote;
        pendingSpeed = speed;
        pendingAffects = affects;
        pendingChoices = null;
        hasPending = true;
        return this;
    }

    /// <summary>Adds a response option to the most recent line. Provide a branch, a callback, or both.</summary>
    public Dialogue Choice(string branch = null, Action onSelected = null)
    {
        if (!hasPending)
            throw new InvalidOperationException($"Dialogue '{key}': Choice() called before any Line().");

        pendingChoices ??= [];
        pendingChoices.Add(new ChoiceSpec { BranchKey = branch, OnSelected = onSelected });
        return this;
    }

    private void Flush()
    {
        if (!hasPending)
            return;

        lines.Add(new LineSpec
        {
            SpeakerId = pendingSpeaker,
            Emote = pendingEmote,
            Speed = pendingSpeed,
            Affects = pendingAffects is { Length: > 0 } ? pendingAffects : [],
            Choices = pendingChoices is { Count: > 0 } ? pendingChoices : []
        });
        hasPending = false;
    }

    /// <summary>Finalizes the sequence without registering it (for inline or one-off use).</summary>
    public DialogueSequence Build()
    {
        Flush();
        return new DialogueSequence(key, lines);
    }

    /// <summary>Finalizes the sequence and adds it to the <see cref="DialogueRegistry"/>.</summary>
    public DialogueSequence Register()
    {
        var sequence = Build();
        DialogueRegistry.Add(sequence);
        return sequence;
    }
}
