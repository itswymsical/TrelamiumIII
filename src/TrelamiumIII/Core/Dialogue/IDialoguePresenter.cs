namespace TrelamiumIII.Core.Dialogue;

/// <summary>How a line of dialogue is put on screen.</summary>
public enum DialogueStyle
{
    /// <summary>The anchored panel with a portrait frame, along the bottom of the screen.</summary>
    Panel,

    /// <summary>The pixelated bubble projected in perspective over its speaker.</summary>
    Projected,
}

/// <summary>
///     What the <see cref="DialogueManager" /> needs from a box, whichever box it opened.
/// </summary>
/// <remarks>
///     The manager owns one conversation at a time and knows nothing about how it looks -- queueing,
///     music, letterboxing and zoom are the same work whether the line is in a portrait panel or a
///     bubble hanging over the speaker's head. This is the seam that lets a second presentation exist
///     without any of that being written twice.
/// </remarks>
public interface IDialoguePresenter
{
    string DialogueKey { get; }

    /// <summary>Whether the camera pulls in for the duration.</summary>
    bool ShouldZoom { get; }

    /// <summary>True once the box has finished its closing animation and may be dropped.</summary>
    bool ShouldBeRemoved { get; }

    void Update();

    /// <param name="bottomAnchorPosition">
    ///     Where the notification stack has room along the bottom of the screen. A box that places
    ///     itself in the world is free to ignore it.
    /// </param>
    void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition);

    /// <summary>Begins closing. Idempotent.</summary>
    void Close();
}
