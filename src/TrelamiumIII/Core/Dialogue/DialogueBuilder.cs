using System;
using System.Collections.Generic;
using System.Text;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///  Builds display-ready <see cref="DialogueData"/> from a <see cref="DialogueSequence"/>. This involves:
///  <para>
///  - Resolving localized text for each line.
///  </para>
///  <para>
///  - Processing bracketed anchors (e.g., [speakerName:X], [span], [])
///  </para>
///  <para>
///  - Binding affects to anchors.
///  </para>
///  <para>
///  - Building choice options.
///  </para>
/// </summary>
public static class DialogueBuilder
{
    private const string SpeakerNamePrefix = "speakerName:";

    /// <summary>Resolved text of a sequence's first line, for previews/indicators. Empty if none.</summary>
    public static string GetPreviewText(string dialogueKey)
    {
        var sequence = DialogueRegistry.Get(dialogueKey);
        if (sequence == null || sequence.Lines.Count == 0)
            return string.Empty;

        return BuildLine(sequence.Key, 1, sequence.Lines[0])?.PlainText ?? string.Empty;
    }

    public static List<DialogueData> BuildSequence(string dialogueKey)
    {
        var sequence = DialogueRegistry.Get(dialogueKey);
        if (sequence == null)
            return [];

        var dialogueLines = new List<DialogueData>();
        for (int i = 0; i < sequence.Lines.Count; i++)
        {
            var lineData = BuildLine(sequence.Key, i + 1, sequence.Lines[i]);
            if (lineData != null)
                dialogueLines.Add(lineData);
        }

        if (GetOptionalValue($"DialogueLibrary.{sequence.Key}.Line{sequence.Lines.Count + 1}.Text", null) != null)
            Warn($"Dialogue '{sequence.Key}': the localization has more lines than the {sequence.Lines.Count} defined in code - the rest won't play.");

        return dialogueLines;
    }

    private static DialogueData BuildLine(string dialogueKey, int lineNumber, LineSpec spec)
    {
        string baseKey = $"DialogueLibrary.{dialogueKey}.Line{lineNumber}";

        string raw = GetOptionalValue($"{baseKey}.Text", string.Empty);

        if (raw.Length == 0)
            Warn($"Dialogue '{dialogueKey}.Line{lineNumber}': no text in the localization - it will play as an empty box.");

        var (text, anchors) = ProcessText(raw);
        var modifiers = BindAffects(anchors, spec.Affects, dialogueKey, lineNumber);

        var choices = BuildChoices(baseKey, spec.Choices);
        var speaker = SpeakerRegistry.Get(spec.SpeakerId);

        return new DialogueData
        {
            PlainText = text,
            Modifiers = modifiers,
            Speaker = ResolveName(spec.SpeakerId),
            SpeakerType = spec.SpeakerId,
            Profile = speaker,
            IsPlayer = SpeakerRegistry.IsPlayer(spec.SpeakerId),
            SpeakerColor = speaker.Color,
            Emote = spec.Emote,
            Speed = spec.Speed,
            BaseDelay = 3,
            TextColor = Color.White,
            BackgroundColor = speaker.Color * 0.5f,
            Choices = choices
        };
    }

    private static List<ChoiceData> BuildChoices(string baseKey, IReadOnlyList<ChoiceSpec> specs)
    {
        var choices = new List<ChoiceData>();

        for (int i = 0; i < specs.Count; i++)
        {
            string raw = GetOptionalValue($"{baseKey}.Choice{i + 1}.Text", null);
            if (raw == null)
            {
                Warn($"Dialogue '{baseKey}': choice {i + 1} has no text in the localization - it won't be offered.");
                continue;
            }

            choices.Add(new ChoiceData
            {
                Text = ProcessText(raw).text,
                BranchKey = specs[i].BranchKey ?? string.Empty,
                OnSelected = specs[i].OnSelected,
                Dead = specs[i].Dead
            });
        }

        if (GetOptionalValue($"{baseKey}.Choice{specs.Count + 1}.Text", null) != null)
            Warn($"Dialogue '{baseKey}': the localization has more choices than the {specs.Count} defined on this line in code - the rest won't be offered.");

        return choices;
    }

    private static void Warn(string message) => TrelamiumIII.Instance?.Logger?.Warn(message);

    /// <summary>
    /// Processes raw text with bracketed anchors. For example, "Hello [speakerName:Merchant]!" becomes "Hello Bob!" if the merchant's name is Bob.
    /// <para>
    /// Anchors can also be empty point anchors (e.g. "Wait for it...[]") or span anchors (e.g. "This is a [span].").
    /// The caller binds affects to anchors in order of appearance.
    /// </para>
    /// </summary>
    /// <param name="raw"></param>
    /// <returns></returns>
    private static (string text, List<(int start, int len)> anchors) ProcessText(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var anchors = new List<(int start, int len)>();

        int i = 0;
        while (i < raw.Length)
        {
            char c = raw[i];
            if (c != '[')
            {
                sb.Append(c);
                i++;
                continue;
            }

            int close = raw.IndexOf(']', i + 1);
            if (close < 0)
            {
                // Unmatched bracket = treat as literal text.
                sb.Append(c);
                i++;
                continue;
            }

            string inner = raw[(i + 1)..close];
            if (inner.StartsWith(SpeakerNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(ResolveSpeakerName(inner[SpeakerNamePrefix.Length..].Trim()));
            }
            else
            {
                int start = sb.Length;
                sb.Append(inner);
                anchors.Add((start, sb.Length - start));
            }

            i = close + 1;
        }

        return (sb.ToString(), anchors);
    }

    /// <summary>Pairs anchors with affects in order. A point anchor covers the preceding glyph so it fires even at end of line.</summary>
    private static List<LineModifier> BindAffects(List<(int start, int len)> anchors, IReadOnlyList<DialogueAffect> affects, string key, int lineNumber)
    {
        var modifiers = new List<LineModifier>();
        int count = Math.Min(anchors.Count, affects.Count);

        for (int i = 0; i < count; i++)
        {
            var (start, len) = anchors[i];
            int rStart = len == 0 ? Math.Max(0, start - 1) : start;
            int rEnd = len == 0 ? start : start + len;
            modifiers.Add(new LineModifier(rStart, rEnd, affects[i]));
        }

        if (anchors.Count != affects.Count)
            TrelamiumIII.Instance?.Logger?.Warn(
                $"Dialogue '{key}.Line{lineNumber}': {anchors.Count} anchor(s) but {affects.Count} affect(s) - binding {count}.");

        return modifiers;
    }

    private static string ResolveSpeakerName(string targetType)
    {
        if (targetType.Equals("player", StringComparison.OrdinalIgnoreCase)
            || targetType.Equals("playername", StringComparison.OrdinalIgnoreCase))
            return Main.LocalPlayer.name;

        return ResolveName(targetType);
    }

    private static string ResolveName(string speakerType)
    {
        var npc = FindNPCName(speakerType);

        if (npc != null && !string.IsNullOrEmpty(npc.GivenName) && npc.GivenName != npc.TypeName)
            return npc.GivenName;

        return speakerType;
    }

    private static NPC FindNPCName(string typeName)
    {
        string lowerTypeName = typeName.ToLower();

        for (int i = 0; i < Main.npc.Length; i++)
        {
            var npc = Main.npc[i];
            if (npc.active && !string.IsNullOrEmpty(npc.TypeName))
            {
                if (npc.TypeName.ToLower() == lowerTypeName)
                    return npc;
            }
        }

        return null;
    }

    private static string GetOptionalValue(string key, string defaultValue)
    {
        try
        {
            var localization = TrelamiumIII.Instance.GetLocalization(key);
            return (!string.IsNullOrEmpty(localization.Value) && localization.Value != localization.Key)
                ? localization.Value
                : defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }
}

public class ChoiceData
{
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// The dialogue key to start when this choice is selected,
    /// e.g. "JourneysBegin.AdventureResponse". May be empty for a callback-only choice.
    /// </summary>
    public string BranchKey { get; init; } = string.Empty;

    /// <summary>
    /// Optional callback run when this choice is selected, before any <see cref="BranchKey"/>
    /// branch starts. A choice may branch, run a callback, or both.
    /// </summary>
    public Action OnSelected { get; init; }

    /// <summary>
    /// When chosen, the conversation's pending completion actions are discarded -- this is the answer
    /// that means "no". See <see cref="Dialogue.Choice"/>.
    /// </summary>
    public bool Dead { get; init; }
}

public class DialogueData
{
    public string PlainText { get; init; } = string.Empty;

    /// <summary>Affects bound to character spans of <see cref="PlainText"/> (resolved from anchors).</summary>
    public IReadOnlyList<LineModifier> Modifiers { get; init; } = [];

    public string Speaker { get; init; } = string.Empty;
    public string SpeakerType { get; init; } = string.Empty;
    public Color SpeakerColor { get; init; } = Color.White;
    public int Emote { get; init; }
    public float Speed { get; init; } = 1.0f;
    public int BaseDelay { get; init; } = 3;
    public Color TextColor { get; init; } = Color.White;
    public Color BackgroundColor { get; init; } = Color.Black;

    /// <summary>Drives portrait, frame count, color, and voice.</summary>
    public ISpeakerProfile Profile { get; init; } = SpeakerRegistry.Default;

    /// <summary>True when this line is spoken by the local player (no portrait frame is drawn).</summary>
    public bool IsPlayer { get; init; }

    /// <summary>
    /// When non-empty, normal dialogue advance is suppressed after text
    /// finishes and the player is presented with these response options.
    /// </summary>
    public List<ChoiceData> Choices { get; init; } = new();
    public bool HasChoices => Choices.Count > 0;
}
