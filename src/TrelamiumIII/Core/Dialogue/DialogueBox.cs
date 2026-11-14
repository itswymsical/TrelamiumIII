using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.UI.Projected;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///     A dialogue box drawn as a plane standing in perspective over whoever is speaking.
/// </summary>
/// <remarks>
///     <para>
///         This is an object in the scene that anchors to an entity (speaker) and grows the dialogue box with text.
///     </para>
///     <para>
///         While the speaker is far from the player the same box slides to the bottom of the screen,
///         retracts its tail and squares up flat to the screen, rather than handing over to a different box.
///     </para>
/// </remarks>
public sealed class DialogueBox : IDialoguePresenter, IProjectedContent, IProjectedPanel
{
    #region Tuning

    private const int OPEN_FRAMES = 16;
    private const int CLOSE_FRAMES = 11;

    /// <summary>How long a glyph takes to come up to full strength once revealed.</summary>
    private const float GLYPH_FADE_FRAMES = 7f;

    /// <summary>Wrap width a line starts at, in texels.</summary>
    private const int TEXT_WIDTH = 250;

    /// <summary>
    ///     Widest a long line may stretch before it has to add rows instead. Bounded by the surface:
    ///     this plus the padding, border and ghost margin has to fit inside
    ///     <see cref="PanelStyle.SURFACE_WIDTH" />.
    /// </summary>
    private const int MAX_TEXT_WIDTH = 360;

    private const int WIDTH_STEP = 22;

    /// <summary>
    ///     Wrap width while docked, in texels: a wide, low text box across the bottom of the screen rather than a
    ///     bubble. About 730 screen pixels at UI scale 1. Bounded by <see cref="PanelStyle.SURFACE_WIDTH" /> the
    ///     same way as <see cref="MAX_TEXT_WIDTH" />.
    /// </summary>
    private const int DOCKED_TEXT_WIDTH = 540;

    /// <summary>Rows a line may take before the box stretches wider to hold it.</summary>
    private const int COMPACT_LINES = 3;

    /// <summary>How much smaller the bubble draws for each row past <see cref="COMPACT_LINES" />.</summary>
    private const float DENSITY_STEP = 0.07f;

    private const float MIN_DENSITY = 0.72f;

    /// <summary>
    ///     Smallest the bubble will shrink to fit between its speaker and the top of the screen. Past
    ///     this it stops shrinking and is clamped onto the screen, overlapping the speaker, since text
    ///     too small to read is worse than a covered head.
    /// </summary>
    private const float MIN_FIT = 0.62f;

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

    /// <summary>A docked box undocks once the speaker is within this many tiles.</summary>
    private const float UNDOCK_TILES = 5f;

    /// <summary>A hanging box docks once the speaker is past this many tiles. Must exceed <see cref="UNDOCK_TILES" />.</summary>
    private const float DOCK_TILES = 6f;

    /// <summary>Screen pixels a docked box's speaker has to be inside the screen edge before it undocks.</summary>
    private const float UNDOCK_INSET = 48f;

    /// <summary>Gap between a docked box and the bottom of the screen, in UI pixels.</summary>
    private const float DOCK_MARGIN = 76f;

    /// <summary>Size of a docked box relative to one hanging over its speaker.</summary>
    private const float DOCK_SCALE = 0.85f;

    /// <summary>Screen pixels a docked box drops through as it opens and closes.</summary>
    private const float DOCK_DROP = 36f;

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

    /// <summary>Wrap width the current line was laid out at, in texels.</summary>
    private int wrapWidth = TEXT_WIDTH;

    /// <summary>Whether the current line was laid out for the docked box, at <see cref="DOCKED_TEXT_WIDTH" />.</summary>
    private bool wrappedDocked;

    /// <summary>Per-line shrink for long lines; fixed once the line is laid out.</summary>
    private float density = 1f;

    /// <summary>Eased shrink that keeps the bubble between the speaker and the top of the screen.</summary>
    private float fit = 1f;

    private bool fitSettled;

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

    /// <summary>Whether the box currently belongs at the bottom of the screen rather than over the speaker.</summary>
    private bool docked;

    /// <summary>Eased <see cref="docked" />, 0 hanging over the speaker to 1 at the bottom of the screen.</summary>
    private float dock;

    private bool dockSettled;

    #endregion

    private readonly bool roots;

    /// <summary>Null when the box was opened with cursor tilt off; applies to the bubble only.</summary>
    private readonly CursorTilt? tilt;

    private DialogueBox(string dialogueKey, DialogueAnchor anchor, bool roots, bool cursorTilt)
    {
        DialogueKey = dialogueKey;
        requestedAnchor = anchor;
        this.anchor = anchor;
        this.roots = roots;
        tilt = cursorTilt ? new CursorTilt() : null;

        seed = Main.rand?.Next(1, 9999) ?? 1;
        warp = QuadWarp.Handmade(seed, 5f);

        surfaces.Initialize();
    }

    /// <summary>Builds a box for a registered sequence, or null when the key has no lines.</summary>
    /// <param name="cursorTilt">Tilt the bubble after the cursor while it is over it.</param>
    public static DialogueBox? Create(string dialogueKey, bool zoomIn, DialogueAnchor anchor, bool roots = true, bool cursorTilt = false)
    {
        List<DialogueData> lines = DialogueBuilder.BuildSequence(dialogueKey);

        if (lines.Count == 0)
            return null;

        var box = new DialogueBox(dialogueKey, anchor, roots, cursorTilt) { ShouldZoom = zoomIn };

        foreach (DialogueData line in lines)
            box.pending.Enqueue(line);

        box.NextLine();

        ProjectedPanelSystem.Register(box);

        return box;
    }

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
        tilt?.Update();

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
        revealedOn = new int[line.PlainText.Length];

        // Re-resolved per line: a conversation with two people in it has to move between their heads,
        // and a line started from somewhere that never named an NPC can still find its own speaker.
        anchor = DialogueAnchor.ForSpeaker(line, requestedAnchor);

        // Snapped rather than eased. A box has to open already facing the right way; easing from
        // whatever the last speaker needed would have it swing round as it arrives.
        UpdateFacing(snap: true);
        UpdateDock(snap: !dockSettled);

        // After docking is decided, since a docked box wraps wider.
        Relayout();

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

    /// <summary>Lays out the current line for the box as it stands, docked or hanging.</summary>
    private void Relayout()
    {
        if (current is null)
            return;

        layout = Wrap(current.PlainText);
        density = MathF.Max(1f - Math.Max(layout.LineCount - COMPACT_LINES, 0) * DENSITY_STEP, MIN_DENSITY);
    }

    /// <summary>
    ///     Lays the line out at the narrowest width that keeps it to <see cref="COMPACT_LINES" /> rows,
    ///     stretching no further than <see cref="MAX_TEXT_WIDTH" />. Docked, it takes
    ///     <see cref="DOCKED_TEXT_WIDTH" /> outright.
    /// </summary>
    /// <remarks>
    ///     A wide, short box covers less of the scene than a narrow, tall one and keeps clear of the
    ///     speaker it hangs over, so a long line spends width before it spends height. Run once per
    ///     line, and again only if the box docks or undocks mid-line; never per frame.
    /// </remarks>
    private TextGlyphLayout Wrap(string text)
    {
        wrappedDocked = docked;

        if (docked)
        {
            wrapWidth = DOCKED_TEXT_WIDTH;
            return TextGlyphLayout.Build(text, Font, TEXT_SCALE, wrapWidth);
        }

        wrapWidth = TEXT_WIDTH;
        TextGlyphLayout laid = TextGlyphLayout.Build(text, Font, TEXT_SCALE, wrapWidth);

        while (laid.LineCount > COMPACT_LINES && wrapWidth < MAX_TEXT_WIDTH)
        {
            wrapWidth = Math.Min(wrapWidth + WIDTH_STEP, MAX_TEXT_WIDTH);
            laid = TextGlyphLayout.Build(text, Font, TEXT_SCALE, wrapWidth);
        }

        return laid;
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
    ///     Ignored while the chat box is open, since it reads the same keys. <see cref="DialogueSystem" /> keeps chat
    ///     from opening while a box is live, which frees Enter to advance.
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

        // A press landing on the frame the last glyph appeared, or during a pause after it, is still
        // before the answers are up. Going on from here would end the conversation unanswered.
        if (current!.HasChoices)
        {
            awaitingChoice = true;
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

        bool interactive = !closing && !Main.drawingPlayerChat && !PlayerInput.IgnoreMouseInterface;

        tilt?.Sample(RestBubblePlacement(), Main.MouseScreen, interactive);

        if (!interactive)
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

        UpdateDock();

        // Docking or undocking mid-line re-wraps it, rather than leaving it at the other state's width.
        if (docked != wrappedDocked)
            Relayout();

        int nameWidth = NamePlateWidth();
        int nameHeight = (int)MathF.Ceiling(Font.LineSpacing * NAME_SCALE) + 2;

        // Taken from the font rather than the layout so an empty line still gets a one-line box
        // instead of collapsing to a sliver.
        float lineHeight = Font.LineSpacing * TEXT_SCALE;
        float maxHeight = MathF.Max(PanelStyle.MaxContentHeight(nameWidth > 0 ? nameHeight : 0), lineHeight);
        float finishedHeight = Math.Clamp(MathF.Max(layout.LineCount, 1) * lineHeight, lineHeight, maxHeight);

        // Docked, the box opens at the whole line's size and holds it while the line types, as a text box
        // would. Hanging, it grows with the words, as a bubble would.
        Vector2 needed = wrappedDocked
            ? new Vector2(wrapWidth, finishedHeight)
            : new Vector2(
                Math.Clamp(MathF.Ceiling(layout.ExtentAfter(charIndex)), MIN_TEXT_WIDTH, wrapWidth),
                Math.Clamp(MathF.Max(layout.LinesAfter(charIndex), 1) * lineHeight, lineHeight, maxHeight));

        smoothedSize = Vector2.Lerp(smoothedSize, needed, 0.22f);
        smoothedSize = Vector2.Max(smoothedSize, needed);

        Point content = new((int)MathF.Round(smoothedSize.X), (int)MathF.Ceiling(smoothedSize.Y));

        UpdateFit(nameWidth, nameHeight, finishedHeight);
        UpdateFacing();

        // The tail retracts as the box docks; at the bottom of the screen there is nobody to point at.
        float tail = 1f - DockBlend;

        // Two passes: the first sizes the bubble so the anchor can be compared against its centre,
        // the second rebuilds it with the tail actually leaning at the speaker.
        geometry = PanelStyle.Measure(content, nameWidth, nameHeight, 0f, tail);
        PlacePanel();
        geometry = PanelStyle.Measure(content, nameWidth, nameHeight, TailAim(), tail);
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
        Vector2 below = panelCentre + new Vector2(
            panelSize.X * 0.34f * facingBlend,
            panelSize.Y * 0.5f + size.Y * 0.8f);

        // Docked against the bottom edge there is no room below, so the stack rises above the bubble.
        Vector2 above = below - new Vector2(0f, panelSize.Y + size.Y * 1.6f + (count - 1) * size.Y * CHOICE_STEP);

        Vector2 origin = Vector2.Lerp(below, above, DockBlend);

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

        if (widest + CHOICE_PAD_X * 2 > room.X)
            TrelamiumIII.Instance?.Logger?.Warn($"Dialogue '{DialogueKey}': a choice is {widest} texels wide, more than a slot's {room.X - CHOICE_PAD_X * 2} - it will be cut off.");

        return new Point(
            Math.Min((int)MathF.Ceiling(widest) + CHOICE_PAD_X * 2, room.X),
            Math.Min((int)MathF.Ceiling(Font.LineSpacing * CHOICE_SCALE) + CHOICE_PAD_Y * 2, room.Y));
    }

    /// <summary>Screen pixels per texel for the response slots.</summary>
    private float Scale => PanelStyle.PIXEL * Main.UIScale;

    /// <summary>Screen pixels per texel for the bubble: the base scale, shrunk for long lines and tight space.</summary>
    private float BubbleScale => Scale * density * fit;

    /// <summary>
    ///     Shrinks the bubble just enough that the whole finished line fits between the speaker's head
    ///     and the top of the screen.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Without this a tall box hanging off someone near the top of the view is clamped onto the
    ///         screen, which pushes it down over the very speaker it is pointing at -- and its tail
    ///         ends up somewhere around their knees.
    ///     </para>
    ///     <para>
    ///         Measured against the finished line, not the revealed part, so the box does not shrink
    ///         a little more with every row that types in. Eased afterwards, because the room above a
    ///         speaker changes as the camera moves; snapped the first time so the box does not open at
    ///         full size and then deflate.
    ///     </para>
    /// </remarks>
    private void UpdateFit(int nameWidth, int nameHeight, float finishedHeight)
    {
        const float EDGE = 8f;

        float target = 1f;

        if (anchor.HasSpeaker)
        {
            BubbleGeometry finished = PanelStyle.Measure(
                new Point(wrapWidth, (int)MathF.Ceiling(finishedHeight)), nameWidth, nameHeight, 0f);

            // The region's top edge sits TailApex.Y texels above the tail's point.
            float room = anchor.ScreenPoint().Y - ANCHOR_GAP - EDGE;
            float wanted = finished.TailApex.Y * Scale * density;

            if (wanted > room)
                target = Math.Clamp(room / wanted, MIN_FIT, 1f);
        }

        // Docked, the room above the speaker no longer matters and the box draws a size down.
        target = MathHelper.Lerp(target, DOCK_SCALE, DockBlend);

        fit =fitSettled ? MathHelper.Lerp(fit, target, 0.2f) : target;
        fitSettled = true;
    }

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
        panelSize = new Vector2(geometry.Used.X, geometry.Used.Y) * BubbleScale;

        Vector2 target = anchor.ScreenPoint();

        float x = anchor.HasSpeaker ? target.X : Main.screenWidth * 0.5f;

        // The tail's point is what has to meet the speaker, and it is not the bottom of the region --
        // the border and the ghost hang past it. Anchoring on the region would leave a visible gap
        // that grew and shrank with the panel.
        float apexFromBottom = (geometry.Used.Y - geometry.TailApex.Y) * BubbleScale;
        float bottom = target.Y - ANCHOR_GAP + apexFromBottom;

        // The bubble is centred on the body rather than on the region, which is wider on the left by
        // the ghost margin. Without this the panel sits a few pixels off its speaker.
        float bodyOffset = (geometry.Body.X + geometry.Body.Width * 0.5f - geometry.Used.X * 0.5f) * BubbleScale;

        Vector2 hanging = new(
            OnScreen(x - bodyOffset, panelSize.X * 0.5f, Main.screenWidth),
            OnScreen(bottom - panelSize.Y * 0.5f, panelSize.Y * 0.5f, Main.screenHeight));

        Vector2 docked = new(
            OnScreen(Main.screenWidth * 0.5f - bodyOffset, panelSize.X * 0.5f, Main.screenWidth),
            OnScreen(Main.screenHeight - DOCK_MARGIN * Main.UIScale - panelSize.Y * 0.5f, panelSize.Y * 0.5f, Main.screenHeight));

        panelCentre = Vector2.Lerp(hanging, docked, DockBlend);
    }

    /// <summary>Smoothstepped <see cref="dock" />.</summary>
    private float DockBlend => dock * dock * (3f - 2f * dock);

    /// <summary>
    ///     Docks the box once the speaker is far away and brings it back once they are close again.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Two thresholds, so walking along the boundary does not slide the box back and forth.
    ///     </para>
    ///     <para>
    ///         A speaker off screen docks the box at any distance; undocking needs them back on screen
    ///         by <see cref="UNDOCK_INSET" />.
    ///     </para>
    ///     <para>
    ///         A line with no NPC behind it (the player's own, or a speaker not in the world) keeps
    ///         whatever state the conversation was in, falling back to the speaker it was opened with.
    ///         A conversation that opens with nobody to hang off at all starts docked.
    ///     </para>
    /// </remarks>
    private void UpdateDock(bool snap = false)
    {
        NPC? speaker = anchor.Speaker ?? requestedAnchor.Speaker;

        if (speaker is not null)
        {
            float threshold = (docked ? UNDOCK_TILES : DOCK_TILES) * 16f;
            bool far = Vector2.DistanceSquared(Main.LocalPlayer.Center, speaker.Center) >= threshold * threshold;

            float inset = docked ? UNDOCK_INSET : 0f;
            Vector2 point = Vector2.Transform(speaker.Center - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

            bool visible = point.X >= inset && point.X <= Main.screenWidth - inset
                        && point.Y >= inset && point.Y <= Main.screenHeight - inset;

            docked = far || !visible;
        }
        else if (!dockSettled)
        {
            docked = true;
        }

        float target = docked ? 1f : 0f;

        dock = snap || MathF.Abs(dock - target) < 0.002f ? target : MathHelper.Lerp(dock, target, 0.1f);
        dockSettled = true;
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

        float bodyCentre = panelCentre.X + (geometry.Body.X + geometry.Body.Width * 0.5f - geometry.Used.X * 0.5f) * BubbleScale;
        float reach = MathF.Max(geometry.Body.Width * 0.5f * BubbleScale, 1f);

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
        QuadPlacement rest = RestBubblePlacement();

        return tilt is null ? rest : tilt.Apply(rest);
    }

    private QuadPlacement RestBubblePlacement()
    {
        float entrance = Entrance;
        float settle = 1f - entrance;

        // A slow, shallow drift on two incommensurate periods. Enough to stop the panel reading as a
        // sprite pinned to the screen; small enough that nobody notices it moving.
        float time = Main.GlobalTimeWrappedHourly;

        // Docked, the rest angles, drift and swing-in all fall away so the box sits flat and still,
        // rising in from below on open and dropping out on close instead.
        float hang = 1f - DockBlend;

        return new QuadPlacement
        {
            Center = panelCentre + new Vector2(0f, settle * DOCK_DROP * DockBlend),
            Size = panelSize * MathHelper.Lerp(MathHelper.Lerp(0.74f, 0.96f, DockBlend), 1f, entrance),

            // Everything with a handedness is scaled by the blend, so a flip passes through zero --
            // the panel squares up to the viewer at the halfway point and carries on turning. The
            // idle drift is left alone: it is symmetric noise and has no side to be on.
            Yaw = (RestYaw + settle * EntryYaw) * hang * facingBlend + MathF.Sin(time * 0.7f) * 0.022f * hang,
            Pitch = (RestPitch + MathF.Sin(time * 0.53f) * 0.016f) * hang,
            Roll = (RestRoll + settle * 0.12f) * hang * facingBlend,
            Depth = settle * ENTRY_DEPTH * hang * PanelCamera.Distance,
            Warp = warp * hang,
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
        float hang = 1f - DockBlend;

        return new QuadPlacement
        {
            Center = centre,
            Size = size * MathHelper.Lerp(0.8f, 1f + lunge * 0.075f, eased),

            // Turns to face the reader as it comes forward, which is most of why the motion reads as
            // the slot stepping out rather than merely sliding sideways.
            Yaw = (RestYaw * MathHelper.Lerp(1.15f, 0.45f, hover) * hang - kick * 0.06f) * facingBlend,
            Pitch = RestPitch * 0.6f * hang,
            Roll = (RestRoll * (index % 2 == 0 ? 1f : -0.7f) * hang + kick * kick * 0.05f) * facingBlend,
            Depth = distance * (MathHelper.Lerp(index * CHOICE_STEP_DEPTH, -CHOICE_LIFT, lunge)
                                + (1f - eased) * CHOICE_ENTRY_DEPTH),
            Warp = QuadWarp.Handmade(seed + index * 31, 3f) * hang,
            FarShade = 0.2f,
        };
    }

    #endregion

    #region Drawing

    /// <summary>Rebuilds the off-screen faces. Driven by <see cref="ProjectedPanelSystem" />.</summary>
    public void BuildSurfaces()
    {
        if (current is not null && presence > 0f)
            surfaces.Build(this);
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        ProjectedQuad? quad = ProjectedPanelSystem.Quad;

        if (current is null || presence <= 0f || surfaces.Bubble is null || quad is null || !quad.Ready)
            return;

        HandleMouse();

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        Color tint = Color.White * presence;

        // The interface layer opened this batch; primitives cannot be issued inside one, so it is
        // closed and reopened around the quads exactly as the layer left it.
        spriteBatch.End();

        // Linear over the prescaled faces: soft seams between chunky texels, not a blur. See ProjectedSurfaces.PRESCALE.
        quad.Begin(device, SamplerState.LinearClamp);
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
    ///     -- a shake moves by the same visible distance at any panel scale.
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
                + renderContext.Offset / (PanelStyle.PIXEL * density * fit)
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
