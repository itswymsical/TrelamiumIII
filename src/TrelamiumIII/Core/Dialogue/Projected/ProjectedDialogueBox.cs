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
///         The same conversation as <see cref="DialogueBox" />, presented the other way round. That
///         one is a panel: it owns the bottom of the screen, holds a fixed width, and the world is
///         something happening behind it. This one is an object in the scene -- it hangs off its
///         speaker, grows to the shape of what is being said, and is drawn small and chunky enough
///         that it reads as a thing rather than as an interface.
///     </para>
///     <para>
///         Three ideas carry that. The panel is authored on a coarse texel grid and magnified with
///         point sampling, so it is pixel art at whatever size it ends up. It is drawn as a textured
///         quad through a real perspective camera rather than blitted, so its far edge genuinely
///         converges instead of being sheared. And it is measured to its content every frame, so the
///         box arrives at the width of the sentence in it as the sentence arrives.
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
    private const int TEXT_WIDTH = 230;

    private const int MIN_TEXT_WIDTH = 34;

    /// <summary>
    ///     Font scale used when drawing into the surface. Never take this below 1.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The glyph atlas is antialiased, so minifying it samples grey into the middle of every
    ///         stroke -- and this surface is then magnified by <see cref="PanelStyle.PIXEL" />, which
    ///         takes each of those grey samples and blows it up into a visible chunk of fringe. Text
    ///         drawn at half scale and doubled back up does not come out where it started; it comes
    ///         out soft.
    ///     </para>
    ///     <para>
    ///         Sharp pixel text is therefore a small font magnified, never a large one scaled down,
    ///         which is why the body is set in the item-stack face rather than the tooltip face. At 1
    ///         each glyph is copied from the atlas exactly as it was baked, and the only thing that
    ///         happens to it afterwards is being made bigger.
    ///     </para>
    /// </remarks>
    private const float TEXT_SCALE = 1f;

    private const float NAME_SCALE = TEXT_SCALE;
    private const float CHOICE_SCALE = TEXT_SCALE;

    /// <summary>How far outside the bubble a click still counts, in screen pixels.</summary>
    private const float HIT_PADDING = 10f;

    private const int CHOICE_PAD_X = 8;
    private const int CHOICE_PAD_Y = 3;

    /// <summary>Screen pixels between the tail's point and the speaker's head.</summary>
    private const float ANCHOR_GAP = 6f;

    private static readonly float RestYaw = MathHelper.ToRadians(15f);
    private static readonly float RestPitch = MathHelper.ToRadians(-7f);
    private static readonly float RestRoll = MathHelper.ToRadians(-1.6f);

    /// <summary>Extra turn the box arrives carrying, unwound as it settles.</summary>
    private static readonly float EntryYaw = MathHelper.ToRadians(-38f);

    // Depths are fractions of the camera's distance rather than pixel amounts. The distance is solved
    // from the screen height, so a fixed push would be half the scene at 720p and a third of it at
    // 1440p -- the same animation reading as a different one per resolution.

    /// <summary>How far back the box starts, as a fraction of the camera's distance.</summary>
    private const float ENTRY_DEPTH = 0.46f;

    /// <summary>How far forward the selected response steps out of the stack.</summary>
    private const float CHOICE_LIFT = 0.04f;

    /// <summary>How far back a response slot arrives from.</summary>
    private const float CHOICE_ENTRY_DEPTH = 0.3f;

    /// <summary>Stagger between slots in the stack, so each has its own plane to sit in.</summary>
    private const float CHOICE_STEP_DEPTH = 0.016f;

    #endregion

    #region State

    private readonly Queue<DialogueData> pending = new();
    private readonly ProjectedSurfaces surfaces = new();
    private readonly DialogueTick revealTick = new();
    private readonly DialogueRenderContext renderContext = new();
    private readonly QuadWarp warp;
    private readonly int seed;

    private DialogueAnchor anchor;

    private DialogueData? current;
    private GlyphLayout layout = GlyphLayout.Empty;

    /// <summary>Frame each character was revealed on, indexed alongside the line's own characters.</summary>
    private int[] revealedOn = [];

    private int charIndex;
    private int charTimer;
    private int pauseTimer;
    private int voiceCursor;
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

    private KeyboardState previousKeys;

    #endregion

    private ProjectedDialogueBox(string dialogueKey, DialogueAnchor anchor)
    {
        DialogueKey = dialogueKey;
        this.anchor = anchor;

        seed = Main.rand?.Next(1, 9999) ?? 1;
        warp = QuadWarp.Handmade(seed, 5f);

        surfaces.Initialize();
    }

    /// <summary>Builds a box for a registered sequence, or null when the key has no lines.</summary>
    public static ProjectedDialogueBox? Create(string dialogueKey, bool zoomIn, DialogueAnchor anchor)
    {
        List<DialogueData> lines = DialogueBuilder.BuildSequence(dialogueKey);

        if (lines.Count == 0)
            return null;

        var box = new ProjectedDialogueBox(dialogueKey, anchor) { ShouldZoom = zoomIn };

        foreach (DialogueData line in lines)
            box.pending.Enqueue(line);

        box.NextLine();

        ProjectedDialogueSystem.Register(box);

        return box;
    }

    public string DialogueKey { get; }

    public bool ShouldZoom { get; private init; }

    public bool ShouldBeRemoved => closing && presence <= 0f;

    /// <summary>
    ///     The item-stack face, not the tooltip face the panel box uses.
    /// </summary>
    /// <remarks>
    ///     It is baked at roughly two thirds the size, which is the whole reason it is here: at
    ///     <see cref="TEXT_SCALE" /> of 1 it lands on the texel grid small enough to be magnified
    ///     rather than needing to be shrunk, and comes out the far side of the pixelation crisp.
    ///     It also ends up larger on screen than the tooltip face did at half scale, because a small
    ///     font doubled is bigger than a large font halved and doubled.
    /// </remarks>
    private static DynamicSpriteFont Font => FontAssets.ItemStack.Value;

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
            Close();
            return;
        }

        current = pending.Dequeue();
        layout = GlyphLayout.Build(current.PlainText, Font, TEXT_SCALE, TEXT_WIDTH);
        revealedOn = new int[current.PlainText.Length];

        charIndex = 0;
        charTimer = 0;
        pauseTimer = 0;
        voiceCursor = 0;
        awaitingChoice = false;
        selectedChoice = 0;

        choiceSlot = MeasureChoiceSlot();
    }

    private void UpdatePresence()
    {
        if (closing)
            presence = MathF.Max(presence - 1f / CLOSE_FRAMES, 0f);
        else
            presence = MathF.Min(presence + 1f / OPEN_FRAMES, 1f);
    }

    private void UpdateTyping()
    {
        if (current is null)
            return;

        if (pauseTimer > 0)
        {
            pauseTimer--;
            return;
        }

        if (charIndex >= current.PlainText.Length)
        {
            if (current.HasChoices)
                awaitingChoice = true;

            return;
        }

        // Reveal-time affects covering this glyph: timing, pitch, one-shot sounds.
        revealTick.Reset(charIndex);

        IReadOnlyList<LineModifier> modifiers = current.Modifiers;
        for (int i = 0; i < modifiers.Count; i++)
        {
            if (modifiers[i].Covers(charIndex))
                modifiers[i].Affect.OnReveal(revealTick);
        }

        charTimer++;

        int delay = Math.Max(1, (int)(current.BaseDelay / (current.Speed * revealTick.SpeedMultiplier)));

        if (charTimer < delay)
            return;

        charTimer = 0;
        revealedOn[charIndex] = age;
        charIndex++;

        PlayVoice();

        if (revealTick.HoldFrames > 0)
            pauseTimer = revealTick.HoldFrames;
    }

    private void PlayVoice()
    {
        // Silence on whitespace. At this typing speed a voice firing on the gaps between words reads
        // as a stutter rather than as speech.
        if (current is null || char.IsWhiteSpace(current.PlainText[charIndex - 1]))
            return;

        SoundStyle? sound = revealTick.Sound ?? current.Profile?.Voice.Next(ref voiceCursor);

        if (!sound.HasValue)
            return;

        SoundStyle style = sound.Value;

        if (revealTick.Pitch != 1f)
            style = style with { Pitch = revealTick.Pitch - 1f };

        SoundEngine.PlaySound(style, Main.LocalPlayer.position);
    }

    private bool LineFinished => current is not null && charIndex >= current.PlainText.Length;

    #endregion

    #region Input

    /// <remarks>
    ///     Only the chat box shuts the whole thing down -- it eats the same keys, and a player typing
    ///     "1" into it should not be answering an NPC with their first response. The mouse gate is
    ///     narrower than that and belongs on the mouse alone: gating the keybind on it too meant a
    ///     player holding an item could not advance the conversation at all.
    /// </remarks>
    private void UpdateInput()
    {
        if (Main.drawingPlayerChat)
            return;

        if (awaitingChoice)
        {
            UpdateChoiceInput();
            return;
        }

        if (KeybindsSystem.FFDialogueKeybind?.JustPressed == true)
        {
            Advance();
            return;
        }

        if (!MouseOverBubble())
            return;

        // Claimed while the cursor is over the bubble, so the click that advances a line does not
        // also swing whatever the player is holding.
        Main.LocalPlayer.mouseInterface = true;

        if (Main.mouseLeft && Main.mouseLeftRelease)
        {
            Main.mouseLeftRelease = false;
            Advance();
        }
        else if (Main.mouseRight && Main.mouseRightRelease)
        {
            Main.mouseRightRelease = false;
            EndConversation();
        }
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
            Close();
    }

    /// <summary>Drops what is left of the conversation, rather than advancing through it.</summary>
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

        int hovered = HoveredChoice(count);

        if (hovered >= 0)
        {
            Select(hovered);

            Main.LocalPlayer.mouseInterface = true;

            if (Main.mouseLeft && Main.mouseLeftRelease)
            {
                Main.mouseLeftRelease = false;
                Confirm();
                return;
            }
        }

        if (Pressed(Keys.Enter) || KeybindsSystem.FFDialogueKeybind?.JustPressed == true)
            Confirm();
    }

    private void Select(int index)
    {
        if (index == selectedChoice)
            return;

        selectedChoice = index;
        SoundEngine.PlaySound(SoundID.MenuTick);
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

        // Queued before closing so the manager starts the branch the moment this box is dropped, and
        // in this box's own style and anchor -- a branch is the same conversation continuing, and
        // would otherwise answer a bubble over the speaker's head with a panel along the bottom of
        // the screen.
        if (!string.IsNullOrEmpty(choice.BranchKey))
            DialogueManager.Instance.StartDialogue(
                choice.BranchKey, style: DialogueStyle.Projected, anchor: anchor);

        Close();
    }

    private bool Pressed(Keys key) => Main.keyState.IsKeyDown(key) && !previousKeys.IsKeyDown(key);

    /// <remarks>
    ///     Padded, because the target is a small quad standing at an angle: its silhouette on screen
    ///     is narrower than the panel looks, and its edges are the part a player aims at.
    /// </remarks>
    private bool MouseOverBubble() =>
        !PlayerInput.IgnoreMouseInterface
        && PanelCamera.Contains(BubblePlacement(), Main.MouseScreen, HIT_PADDING);

    private int HoveredChoice(int count)
    {
        if (PlayerInput.IgnoreMouseInterface)
            return -1;

        for (int i = 0; i < count; i++)
        {
            if (PanelCamera.Contains(ChoicePlacement(i), Main.MouseScreen, HIT_PADDING))
                return i;
        }

        return -1;
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

        // Two passes: the first sizes the bubble so the anchor can be compared against its centre,
        // the second rebuilds it with the tail actually leaning at the speaker.
        geometry = PanelStyle.Measure(content, nameWidth, nameHeight, 0f);
        PlacePanel();
        geometry = PanelStyle.Measure(content, nameWidth, nameHeight, TailAim());
        PlacePanel();
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
        int width = Math.Min(
            (int)MathF.Ceiling(widest) + CHOICE_PAD_X * 2,
            ProjectedSurfaces.CHOICE_WIDTH - PanelStyle.MARGIN - PanelStyle.BORDER);

        int height = (int)MathF.Ceiling(Font.LineSpacing * CHOICE_SCALE) + CHOICE_PAD_Y * 2;

        return new Point(width, height);
    }

    private float Scale => PanelStyle.PIXEL * Main.UIScale;

    /// <summary>Puts the surface's used region on screen with its tail just clear of the speaker.</summary>
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
            Yaw = RestYaw + settle * EntryYaw + MathF.Sin(time * 0.7f) * 0.022f,
            Pitch = RestPitch + MathF.Sin(time * 0.53f) * 0.016f,
            Roll = RestRoll + settle * 0.12f,
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
        Vector2 size = new Vector2(footprint.X, footprint.Y) * Scale;

        bool selected = index == selectedChoice;

        // Staggered so a slot lands after the one above it, and after the bubble has settled.
        float arrival = Math.Clamp((presence - 0.55f) / 0.45f - index * 0.22f, 0f, 1f);
        float eased = arrival * arrival * (3f - 2f * arrival);

        Vector2 origin = panelCentre + new Vector2(panelSize.X * 0.34f, panelSize.Y * 0.5f + size.Y * 0.8f);

        // The step down has to clear the slot's own height plus the room its tilt and its selected
        // scale-up take, or the stack reads as overlapping cards rather than as a list.
        Vector2 centre = origin + new Vector2(
            index * size.X * 0.09f + (selected ? -size.X * 0.06f : 0f),
            index * size.Y * 1.34f);

        centre += new Vector2(60f, 0f) * (1f - eased);

        float distance = PanelCamera.Distance;

        return new QuadPlacement
        {
            Center = new Vector2(
                OnScreen(centre.X, size.X * 0.5f, Main.screenWidth),
                OnScreen(centre.Y, size.Y * 0.5f, Main.screenHeight)),
            Size = size * MathHelper.Lerp(0.8f, selected ? 1.06f : 1f, eased),
            Yaw = RestYaw * (selected ? 0.55f : 1.15f),
            Pitch = RestPitch * 0.6f,
            Roll = RestRoll * (index % 2 == 0 ? 1f : -0.7f),
            Depth = distance * ((selected ? -CHOICE_LIFT : index * CHOICE_STEP_DEPTH)
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
                    modifiers[m].Affect.OnDraw(renderContext);
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
