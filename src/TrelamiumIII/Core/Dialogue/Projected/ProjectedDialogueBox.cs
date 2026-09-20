using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Core.Dialogue.Projected;

/// <summary>
///     A dialogue box drawn as a plane standing in perspective over whoever is speaking.
/// </summary>
/// <remarks>
///     <para>
///         Does exactly what <see cref="DialogueBox" /> does, but obviously is projected.
///         This is an object in the scene that anchors to an entity (speaker) and grows the dialogue box with text.
///     </para>
/// </remarks>
public sealed class ProjectedDialogueBox : IDialoguePresenter, IProjectedContent, IDisposable
{
    #region Tuning

    private const int OPEN_FRAMES = 16;
    private const int CLOSE_FRAMES = 11;

    /// <summary>How long a glyph takes to come up to full strength once revealed.</summary>
    private const float GLYPH_FADE_FRAMES = 7f;

    /// <summary>Wrap width of the text area, in texels.</summary>
    private const int TEXT_WIDTH = 290;

    /// <summary>Vertical step between response slots, as a multiple of a slot's height.</summary>
    private const float CHOICE_STEP = 1.34f;

    /// <summary>Sideways drift between response slots, as a fraction of a slot's width.</summary>
    private const float CHOICE_SKEW = 0.09f;

    private const int MIN_TEXT_WIDTH = 48;

    /// <summary>
    ///     Font scale used when drawing into the surface. Never take this below 1; see
    ///     <see cref="Font" /> for why.
    /// </summary>
    private const float TEXT_SCALE = 1f;

    private const float NAME_SCALE = TEXT_SCALE;
    private const float CHOICE_SCALE = TEXT_SCALE;

    /// <summary>
    ///     Response slots are drawn smaller than the bubble, so the eye reads the line first.
    /// </summary>
    private const float CHOICE_PANEL_SCALE = 0.82f;

    /// <summary>How far outside the bubble a click still counts, in screen pixels.</summary>
    private const float HIT_PADDING = 10f;

    private const int CHOICE_PAD_X = 9;
    private const int CHOICE_PAD_Y = 3;

    /// <summary>Screen pixels between the tail's point and the speaker's head.</summary>
    private const float ANCHOR_GAP = 34f;

    private static readonly float RestYaw = MathHelper.ToRadians(15f);
    private static readonly float RestPitch = MathHelper.ToRadians(-7f);
    private static readonly float RestRoll = MathHelper.ToRadians(-1.6f);

    /// <summary>Extra turn the box arrives carrying, unwound as it settles.</summary>
    private static readonly float EntryYaw = MathHelper.ToRadians(-38f);

    // Depths are fractions of the camera's distance rather than pixel amounts. The distance is solved
    // from the screen height, so a fixed push would be half the scene at 720p and a third of it at 1440p.

    /// <summary>How far back the box starts, as a fraction of the camera's distance.</summary>
    private const float ENTRY_DEPTH = 0.46f;

    /// <summary>How far forward the selected response steps out of the stack.</summary>
    private const float CHOICE_LIFT = 0.04f;

    /// <summary>How far back a response slot arrives from.</summary>
    private const float CHOICE_ENTRY_DEPTH = 0.3f;

    /// <summary>Stagger between slots in the stack so each has its own plane to sit in.</summary>
    private const float CHOICE_STEP_DEPTH = 0.016f;

    #endregion

    #region State

    private readonly Queue<DialogueData> pending = new();
    private readonly ProjectedSurfaces surfaces = new();
    private readonly DialogueTick revealTick = new();
    private readonly SpeechVoice voice = new();
    private readonly DialogueRenderContext renderContext = new();
    private readonly QuadWarp warp;
    private readonly int seed;

    /// <summary>What the caller asked for. kept as the fallback when a line names no present entity.</summary>
    private readonly DialogueAnchor requestedAnchor;

    /// <summary>How far each response slot has eased out of the stack, 0 to 1.</summary>
    private readonly float[] choiceHover = new float[ProjectedSurfaces.MAX_CHOICES];

    /// <summary>Decaying kick applied when a slot is newly picked, for the snap out of the stack.</summary>
    private readonly float[] choiceKick = new float[ProjectedSurfaces.MAX_CHOICES];

    private DialogueAnchor anchor;

    private bool mouseConfirm;
    private bool mouseDismiss;
    private int mouseChoice = -1;

    private DialogueData? current;
    private TextGlyphLayout layout = TextGlyphLayout.Empty;

    /// <summary>Frame each character was revealed on, indexed alongside the line's own characters.</summary>
    private int[] revealedOn = [];

    private int charIndex;
    private int pauseTimer;
    private int age;

    private bool awaitingChoice;
    private int selectedChoice;

    private bool closing;
    private float presence;

    /// <summary>Smoothed content size in texels. Shrinks toward the text; growth is immediate.</summary>
    private Vector2 smoothedSize;

    private BubbleGeometry geometry;
    private Point choiceSlot;
    private Vector2 panelCentre;
    private Vector2 panelSize;
    private Vector2 choiceOrigin;

    /// <summary>Whoever is currently held in a talking loop, so the same NPC is not re-rooted.</summary>
    private NPC? rootedSpeaker;

    /// <summary>Which way round the perspective stands: +1 default, -1 mirrored.</summary>
    private float facing = 1f;

    /// <summary>Eased <see cref="facing" />, so a flip turns the panel rather than snapping it.</summary>
    private float facingBlend = 1f;

    private KeyboardState previousKeys;

    #endregion

    private readonly bool roots;

    private ProjectedDialogueBox(string dialogueKey, DialogueAnchor anchor, bool roots)
    {
        DialogueKey = dialogueKey;
        requestedAnchor = anchor;
        this.anchor = anchor;
        this.roots = roots;

        seed = Main.rand?.Next(1, 9999) ?? 1;
        warp = QuadWarp.Handmade(seed, 5f);

        surfaces.Initialize();
    }

    /// <summary>Builds a box for a registered sequence, or null when the key has no lines.</summary>
    public static ProjectedDialogueBox? Create(string dialogueKey, bool zoomIn, DialogueAnchor anchor, bool roots = true)
    {
        List<DialogueData> lines = DialogueBuilder.BuildSequence(dialogueKey);

        if (lines.Count == 0)
            return null;

        var box = new ProjectedDialogueBox(dialogueKey, anchor, roots) { ShouldZoom = zoomIn };

        foreach (DialogueData line in lines)
            box.pending.Enqueue(line);

        box.NextLine();

        ProjectedDialogueSystem.Register(box);

        return box;
    }

    /// <summary>
    ///     Picks the conversation up from another box, part-way through a line.
    /// </summary>
    /// <remarks>
    ///     Text already on screen when the handover happened is backdated past its own fade, so it is
    ///     there at full strength the moment this box appears. Only the box itself fades in; the words
    ///     inside it do not re-type or re-materialise, which would announce the swap.
    /// </remarks>
    internal static ProjectedDialogueBox? Resume(DialogueHandoff handoff, DialogueAnchor anchor)
    {
        if (handoff.Current is null)
            return null;

        var box = new ProjectedDialogueBox(handoff.Key, anchor, handoff.Roots) { ShouldZoom = handoff.ZoomIn };

        foreach (DialogueData line in handoff.Remaining)
            box.pending.Enqueue(line);

        box.Adopt(handoff.Current);

        box.charIndex = Math.Clamp(handoff.CharIndex, 0, handoff.Current.PlainText.Length);
        box.awaitingChoice = handoff.AwaitingChoice;
        box.selectedChoice = handoff.SelectedChoice;

        for (int i = 0; i < box.charIndex; i++)
            box.revealedOn[i] = -(int)GLYPH_FADE_FRAMES - 1;

        ProjectedDialogueSystem.Register(box);

        return box;
    }

    internal DialogueHandoff Capture() => new()
    {
        Key = DialogueKey,

        // A box on its way out has nothing left to hand over; see DialogueBox.Capture.
        Current = closing ? null : current,
        Remaining = [.. pending],
        CharIndex = charIndex,
        AwaitingChoice = awaitingChoice,
        SelectedChoice = selectedChoice,
        ZoomIn = ShouldZoom,
        Roots = roots,
    };

    /// <summary>The line being spoken, for whoever needs to know who is talking.</summary>
    internal DialogueData? CurrentLine => current;

    public string DialogueKey { get; }

    public bool ShouldZoom { get; private init; }

    public bool ShouldBeRemoved => closing && presence <= 0f;

    public bool Completed { get; private set; }

    public ChoiceData? Chosen { get; private set; }

    /// <summary>
    ///     The face the current line is set in: the speaker's own, or the box's default.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Read from the line rather than fixed, so a speaker carrying a
    ///         <see cref="ISpeakerProfile.Font" /> is set in it. Safe to vary per line because
    ///         everything measured from a font -- the wrapped layout, the plate, the response slots,
    ///         the panel's own dimensions -- is rebuilt whenever the line changes.
    ///     </para>
    ///     <para>
    ///         Never take the drawing scale below 1 whatever face lands here: the atlas is
    ///         antialiased, minifying samples rows out of the strokes, and the magnification that
    ///         follows cannot put back detail that was dropped. Size and resolution are separate
    ///         dials -- what lands on screen is the atlas height times <see cref="PanelStyle.PIXEL" />,
    ///         what can be read is the atlas height alone -- so a bigger face with a smaller
    ///         <c>PIXEL</c> is how you buy legibility, never a larger scale.
    ///     </para>
    /// </remarks>
    private DynamicSpriteFont Font => current?.Profile?.Font ?? FontAssets.MouseText.Value;

    /// <summary>The speaker's colour, dimmed to the second outline behind the shape.</summary>
    private Color Ghost => current is null ? Color.Gray : current.SpeakerColor * 0.55f;

    #region Conversation

    public void Update()
    {
        age++;

        UpdatePresence();

        if (Main.gameMenu || current is null)
        {
            Close();
            return;
        }

        if (!closing)
            UpdateTyping();

        // Before input, not after: the mouse is tested against the panel's projected corners, and
        // testing it against last frame's would misread every click during the entrance -- when the
        // box is still swinging several hundred pixels per frame.
        UpdateLayout();

        if (!closing)
            UpdateInput();

        UpdateChoiceMotion();

        previousKeys = Main.keyState;
    }

    public void Close()
    {
        closing = true;
        awaitingChoice = false;
    }

    /// <summary>Finishes typing the current line at once.</summary>
    public void ForceComplete()
    {
        if (current is null)
            return;

        // Backdated so a line skipped mid-word does not flash its remaining glyphs in from nothing:
        // they arrive already part-way through their own fade, staggered by where they sit.
        for (int i = charIndex; i < current.PlainText.Length; i++)
            revealedOn[i] = age - (int)(GLYPH_FADE_FRAMES * 0.6f) + Math.Min(i - charIndex, 4);

        charIndex = current.PlainText.Length;
        pauseTimer = 0;
    }

    private void NextLine()
    {
        if (pending.Count == 0)
        {
            Finish();
            return;
        }

        Adopt(pending.Dequeue());
    }

    /// <summary>Sets this line up to be typed. Shared by the queue and by a handover.</summary>
    private void Adopt(DialogueData line)
    {
        current = line;
        layout = TextGlyphLayout.Build(line.PlainText, Font, TEXT_SCALE, TEXT_WIDTH);
        revealedOn = new int[line.PlainText.Length];

        // Re-resolved per line: a conversation with two people in it has to move between their heads,
        // and a line started from somewhere that never named an NPC can still find its own speaker.
        anchor = DialogueAnchor.ForSpeaker(line, requestedAnchor);

        // Snapped rather than eased. A box has to open already facing the right way; easing from
        // whatever the last speaker needed would have it swing round as it arrives.
        UpdateFacing(snap: true);

        RootSpeaker();

        Array.Clear(choiceHover);
        Array.Clear(choiceKick);

        charIndex = 0;
        pauseTimer = 0;
        awaitingChoice = false;
        selectedChoice = 0;

        voice.Reset();

        choiceSlot = MeasureChoiceSlot();
    }

    private void UpdatePresence()
    {
        if (closing)
            presence = MathF.Max(presence - 1f / CLOSE_FRAMES, 0f);
        else
            presence = MathF.Min(presence + 1f / OPEN_FRAMES, 1f);
    }

    /// <remarks>
    ///     Loops rather than revealing at most one character. The clock deals in fractional frames, so
    ///     a fast line can owe more than one character on the same frame, and each is costed against
    ///     its own affects -- a <c>Slow</c> span midway through a line slows only itself.
    /// </remarks>
    private void UpdateTyping()
    {
        if (current is null)
            return;

        if (pauseTimer > 0)
        {
            pauseTimer--;
            voice.Interrupt();
            return;
        }

        if (charIndex >= current.PlainText.Length)
        {
            if (current.HasChoices)
                awaitingChoice = true;

            return;
        }

        voice.Tick();

        while (charIndex < current.PlainText.Length)
        {
            // Reveal-time affects covering this glyph: timing, pitch, one-shot sounds.
            revealTick.Reset(charIndex);

            IReadOnlyList<LineModifier> modifiers = current.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Covers(charIndex))
                    modifiers[i].ApplyReveal(revealTick);
            }

            if (!voice.TryReveal(current.BaseDelay, current.Speed, revealTick.SpeedMultiplier))
                break;

            revealedOn[charIndex] = age;
            voice.Note(current.PlainText[charIndex], revealTick);
            charIndex++;

            // A Pause affect holds the typewriter after the covered glyph appears.
            if (revealTick.HoldFrames > 0)
            {
                pauseTimer = revealTick.HoldFrames;
                break;
            }
        }

        voice.Play(current.Profile, Main.LocalPlayer.position);
    }

    private bool LineFinished => current is not null && charIndex >= current.PlainText.Length;

    /// <summary>
    ///     True while characters are actually arriving. Drives the speaker's mouth.
    /// </summary>
    /// <remarks>
    ///     A held pause, a finished line waiting on the player, and a box on its way out are all
    ///     silence. Only the stretch where the typewriter is running counts.
    /// </remarks>
    private bool IsVoicing => !closing && pauseTimer <= 0 && !LineFinished;

    /// <summary>
    ///     Hands the line's speaker a talking loop, if it is not already running one.
    /// </summary>
    /// <remarks>
    ///     Rooting only on a change of speaker, rather than on every line, keeps a run of lines from
    ///     the same character as one uninterrupted behaviour -- re-rooting each time would stack a
    ///     fresh interrupt on top of the last and restart the animation mid-sentence.
    ///     <para>
    ///         The predicate reads <see cref="rootedSpeaker" /> rather than the NPC it was given, so
    ///         when the conversation moves on this field changing is what releases the old speaker.
    ///     </para>
    /// </remarks>
    private void RootSpeaker()
    {
        if (!roots)
            return;

        NPC? speaker = anchor.Speaker;

        if (ReferenceEquals(speaker, rootedSpeaker))
            return;

        rootedSpeaker = speaker;

        DialogueBehavior.Root(
            speaker,
            stillSpeaking: () => !closing && ReferenceEquals(rootedSpeaker, speaker),
            voicing: () => IsVoicing);
    }

    #endregion

    #region Input

    /// <remarks>
    ///     Only the chat box shuts the whole thing down -- it eats the same keys, and a player typing
    ///     "1" into it should not be answering an NPC with their first response. While a box is open
    ///     the chat cannot be opened at all (see <see cref="ProjectedDialogueSystem" />), so Enter is
    ///     free to be the advance key it wants to be.
    /// </remarks>
    private void UpdateInput()
    {
        if (Main.drawingPlayerChat)
        {
            ClearMouseIntent();
            return;
        }

        if (mouseDismiss)
        {
            ClearMouseIntent();
            EndConversation();
            return;
        }

        if (awaitingChoice)
        {
            UpdateChoiceInput();
            return;
        }

        bool clicked = mouseConfirm;
        ClearMouseIntent();

        if (clicked || Pressed(Keys.Enter) || KeybindsSystem.FFDialogueKeybind?.JustPressed == true)
            Advance();
    }

    private void ClearMouseIntent()
    {
        mouseConfirm = false;
        mouseDismiss = false;
    }

    private void Advance()
    {
        if (!LineFinished)
        {
            ForceComplete();
            return;
        }

        if (pending.Count > 0)
            NextLine();
        else
            Finish();
    }

    /// <summary>
    ///     Drops what is left of the conversation, rather than advancing through it. Counts as cut off,
    ///     so nothing waiting on the conversation completing fires.
    /// </summary>
    private void EndConversation()
    {
        pending.Clear();
        SoundEngine.PlaySound(SoundID.MenuClose);
        Close();
    }

    private void UpdateChoiceInput()
    {
        List<ChoiceData> choices = current!.Choices;
        int count = Math.Min(choices.Count, ProjectedSurfaces.MAX_CHOICES);

        // Arrows and digits only. The letter keys next to them are the movement keys, and a player
        // who is not rooted for the conversation would walk off mid-answer.
        int moved = 0;

        if (Pressed(Keys.Down))
            moved = 1;
        else if (Pressed(Keys.Up))
            moved = -1;

        if (moved != 0)
            Select((selectedChoice + moved + count) % count);

        for (int i = 0; i < count; i++)
        {
            if (Pressed(Keys.D1 + i) || Pressed(Keys.NumPad1 + i))
            {
                Select(i);
                Confirm();
                return;
            }
        }

        if (mouseChoice >= 0 && mouseChoice < count)
            Select(mouseChoice);

        bool clicked = mouseConfirm;
        ClearMouseIntent();

        if (clicked || Pressed(Keys.Enter) || KeybindsSystem.FFDialogueKeybind?.JustPressed == true)
            Confirm();
    }

    private void Select(int index)
    {
        if (index == selectedChoice)
            return;

        selectedChoice = index;
        choiceKick[index] = 1f;

        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    /// <summary>
    ///     Eases each slot toward or away from the front of the stack.
    /// </summary>
    /// <remarks>
    ///     Two values per slot rather than one. The hover is a plain ease, which is what carries the
    ///     slot out and holds it there; the kick is a decaying impulse on top, which is what makes it
    ///     arrive rather than merely end up. A single eased value can only ever glide.
    /// </remarks>
    private void UpdateChoiceMotion()
    {
        for (int i = 0; i < choiceHover.Length; i++)
        {
            float target = awaitingChoice && i == selectedChoice ? 1f : 0f;

            choiceHover[i] = MathHelper.Lerp(choiceHover[i], target, 0.28f);
            choiceKick[i] = MathF.Max(choiceKick[i] - 0.055f, 0f);
        }
    }

    private void Confirm()
    {
        List<ChoiceData> choices = current!.Choices;

        if (selectedChoice < 0 || selectedChoice >= choices.Count)
            return;

        ChoiceData choice = choices[selectedChoice];

        SoundEngine.PlaySound(SoundID.MenuOpen);
        awaitingChoice = false;

        choice.OnSelected?.Invoke();

        // Recorded rather than started here. The manager opens the branch the moment this box is
        // dropped, in the conversation's own style, anchor and music -- a branch is the same
        // conversation continuing, and would otherwise answer a bubble over the speaker's head with a
        // panel along the bottom of the screen, in silence.
        Finish(choice);
    }

    /// <summary>Closes the box as the end of what it had to say, rather than as it being cut off.</summary>
    private void Finish(ChoiceData? choice = null)
    {
        if (closing)
            return;

        Completed = true;
        Chosen = choice;
        Close();
    }

    private bool Pressed(Keys key) => Main.keyState.IsKeyDown(key) && !previousKeys.IsKeyDown(key);

    /// <summary>
    ///     Reads the mouse against the panel and records what it wants, for the next update to act on.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Called while drawing, which is where every other piece of interactive UI in this mod
    ///         tests the cursor, and the reason clicking the box did nothing when this lived in the
    ///         update: by the time a mod's UI update runs, the release latch this depends on has
    ///         already been spent for the frame. Vanilla's own in-game notifications hit-test from
    ///         their draw for the same reason.
    ///     </para>
    ///     <para>
    ///         Recording intent rather than acting on it keeps every state change in the update, so a
    ///         click never advances a line halfway through the frame that is drawing it.
    ///     </para>
    ///     <para>
    ///         Padded, because the target is a small quad standing at an angle: its silhouette on
    ///         screen is narrower than the panel looks, and its edges are what a player aims at.
    ///     </para>
    /// </remarks>
    private void HandleMouse()
    {
        mouseChoice = -1;

        if (closing || Main.drawingPlayerChat || PlayerInput.IgnoreMouseInterface)
            return;

        if (awaitingChoice)
        {
            for (int i = 0; i < surfaces.ChoicesDrawn; i++)
            {
                if (!PanelCamera.Contains(ChoicePlacement(i), Main.MouseScreen, HIT_PADDING))
                    continue;

                mouseChoice = i;
                break;
            }

            if (mouseChoice < 0)
                return;
        }
        else if (!PanelCamera.Contains(BubblePlacement(), Main.MouseScreen, HIT_PADDING))
        {
            return;
        }

        // Claimed while the cursor is over the panel, so the click that advances a line does not also
        // swing whatever the player is holding.
        Main.LocalPlayer.mouseInterface = true;

        if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            Main.mouseLeftRelease = false;
            mouseConfirm = true;
        }
        else if (!awaitingChoice && Main.mouseRight && Main.mouseRightRelease)
        {
            Main.mouseRightRelease = false;
            mouseDismiss = true;
        }
    }

    #endregion

    #region Layout

    /// <summary>
    ///     Sizes the box to what has been revealed and works out where on screen it hangs.
    /// </summary>
    /// <remarks>
    ///     Growth is immediate and shrinking is smoothed. Easing the growth as well would leave the
    ///     newest glyph outside the panel for the two or three frames the box took to catch up, which
    ///     is exactly the moment the eye is on it. Shrinking has no such deadline, so the box eases
    ///     down to the next line rather than snapping.
    /// </remarks>
    private void UpdateLayout()
    {
        if (current is null)
            return;

        int nameWidth = NamePlateWidth();
        int nameHeight = (int)MathF.Ceiling(Font.LineSpacing * NAME_SCALE) + 2;

        // Taken from the font rather than the layout so an empty line still gets a one-line box
        // instead of collapsing to a sliver.
        float lineHeight = Font.LineSpacing * TEXT_SCALE;

        Vector2 needed = new(
            Math.Clamp(MathF.Ceiling(layout.ExtentAfter(charIndex)), MIN_TEXT_WIDTH, TEXT_WIDTH),
            Math.Clamp(
                MathF.Max(layout.LinesAfter(charIndex), 1) * lineHeight,
                lineHeight,
                MathF.Max(PanelStyle.MaxContentHeight(nameWidth > 0 ? nameHeight : 0), lineHeight)));

        smoothedSize = Vector2.Lerp(smoothedSize, needed, 0.22f);
        smoothedSize = Vector2.Max(smoothedSize, needed);

        Point content = new((int)MathF.Round(smoothedSize.X), (int)MathF.Ceiling(smoothedSize.Y));

        UpdateFacing();

        // Two passes: the first sizes the bubble so the anchor can be compared against its centre,
        // the second rebuilds it with the tail actually leaning at the speaker.
        geometry = PanelStyle.Measure(content, nameWidth, nameHeight, 0f);
        PlacePanel();
        geometry = PanelStyle.Measure(content, nameWidth, nameHeight, TailAim());
        PlacePanel();
        PlaceChoices();
    }

    /// <summary>
    ///     Puts the response stack below the bubble's lower right, moved as a whole if it will not fit.
    /// </summary>
    /// <remarks>
    ///     Shifting the column rather than clamping each slot into the window. Clamped individually,
    ///     slots that overran the bottom would all pile onto the same edge and stop being a list at
    ///     the exact moment there were enough of them to need reading as one.
    /// </remarks>
    private void PlaceChoices()
    {
        if (current is null || !current.HasChoices)
            return;

        Point footprint = PanelStyle.ChoiceFootprint(choiceSlot);
        Vector2 size = new Vector2(footprint.X, footprint.Y) * Scale * CHOICE_PANEL_SCALE;
        int count = Math.Min(current.Choices.Count, ProjectedSurfaces.MAX_CHOICES);

        const float EDGE = 14f;

        // The stack cascades away from the bubble on whichever side the panel is turned toward, so it
        // mirrors with the perspective rather than always trailing off to the right.
        Vector2 origin = panelCentre + new Vector2(
            panelSize.X * 0.34f * facingBlend,
            panelSize.Y * 0.5f + size.Y * 0.8f);

        float skew = (count - 1) * size.X * CHOICE_SKEW * facingBlend;

        // Measured from both ends, because the skew can now run in either direction and the trailing
        // slot is not reliably the rightmost one.
        float left = MathF.Min(origin.X, origin.X + skew) - size.X * 0.5f;
        float right = MathF.Max(origin.X, origin.X + skew) + size.X * 0.5f;
        float bottom = origin.Y + (count - 1) * size.Y * CHOICE_STEP + size.Y * 0.5f;

        origin.Y -= MathF.Max(bottom - (Main.screenHeight - EDGE), 0f);
        origin.X -= MathF.Max(right - (Main.screenWidth - EDGE), 0f);
        origin.X += MathF.Max(EDGE - left, 0f);

        // Only after pulling it in, so a stack too tall for the window sits against the top rather
        // than being pushed back off the bottom by its own correction.
        origin.Y = MathF.Max(origin.Y, size.Y * 0.5f + EDGE);

        choiceOrigin = origin;
    }

    private int NamePlateWidth()
    {
        if (current is null || current.IsPlayer || string.IsNullOrEmpty(current.Speaker))
            return 0;

        return (int)MathF.Ceiling(Font.MeasureString(current.Speaker).X * NAME_SCALE) + PanelStyle.NAME_PAD_X * 2;
    }

    private Point MeasureChoiceSlot()
    {
        if (current is null || !current.HasChoices)
            return Point.Zero;

        float widest = 0f;

        foreach (ChoiceData choice in current.Choices)
            widest = MathF.Max(widest, Font.MeasureString(choice.Text).X * CHOICE_SCALE);

        // One width for every slot. Ragged buttons read as a list of different things rather than as
        // a set of answers to the same question.
        Point room = PanelStyle.MaxChoiceContent();

        return new Point(
            Math.Min((int)MathF.Ceiling(widest) + CHOICE_PAD_X * 2, room.X),
            Math.Min((int)MathF.Ceiling(Font.LineSpacing * CHOICE_SCALE) + CHOICE_PAD_Y * 2, room.Y));
    }

    private float Scale => PanelStyle.PIXEL * Main.UIScale;

    /// <summary>Puts the surface's used region on screen with its tail just clear of the speaker.</summary>
    /// <summary>
    ///     Decides which way round the perspective stands, from where the speaker is relative to the
    ///     player.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A fixed yaw only ever looks right from one side. Standing to the speaker's left, a panel
    ///         converging away to the right reads as a plane hanging in the scene; walk past them and
    ///         the same panel is now turned away from the viewer, which reads as a mistake. Mirroring
    ///         it keeps the convergence agreeing with where the player actually is.
    ///     </para>
    ///     <para>
    ///         Compared in screen space rather than world space. What has to agree is where the two
    ///         appear, and under the dialogue system's own zoom those are not the same comparison.
    ///     </para>
    ///     <para>
    ///         The deadband is what stops a speaker standing on top of the player flipping the panel
    ///         back and forth with every idle step, and the blend is eased rather than switched so the
    ///         flip is the panel turning through edge-on -- which is the one moment a plane in space
    ///         can change which way it faces without looking like it teleported.
    ///     </para>
    /// </remarks>
    private void UpdateFacing(bool snap = false)
    {
        const float DEADBAND = 28f;

        Vector2 player = Vector2.Transform(
            Main.LocalPlayer.Center - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

        float offset = anchor.ScreenPoint().X - player.X;

        // Outside the band only: inside it, whichever side it was on last is still the better guess.
        if (MathF.Abs(offset) > DEADBAND)
            facing = offset < 0f ? -1f : 1f;

        facingBlend = snap ? facing : MathHelper.Lerp(facingBlend, facing, 0.12f);
    }

    private void PlacePanel()
    {
        panelSize = new Vector2(geometry.Used.X, geometry.Used.Y) * Scale;

        Vector2 target = anchor.ScreenPoint();

        float x = anchor.HasSpeaker ? target.X : Main.screenWidth * 0.5f;

        // The tail's point is what has to meet the speaker, and it is not the bottom of the region --
        // the border and the ghost hang past it. Anchoring on the region would leave a visible gap
        // that grew and shrank with the panel.
        float apexFromBottom = (geometry.Used.Y - geometry.TailApex.Y) * Scale;
        float bottom = target.Y - ANCHOR_GAP + apexFromBottom;

        // The bubble is centred on the body rather than on the region, which is wider on the left by
        // the ghost margin. Without this the panel sits a few pixels off its speaker.
        float bodyOffset = (geometry.Body.X + geometry.Body.Width * 0.5f - geometry.Used.X * 0.5f) * Scale;

        panelCentre = new Vector2(
            OnScreen(x - bodyOffset, panelSize.X * 0.5f, Main.screenWidth),
            OnScreen(bottom - panelSize.Y * 0.5f, panelSize.Y * 0.5f, Main.screenHeight));
    }

    /// <summary>
    ///     Keeps one axis of the panel inside the window.
    /// </summary>
    /// <remarks>
    ///     Centres instead of clamping when the panel is wider than the space it is being kept inside,
    ///     which a small window at a large UI scale can manage. A plain clamp would be handed a
    ///     minimum above its maximum and throw.
    /// </remarks>
    private static float OnScreen(float centre, float half, float extent)
    {
        const float EDGE = 8f;

        float low = half + EDGE;
        float high = extent - half - EDGE;

        return high < low ? extent * 0.5f : Math.Clamp(centre, low, high);
    }

    /// <summary>Where the tail has to lean to point at the speaker, as -1 to 1 across the body.</summary>
    private float TailAim()
    {
        Vector2 target = anchor.ScreenPoint();

        float bodyCentre = panelCentre.X + (geometry.Body.X + geometry.Body.Width * 0.5f - geometry.Used.X * 0.5f) * Scale;
        float reach = MathF.Max(geometry.Body.Width * 0.5f * Scale, 1f);

        return Math.Clamp((target.X - bodyCentre) / reach, -1f, 1f);
    }

    #endregion

    #region Placement

    /// <summary>
    ///     Eased 0 to 1 with an overshoot, so the panel arrives slightly past its rest angle and
    ///     settles back rather than decelerating into it. Closing runs the same curve without the
    ///     overshoot -- a box on its way out has nothing to settle into.
    /// </summary>
    private float Entrance
    {
        get
        {
            float t = Math.Clamp(presence, 0f, 1f);

            if (closing)
                return t * t;

            float back = 1f - t;
            return 1f + (2.2f * back * back * back - 3.2f * back * back) * back;
        }
    }

    private QuadPlacement BubblePlacement()
    {
        float entrance = Entrance;
        float settle = 1f - entrance;

        // A slow, shallow drift on two incommensurate periods. Enough to stop the panel reading as a
        // sprite pinned to the screen; small enough that nobody notices it moving.
        float time = Main.GlobalTimeWrappedHourly;

        return new QuadPlacement
        {
            Center = panelCentre,
            Size = panelSize * MathHelper.Lerp(0.74f, 1f, entrance),

            // Everything with a handedness is scaled by the blend, so a flip passes through zero --
            // the panel squares up to the viewer at the halfway point and carries on turning. The
            // idle drift is left alone: it is symmetric noise and has no side to be on.
            Yaw = (RestYaw + settle * EntryYaw) * facingBlend + MathF.Sin(time * 0.7f) * 0.022f,
            Pitch = RestPitch + MathF.Sin(time * 0.53f) * 0.016f,
            Roll = (RestRoll + settle * 0.12f) * facingBlend,
            Depth = settle * ENTRY_DEPTH * PanelCamera.Distance,
            Warp = warp,
            FarShade = 0.22f,
        };
    }

    /// <summary>
    ///     Response slots cascade down and out from the bubble's lower right.
    /// </summary>
    /// <remarks>
    ///     Down and to the side rather than straight down, because straight down is where the speaker
    ///     is -- the tail points at them. The stagger also gives each slot its own depth, so the
    ///     selected one has somewhere to step forward from.
    /// </remarks>
    private QuadPlacement ChoicePlacement(int index)
    {
        Point footprint = PanelStyle.ChoiceFootprint(choiceSlot);
        Vector2 size = new Vector2(footprint.X, footprint.Y) * Scale * CHOICE_PANEL_SCALE;

        // Staggered so a slot lands after the one above it, and after the bubble has settled.
        float arrival = Math.Clamp((presence - 0.55f) / 0.45f - index * 0.22f, 0f, 1f);
        float eased = arrival * arrival * (3f - 2f * arrival);

        float hover = choiceHover[index];
        float kick = choiceKick[index];

        // The impulse decays, so the overshoot happens once on arrival and does not idle-wobble.
        float lunge = hover + kick * kick * 0.35f;

        // The step down has to clear the slot's own height plus the room its tilt and its selected
        // scale-up take, or the stack reads as overlapping cards rather than as a list.
        // Skew, lunge and the entrance slide all run along the panel's own handedness, so a mirrored
        // stack steps out to the left and arrives from the left.
        Vector2 centre = choiceOrigin + new Vector2(
            (index * size.X * CHOICE_SKEW - lunge * size.X * 0.11f) * facingBlend,
            index * size.Y * CHOICE_STEP);

        centre += new Vector2(60f * facingBlend, 0f) * (1f - eased);

        float distance = PanelCamera.Distance;

        return new QuadPlacement
        {
            Center = centre,
            Size = size * MathHelper.Lerp(0.8f, 1f + lunge * 0.075f, eased),

            // Turns to face the reader as it comes forward, which is most of why the motion reads as
            // the slot stepping out rather than merely sliding sideways.
            Yaw = (RestYaw * MathHelper.Lerp(1.15f, 0.45f, hover) - kick * 0.06f) * facingBlend,
            Pitch = RestPitch * 0.6f,
            Roll = (RestRoll * (index % 2 == 0 ? 1f : -0.7f) + kick * kick * 0.05f) * facingBlend,
            Depth = distance * (MathHelper.Lerp(index * CHOICE_STEP_DEPTH, -CHOICE_LIFT, lunge)
                                + (1f - eased) * CHOICE_ENTRY_DEPTH),
            Warp = QuadWarp.Handmade(seed + index * 31, 3f),
            FarShade = 0.2f,
        };
    }

    #endregion

    #region Drawing

    /// <summary>Rebuilds the off-screen faces. Driven by <see cref="ProjectedDialogueSystem" />.</summary>
    internal void BuildSurfaces()
    {
        if (current is not null && presence > 0f)
            surfaces.Build(this);
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        ProjectedQuad? quad = ProjectedDialogueSystem.Quad;

        if (current is null || presence <= 0f || surfaces.Bubble is null || quad is null || !quad.Ready)
            return;

        HandleMouse();

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        Color tint = Color.White * presence;

        // The interface layer opened this batch; primitives cannot be issued inside one, so it is
        // closed and reopened around the quads exactly as the layer left it.
        spriteBatch.End();

        quad.Begin(device);
        quad.Draw(device, surfaces.Bubble, surfaces.BubbleRegion, BubblePlacement(), tint);

        if (awaitingChoice && surfaces.Choices is not null)
        {
            for (int i = 0; i < surfaces.ChoicesDrawn; i++)
                quad.Draw(device, surfaces.Choices, surfaces.ChoiceRegion(i), ChoicePlacement(i), tint);
        }

        quad.End(device);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            Main.SamplerStateForCursor,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.UIScaleMatrix);
    }

    Point IProjectedContent.DrawBubble(PixelCanvas canvas)
    {
        PanelStyle.DrawBubble(canvas, geometry, Ghost);

        if (geometry.NameTab != Rectangle.Empty && current is not null)
        {
            canvas.Glyph(
                Font,
                current.Speaker,
                new Vector2(geometry.NameTab.X + PanelStyle.NAME_PAD_X, geometry.NameTab.Y),
                PanelStyle.Inverted,
                NAME_SCALE);
        }

        DrawGlyphs(canvas);
        DrawAdvanceCue(canvas);

        return geometry.Used;
    }

    /// <summary>
    ///     Draws the revealed text, one glyph at a time.
    /// </summary>
    /// <remarks>
    ///     Per glyph rather than per line because each one is at a different point in its own fade,
    ///     and because the affect system reserves the right to move or recolour any of them. Affect
    ///     offsets are authored in screen pixels, so they are divided down into texels on the way in
    ///     -- a shake written for the panel box shakes by the same visible distance here.
    /// </remarks>
    private void DrawGlyphs(PixelCanvas canvas)
    {
        if (current is null)
            return;

        LaidGlyph[] glyphs = layout.Glyphs;
        IReadOnlyList<LineModifier> modifiers = current.Modifiers;

        int count = Math.Min(charIndex, glyphs.Length);

        for (int i = 0; i < count; i++)
        {
            LaidGlyph glyph = glyphs[i];

            if (glyph.Character is ' ' or '\n')
                continue;

            float fade = Math.Clamp((age - revealedOn[i]) / GLYPH_FADE_FRAMES, 0f, 1f);

            renderContext.Reset(i, PanelStyle.Text);

            for (int m = 0; m < modifiers.Count; m++)
            {
                if (modifiers[m].Covers(i))
                    modifiers[m].ApplyDraw(renderContext);
            }

            // Drops into place as it fades up. The rise is in texels, so it is a whole chunky pixel
            // of travel rather than a sub-pixel slide that point sampling would render as a jump.
            float rise = (1f - fade) * (1f - fade) * -3f;

            Vector2 position = new Vector2(geometry.ContentOrigin.X, geometry.ContentOrigin.Y)
                + new Vector2(glyph.X, glyph.Line * layout.LineHeight)
                + renderContext.Offset / PanelStyle.PIXEL
                + new Vector2(0f, rise);

            canvas.Glyph(Font, glyph.Character.ToString(), position, renderContext.Color * fade, TEXT_SCALE);
        }
    }

    /// <summary>A bobbing arrow in the corner, once the line is done and there is more to come.</summary>
    private void DrawAdvanceCue(PixelCanvas canvas)
    {
        if (!LineFinished || awaitingChoice || pending.Count == 0)
            return;

        float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 5f) * 1.5f;

        float x = geometry.Body.Right - PanelStyle.PAD_X - 3f;
        float y = geometry.Body.Bottom - PanelStyle.PAD_Y - 4f + bob;

        canvas.Triangle(
            new Vector2(x - 4f, y - 3f),
            new Vector2(x + 4f, y - 3f),
            new Vector2(x, y + 3f),
            PanelStyle.Text);
    }

    int IProjectedContent.ChoiceCount => awaitingChoice && current is not null ? current.Choices.Count : 0;

    Point IProjectedContent.DrawChoice(PixelCanvas canvas, int index)
    {
        bool selected = index == selectedChoice;

        Point origin = PanelStyle.DrawChoice(canvas, choiceSlot, selected, Ghost);

        canvas.Glyph(
            Font,
            current!.Choices[index].Text,
            new Vector2(origin.X + CHOICE_PAD_X, origin.Y + CHOICE_PAD_Y),
            selected ? PanelStyle.Inverted : PanelStyle.Text,
            CHOICE_SCALE);

        return PanelStyle.ChoiceFootprint(choiceSlot);
    }

    #endregion

    public void Dispose() => surfaces.Dispose();
}
