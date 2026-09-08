using System.Collections.Generic;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     A conversation in flight, lifted out of one box so another can pick it up where it stood.
/// </summary>
/// <remarks>
///     <para>
///         This is everything a presenter owns that a player would notice going missing: which line is
///         being said, how much of it has arrived, what is queued behind it, and which response is
///         highlighted. Deliberately not the presentation -- no sizes, no angles, no timers -- because
///         the box receiving this is going to do all of that differently.
///     </para>
///     <para>
///         Handing state over rather than running both boxes in parallel and cross-fading them. Two
///         typewriters fed the same script would stay in step only for as long as nothing touched
///         either of them, and the first click, skip or affect-driven pause would desynchronise the
///         two halves of what is supposed to be one conversation.
///     </para>
/// </remarks>
public sealed class DialogueHandoff
{
    public required string Key { get; init; }

    /// <summary>The line being spoken, or null if the conversation has run out.</summary>
    public required DialogueData? Current { get; init; }

    /// <summary>Lines still queued behind <see cref="Current" />.</summary>
    public required IReadOnlyList<DialogueData> Remaining { get; init; }

    /// <summary>How many characters of <see cref="Current" /> have been revealed.</summary>
    public required int CharIndex { get; init; }

    public required bool AwaitingChoice { get; init; }

    public required int SelectedChoice { get; init; }

    public required bool ZoomIn { get; init; }

    /// <summary>Whether the speaker is held facing the player for the conversation.</summary>
    public required bool Roots { get; init; }
}
