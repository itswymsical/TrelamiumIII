using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Core.Dialogue;

public sealed class AdaptiveDialogueBox : IDialoguePresenter
{
    /// <summary>Inside this, in tiles, the conversation moves to the projected bubble.</summary>
    private const float NEAR_TILES = 20f;

    /// <summary>Outside this, in tiles, it moves to the panel. Must exceed <see cref="NEAR_TILES" />.</summary>
    private const float FAR_TILES = 30f;

    private readonly DialogueAnchor requested;

    private IDialoguePresenter active;
    private IDialoguePresenter? fading;
    private bool projected;

    private AdaptiveDialogueBox(IDialoguePresenter active, bool projected, DialogueAnchor requested)
    {
        this.active = active;
        this.projected = projected;
        this.requested = requested;
    }

    /// <summary>
    ///     Opens the conversation in whichever box the speaker's current distance calls for.
    /// </summary>
    public static AdaptiveDialogueBox? Create(string dialogueKey, bool zoomIn, DialogueAnchor anchor, bool roots = true)
    {
        // Measured against the first line's speaker rather than the anchor handed in, so a dialogue
        // started from a job step opens in the right box even when nobody passed an NPC.
        DialogueData? first = DialogueBuilder.BuildSequence(dialogueKey) is { Count: > 0 } lines
            ? lines[0]
            : null;

        bool near = Near(DialogueAnchor.ForSpeaker(first, anchor), wasProjected: false);

        IDialoguePresenter? box = near
            ? ProjectedDialogueBox.Create(dialogueKey, zoomIn, anchor, roots)
            : DialogueBox.CreateDialogue(dialogueKey, zoomIn, roots);

        // A key with no lines fails in either box, so there is no second chance worth taking.
        return box is null ? null : new AdaptiveDialogueBox(box, near, anchor);
    }

    public string DialogueKey => active.DialogueKey;

    public bool ShouldZoom => active.ShouldZoom;

    public bool ShouldBeRemoved => active.ShouldBeRemoved && fading is null;

    // Never the fading box's: that one was handed off mid-conversation, not finished.
    public bool Completed => active.Completed;

    public ChoiceData? Chosen => active.Chosen;

    public void Update()
    {
        if (fading is not null)
        {
            fading.Update();

            if (fading.ShouldBeRemoved)
                fading = null;
        }

        active.Update();

        if (!active.ShouldBeRemoved)
            Reconsider();
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        // Outgoing first, so the box arriving is the one on top.
        fading?.DrawInGame(spriteBatch, bottomAnchorPosition);
        active.DrawInGame(spriteBatch, bottomAnchorPosition);
    }

    public void Close()
    {
        active.Close();
        fading?.Close();
    }

    /// <summary>Swaps presentation if the speaker has moved clear of the deadband.</summary>
    private void Reconsider()
    {
        // One swap at a time. Starting another while the last is still fading would drop the outgoing
        // box mid-animation, which is the one moment it is the only thing on screen.
        if (fading is not null)
            return;

        DialogueAnchor anchor = DialogueAnchor.ForSpeaker(CurrentLine, requested);

        if (!anchor.HasSpeaker)
            return;

        bool near = Near(anchor, projected);

        if (near == projected)
            return;

        DialogueHandoff handoff = active switch
        {
            ProjectedDialogueBox box => box.Capture(),
            DialogueBox box => box.Capture(),
            _ => throw new InvalidOperationException($"Cannot hand off from {active.GetType().Name}."),
        };

        // Nothing left to say: let the current box finish closing on its own rather than opening
        // another one just to close it too.
        if (handoff.Current is null)
            return;

        IDialoguePresenter? next = near
            ? ProjectedDialogueBox.Resume(handoff, requested)
            : DialogueBox.Resume(handoff);

        if (next is null)
            return;

        active.Close();
        fading = active;

        active = next;
        projected = near;
    }

    private DialogueData? CurrentLine => active switch
    {
        ProjectedDialogueBox box => box.CurrentLine,
        DialogueBox box => box.CurrentLine,
        _ => null,
    };

    /// <summary>
    ///     Whether the speaker is close enough for the bubble, given which box is already showing.
    /// </summary>
    /// <remarks>
    ///     Asymmetric on purpose. A bubble already up holds on until the far threshold and a panel
    ///     already up waits for the near one, so the band between them belongs to whichever arrived
    ///     first and walking along it swaps nothing.
    /// </remarks>
    private static bool Near(DialogueAnchor anchor, bool wasProjected)
    {
        if (!anchor.HasSpeaker)
            return false;

        float threshold = (wasProjected ? FAR_TILES : NEAR_TILES) * 16f;

        return Vector2.DistanceSquared(Main.LocalPlayer.Center, anchor.WorldPoint()) < threshold * threshold;
    }
}
