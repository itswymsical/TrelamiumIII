namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     What the <see cref="DialogueManager" /> needs from a box, whichever box it opened.
/// </summary>
/// <remarks>
///     The manager owns one conversation at a time and knows nothing about how it looks -- queueing,
///     music, letterboxing and zoom are the same work however the line is drawn. This is the seam
///     that lets another presentation exist without any of that being written twice.
/// </remarks>
public interface IDialoguePresenter
{
    string DialogueKey { get; }

    /// <summary>Whether the camera pulls in for the duration.</summary>
    bool ShouldZoom { get; }

    /// <summary>True once the box has finished its closing animation and may be dropped.</summary>
    bool ShouldBeRemoved { get; }

    /// <summary>
    ///     True when the box was seen through to its end -- its last line advanced past, or a response
    ///     confirmed -- rather than dismissed or cut off. Decides whether the conversation completes.
    /// </summary>
    bool Completed { get; }

    /// <summary>
    ///     The response that ended the box, or null if it ended on a plain line. The box only records
    ///     it: opening the branch is the manager's job, so the branch inherits the whole conversation.
    /// </summary>
    ChoiceData? Chosen { get; }

    void Update();

    /// <param name="bottomAnchorPosition">
    ///     Where the notification stack has room along the bottom of the screen. A box that places
    ///     itself in the world is free to ignore it.
    /// </param>
    void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition);

    /// <summary>Begins closing. Idempotent.</summary>
    void Close();
}
