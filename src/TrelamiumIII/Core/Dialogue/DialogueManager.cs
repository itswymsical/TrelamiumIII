using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;

namespace TrelamiumIII.Core.Dialogue;

public sealed class DialogueManager
{
    private static readonly DialogueManager instance = new();
    public static DialogueManager Instance => instance;

    private readonly Dictionary<string, List<string>> cachedLineKeys = new();
    private IDialoguePresenter activeDialogue = null;

    /// <summary>The conversation <see cref="activeDialogue" /> belongs to. Set whenever it is.</summary>
    private Conversation? conversation;
    private int? currentMusic = null;
    private int previousMusic = -1;
    private bool isZoomedIn = false;

    private readonly Queue<QueuedDialogue> dialogueQueue = new();

    private const float ZOOM_LEVEL = 1.55f;
    private const int ZOOM_TIME = 80;

    public bool IsUIHidden { get; private set; }

    /// <summary>True while a conversation is holding the camera pulled in. Other zooms should defer to it.</summary>
    public bool IsZoomedIn => isZoomedIn;

    public delegate void DialogueStartHandler(string dialogueKey);
    public static event DialogueStartHandler OnDialogueStart;

    public delegate void DialogueEndHandler(string dialogueKey);
    public static event DialogueEndHandler OnDialogueEnd;

    /// <param name="style">Which box the line opens in. See <see cref="DialogueStyle" />.</param>
    /// <param name="anchor">
    ///     What a projected box hangs off. Ignored by the panel box, which owns the bottom of the
    ///     screen wherever the speaker is standing.
    /// </param>
    public bool StartDialogue(
        string dialogueKey,
        bool zoomIn = false,
        bool letterbox = false,
        int? music = null,
        DialogueStyle style = DialogueStyle.Panel,
        DialogueAnchor? anchor = null,
        bool root = true)
    {
        var queuedDialogue = new QueuedDialogue
        {
            DialogueKey = dialogueKey,
            ZoomIn = zoomIn,
            Letterbox = letterbox,
            Music = music,
            Style = style,
            Anchor = anchor ?? DialogueAnchor.OnPlayer,
            Root = root
        };

        // If no dialogue is active, start immediately
        if (!IsAnyActive())
        {
            return StartDialogueInternal(queuedDialogue);
        }
        else
        {
            dialogueQueue.Enqueue(queuedDialogue);
            return true;
        }
    }

    private bool StartDialogueInternal(QueuedDialogue queuedDialogue)
    {
        Main.CloseNPCChatOrSign();

        if (queuedDialogue.Letterbox)
        {
            Letterbox.Show();
        }
        IsUIHidden = queuedDialogue.Letterbox;

        activeDialogue = Present(queuedDialogue.DialogueKey, queuedDialogue);

        if (activeDialogue == null)
        {
            if (queuedDialogue.Letterbox)
            {
                Letterbox.Hide();
                IsUIHidden = false;
            }
            return false;
        }

        conversation = new Conversation(queuedDialogue);
        conversation.Enter(queuedDialogue.DialogueKey);

        OnDialogueStart?.Invoke(queuedDialogue.DialogueKey);
        if (queuedDialogue.Music.HasValue)
        {
            previousMusic = Main.musicBox2;
            currentMusic = queuedDialogue.Music.Value;
            Main.musicBox2 = queuedDialogue.Music.Value;
        }

        return true;
    }

    /// <summary>Opens <paramref name="dialogueKey" /> in the box <paramref name="presentation" /> asks for.</summary>
    private static IDialoguePresenter? Present(string dialogueKey, QueuedDialogue presentation) => presentation.Style switch
    {
        DialogueStyle.Projected =>
            ProjectedDialogueBox.Create(dialogueKey, presentation.ZoomIn, presentation.Anchor, presentation.Root),
        DialogueStyle.Adaptive =>
            AdaptiveDialogueBox.Create(dialogueKey, presentation.ZoomIn, presentation.Anchor, presentation.Root),
        _ =>
            DialogueBox.CreateDialogue(dialogueKey, presentation.ZoomIn, presentation.Root),
    };

    /// <summary>
    ///     Carries the conversation into the branch the player chose, presented the way the
    ///     conversation was opened.
    /// </summary>
    /// <remarks>
    ///     Music, letterbox and zoom are left exactly as they stand rather than torn down and set up
    ///     again. The branch is the same conversation continuing: a restart would drop the track back
    ///     to the biome's, flash the bars, and bounce the camera out and back in between two lines.
    /// </remarks>
    private bool ContinueInto(string branchKey)
    {
        IDialoguePresenter? branch = Present(branchKey, conversation!.Opening);

        if (branch == null)
        {
            TrelamiumIII.Instance?.Logger?.Warn(
                $"Dialogue '{conversation.Opening.DialogueKey}': branch '{branchKey}' has no lines - ending the conversation there.");
            return false;
        }

        activeDialogue = branch;
        conversation.Enter(branchKey);
        OnDialogueStart?.Invoke(branchKey);

        return true;
    }

    public void Update()
    {
        Letterbox.Update();

        if (activeDialogue != null)
        {
            if (activeDialogue.ShouldBeRemoved)
            {
                EndDialogue();
            }
            else
            {
                activeDialogue.Update();
                UpdateZoom();

                if (currentMusic.HasValue)
                    Main.musicBox2 = currentMusic.Value;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        Letterbox.Draw(spriteBatch, 0.05f);

        if (activeDialogue != null)
        {
            Vector2 adjustedPosition = bottomAnchorPosition;
            activeDialogue.DrawInGame(spriteBatch, adjustedPosition);
        }
    }

    private void UpdateZoom()
    {
        if (activeDialogue != null && !isZoomedIn && activeDialogue.ShouldZoom)
        {
            ZoomHandler.SetZoomAnimation(ZOOM_LEVEL, ZOOM_TIME);
            isZoomedIn = true;
        }
        else if (activeDialogue == null && isZoomedIn)
        {
            ZoomHandler.SetZoomAnimation(1f, ZOOM_TIME);
            isZoomedIn = false;
        }
    }

    /// <summary>
    ///     Drops the box that just finished, then either carries its conversation into the branch the
    ///     player chose or brings the conversation to a close.
    /// </summary>
    private void EndDialogue()
    {
        IDialoguePresenter finished = activeDialogue;
        string completedDialogueKey = finished.DialogueKey;

        // The abandoned check covers a box that had already been answered when it was force-ended:
        // its own outcome still reads as a finish, but the conversation is not going anywhere.
        bool completed = finished.Completed && conversation is { Abandoned: false };
        ChoiceData? chosen = completed ? finished.Chosen : null;

        activeDialogue = null;

        // Fire the event after clearing activeDialogue
        if (!string.IsNullOrEmpty(completedDialogueKey))
        {
            OnDialogueEnd?.Invoke(completedDialogueKey);
        }

        if (chosen is not null)
        {
            if (chosen.Dead)
                conversation!.Kill();

            if (!string.IsNullOrEmpty(chosen.BranchKey) && ContinueInto(chosen.BranchKey))
                return;
        }

        Conversation? ended = conversation;
        conversation = null;

        Letterbox.Hide();
        IsUIHidden = false;

        if (currentMusic.HasValue)
        {
            Main.musicBox2 = previousMusic;
            currentMusic = null;
        }

        if (isZoomedIn)
        {
            ZoomHandler.SetZoomAnimation(1f, ZOOM_TIME);
            isZoomedIn = false;
        }

        // After the teardown, so an action that opens a dialogue of its own opens it clean.
        if (completed)
            ended?.Complete();

        // Check if there's a queued dialogue to start. A completion action may already have opened one.
        if (!IsAnyActive() && dialogueQueue.Count > 0)
        {
            var nextDialogue = dialogueQueue.Dequeue();
            StartDialogueInternal(nextDialogue);
        }
    }

    /// <summary>
    /// Gets the key of the currently active dialogue, or null if none
    /// </summary>
    public string GetActiveDialogueKey()
    {
        return activeDialogue?.DialogueKey;
    }

    /// <summary>
    /// Checks if a specific dialogue key is currently active: on screen, or the dialogue that opened
    /// the conversation now playing one of its branches.
    /// </summary>
    /// <remarks>
    /// The second half is what stops an NPC clicked mid-branch from queueing its whole greeting again
    /// behind the answer it is already giving.
    /// </remarks>
    public bool IsDialogueActive(string dialogueKey)
    {
        return activeDialogue?.DialogueKey == dialogueKey
            || conversation?.Opening.DialogueKey == dialogueKey;
    }

    /// <summary>
    /// Gets the number of dialogues currently in queue
    /// </summary>
    public int GetQueuedDialogueCount()
    {
        return dialogueQueue.Count;
    }

    /// <summary>
    /// Checks if a dialogue is queued (not currently active)
    /// </summary>
    public bool IsDialogueQueued(string dialogueKey)
    {
        return dialogueQueue.Any(d => d.DialogueKey == dialogueKey);
    }

    /// <summary>
    /// Clears all queued dialogues (keeps current active dialogue)
    /// </summary>
    public void ClearQueue()
    {
        dialogueQueue.Clear();
    }

    /// <summary>
    /// Force ends current dialogue and clears queue
    /// </summary>
    public void ForceEndAll()
    {
        dialogueQueue.Clear();
        if (activeDialogue != null)
        {
            // Cut off, not finished: no branch opens behind it and nothing waiting on it fires.
            conversation?.Abandon();
            activeDialogue.Close();
        }
    }

    public bool IsAnyActive() => activeDialogue != null;
    public void ClearCache() => cachedLineKeys.Clear();

    /// <summary>
    ///     A dialogue and every branch the player takes out of it, played as one piece.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         What a branch inherits from its parent lives here rather than on the box, because the box
    ///         is replaced at every branch and this is not. The opening dialogue's presentation carries
    ///         through every branch, so the conversation keeps its track, its camera and its bars; and
    ///         completion actions wait for whichever branch the conversation actually ends on, rather
    ///         than firing as the parent hands over.
    ///     </para>
    ///     <para>
    ///         Actions accumulate as the conversation passes through sequences, so a branch can add its
    ///         own on top of what its parent promised. A dead choice discards what has accumulated up to
    ///         it -- the answer that means "no" -- but not what a later branch adds after it.
    ///     </para>
    /// </remarks>
    private sealed class Conversation(QueuedDialogue opening)
    {
        /// <summary>The dialogue that opened the conversation, and the presentation every branch inherits.</summary>
        public QueuedDialogue Opening { get; } = opening;

        /// <summary>Force-ended. An abandoned conversation neither branches nor completes.</summary>
        public bool Abandoned { get; private set; }

        private Action? onComplete;

        /// <summary>Picks up the completion actions of a sequence the conversation has reached.</summary>
        public void Enter(string dialogueKey) => onComplete += DialogueRegistry.Get(dialogueKey)?.OnComplete;

        /// <summary>The player took a dead choice: whatever was promised up to here is off.</summary>
        public void Kill() => onComplete = null;

        public void Abandon() => Abandoned = true;

        public void Complete() => onComplete?.Invoke();
    }
}

public class QueuedDialogue
{
    public string DialogueKey { get; set; }
    public bool ZoomIn { get; set; }
    public bool Letterbox { get; set; }
    public int? Music { get; set; }
    public DialogueStyle Style { get; set; }
    public DialogueAnchor Anchor { get; set; }

    /// <summary>Hold the line's speaker facing the player while they talk.</summary>
    public bool Root { get; set; } = true;
}
