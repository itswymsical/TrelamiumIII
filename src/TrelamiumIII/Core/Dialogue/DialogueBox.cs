using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria.Audio;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Dialogue;

public class DialogueBox : IInGameNotification, IDialoguePresenter
{
    #region Constants and Fields
    private const float ANIMATION_DURATION = 30f;
    private const float PANEL_WIDTH = 480f;
    private const int AUTO_REMOVE_DELAY = 200;

    private const float CHOICE_CENTER_OFFSET_Y = -80f;
    private const float CHOICE_ARM_LENGTH = 120f;   // distance from center point to each slot
    private const float CHOICE_SLOT_WIDTH = 220f;   // width of each choice backing panel
    private const float CHOICE_SLOT_HEIGHT = 36f;    // height of each choice backing panel

    private bool isRemoved;
    private float animationProgress;

    private bool isLastDialogue;
    private Vector2 targetPosition;
    private Vector2 startPosition;

    private int charIndex;
    private float pauseTimer;

    private readonly string dialogueKey;
    private readonly Queue<DialogueData> dialogueLines = new();
    private DialogueData currentDialogue = null;
    private int currentLineIndex;

    private readonly DialogueTick revealTick = new();
    private readonly SpeechVoice voice = new();
    private readonly DialogueRenderContext renderContext = new();

    // Rooting is per line rather than per conversation, so a two-hander faces the player with
    // whoever is currently saying something. See DialogueBehavior.
    private Projected.DialogueAnchor speakerAnchor;
    private NPC rootedSpeaker;
    private bool roots = true;

    // ---- Choice state ----
    private bool awaitingChoice;
    private int selectedChoiceIndex;

    private static Texture2D ArrowTexture => (Texture2D)Assets.Textures.UI.Dialogue.Next.Asset;
    private static Texture2D PortraitFrameTexture => (Texture2D)Assets.Textures.UI.Dialogue.PortraitFrame.Asset;
    private static Texture2D PortraitFrameInnerTexture => (Texture2D)Assets.Textures.UI.Dialogue.PortraitFrame_Inner.Asset;

    public string DialogueKey { get; private set; }
    #endregion

    #region Properties
    public bool ShouldZoom { get; set; }
    public bool ShouldBeRemoved => isRemoved && animationProgress >= ANIMATION_DURATION;
    public bool Completed { get; private set; }
    public ChoiceData? Chosen { get; private set; }
    public float Opacity => isRemoved
        ? 1f - animationProgress / ANIMATION_DURATION
        : MathHelper.Clamp(animationProgress / ANIMATION_DURATION, 0f, 1f);
    #endregion

    #region Public Methods
    public static DialogueBox CreateDialogue(string dialogueKey, bool zoomIn, bool roots = true)
    {
        var lines = DialogueBuilder.BuildSequence(dialogueKey);
        if (lines.Count == 0)
            return null;

        var box = new DialogueBox(dialogueKey) { ShouldZoom = zoomIn, roots = roots };

        foreach (var line in lines)
            box.dialogueLines.Enqueue(line);

        box.NextLine();
        return box;
    }

    private DialogueBox(string dialogueKey)
    {
        this.dialogueKey = dialogueKey;
        this.DialogueKey = dialogueKey;
    }

    /// <summary>
    ///     Picks the conversation up from another box, part-way through a line.
    /// </summary>
    /// <remarks>
    ///     No <see cref="NextLine" />: the line is already chosen and part-typed, and dequeuing here
    ///     would drop it and start the one behind it.
    /// </remarks>
    internal static DialogueBox Resume(DialogueHandoff handoff)
    {
        var box = new DialogueBox(handoff.Key) { ShouldZoom = handoff.ZoomIn, roots = handoff.Roots };

        foreach (var line in handoff.Remaining)
            box.dialogueLines.Enqueue(line);

        box.currentDialogue = handoff.Current;
        box.charIndex = handoff.Current is null
            ? 0
            : Math.Clamp(handoff.CharIndex, 0, handoff.Current.PlainText.Length);
        box.awaitingChoice = handoff.AwaitingChoice;
        box.selectedChoiceIndex = handoff.SelectedChoice;

        box.RootSpeaker();

        return box;
    }

    internal DialogueHandoff Capture() => new()
    {
        Key = DialogueKey,

        // A box on its way out has nothing left to hand over. Resuming its last line in another box
        // would re-ask a question the player already answered, and lose the answer.
        Current = isRemoved ? null : currentDialogue,
        Remaining = [.. dialogueLines],
        CharIndex = charIndex,
        AwaitingChoice = awaitingChoice,
        SelectedChoice = selectedChoiceIndex,
        ZoomIn = ShouldZoom,
        Roots = roots,
    };

    /// <summary>The line being spoken, for whoever needs to know who is talking.</summary>
    internal DialogueData CurrentLine => currentDialogue;

    /// <summary>
    ///     The face the current line is set in: the speaker's own, or the tooltip face.
    /// </summary>
    /// <remarks>
    ///     Everything measured from it is recomputed per frame from the live line, so a conversation
    ///     that changes speaker changes face mid-conversation without anything to invalidate.
    /// </remarks>
    private DynamicSpriteFont Font => currentDialogue?.Profile?.Font ?? FontAssets.MouseText.Value;

    /// <summary>
    ///     One glyph with a one-pixel outline, in a font of this box's choosing.
    /// </summary>
    /// <remarks>
    ///     Replaces <c>Utils.DrawBorderString</c>, which is hard-wired to vanilla's tooltip face and
    ///     so cannot honour a speaker's own. The anchor matches what that used: left edge, and a
    ///     third of the way down the line box.
    /// </remarks>
    private static void DrawGlyph(
        SpriteBatch spriteBatch, DynamicSpriteFont font, string text, Vector2 position, Color color, float scale)
    {
        Vector2 origin = font.MeasureString(text) * new Vector2(0f, 0.3f);
        Color border = Color.Black * (color.A / 255f);

        for (int i = 0; i < 4; i++)
        {
            Vector2 offset = i switch
            {
                0 => new Vector2(-1f, 0f),
                1 => new Vector2(1f, 0f),
                2 => new Vector2(0f, -1f),
                _ => new Vector2(0f, 1f),
            };

            spriteBatch.DrawString(font, text, position + offset, border, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        spriteBatch.DrawString(font, text, position, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    public void Update()
    {
        UpdateAnimation();
        UpdateTextDisplay();
        HandleInput();
    }

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        if (Opacity <= 0f || Main.gameMenu || currentDialogue == null) return;

        SetupPositions(bottomAnchorPosition);

        DrawPanel(spriteBatch);
        DrawPortrait(spriteBatch);
        DrawText(spriteBatch);

        if (awaitingChoice)
            DrawChoices(spriteBatch);
        else
            DrawSkip(spriteBatch);
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom) => positionAnchorBottom.Y -= 50f * Opacity;

    public void ForceComplete()
    {
        if (currentDialogue != null)
        {
            charIndex = currentDialogue.PlainText.Length;
            pauseTimer = 0f;
        }
    }

    public void Close()
    {
        if (!isRemoved)
        {
            isRemoved = true;
            animationProgress = 0f;
        }
    }
    #endregion

    #region Core Logic
    private void NextLine()
    {
        if (dialogueLines.Count > 0)
        {
            currentDialogue = dialogueLines.Dequeue();
            charIndex = 0;
            pauseTimer = 0f;
            awaitingChoice = false;
            selectedChoiceIndex = 0;
            currentLineIndex++;

            // Voice playback is per-line state, don't let it bleed across lines.
            voice.Reset();

            RootSpeaker();
        }
        else
        {
            isLastDialogue = true;
            Finish();
        }
    }

    /// <summary>
    ///     True while characters are actually arriving. Drives the speaker's mouth.
    /// </summary>
    private bool IsVoicing =>
        !isRemoved && pauseTimer <= 0 && currentDialogue != null && charIndex < currentDialogue.PlainText.Length;

    /// <summary>
    ///     Hands the line's speaker a talking loop, if it is not already running one.
    /// </summary>
    /// <remarks>
    ///     Only on a change of speaker: a run of lines from the same character stays one behaviour,
    ///     where re-rooting per line would stack a fresh interrupt and restart the animation
    ///     mid-sentence. The predicate reads <see cref="rootedSpeaker" />, so this field changing is
    ///     what releases whoever was talking before.
    /// </remarks>
    private void RootSpeaker()
    {
        if (!roots)
            return;

        speakerAnchor = Projected.DialogueAnchor.ForSpeaker(currentDialogue, speakerAnchor);

        NPC speaker = speakerAnchor.Speaker;

        if (ReferenceEquals(speaker, rootedSpeaker))
            return;

        rootedSpeaker = speaker;

        DialogueBehavior.Root(
            speaker,
            stillSpeaking: () => !isRemoved && ReferenceEquals(rootedSpeaker, speaker),
            voicing: () => IsVoicing);
    }

    private void UpdateAnimation()
    {
        if (!isRemoved || animationProgress < ANIMATION_DURATION)
            animationProgress = Math.Min(animationProgress + 1f, ANIMATION_DURATION);
    }

    private void UpdateTextDisplay()
    {
        if (Main.gameMenu || currentDialogue == null)
        {
            Close();
            return;
        }

        // A box on its way out keeps drawing but stops talking. Without this, one handing its
        // conversation to another box carries on typing and voicing the same line underneath it.
        if (isRemoved)
            return;

        if (pauseTimer > 0)
        {
            pauseTimer--;
            voice.Interrupt();
            return;
        }

        if (charIndex < currentDialogue.PlainText.Length)
        {
            voice.Tick();

            // Loops rather than revealing one glyph: the clock deals in fractional frames, so a fast
            // line can owe more than one character on the same frame.
            while (charIndex < currentDialogue.PlainText.Length)
            {
                // Gather reveal-time affects covering this glyph (timing / audio).
                revealTick.Reset(charIndex);
                var modifiers = currentDialogue.Modifiers;
                for (int i = 0; i < modifiers.Count; i++)
                    if (modifiers[i].Covers(charIndex))
                        modifiers[i].ApplyReveal(revealTick);

                if (!voice.TryReveal(currentDialogue.BaseDelay, currentDialogue.Speed, revealTick.SpeedMultiplier))
                    break;

                voice.Note(currentDialogue.PlainText[charIndex], revealTick);
                charIndex++;

                // A Pause affect holds the typewriter after the covered glyph appears.
                if (revealTick.HoldFrames > 0)
                {
                    pauseTimer = revealTick.HoldFrames;
                    break;
                }
            }

            voice.Play(currentDialogue.Profile, Main.LocalPlayer.position);
        }
        else if (currentDialogue.HasChoices && !awaitingChoice)
        {
            awaitingChoice = true;
            selectedChoiceIndex = 0;
        }
    }

    private void HandleInput()
    {
        if (isRemoved) return;

        if (awaitingChoice)
        {
            HandleChoiceInput();
            return;
        }

        if (KeybindsSystem.FFDialogueKeybind.JustPressed)
        {
            if (currentDialogue != null && charIndex < currentDialogue.PlainText.Length)
                ForceComplete();
            else if (dialogueLines.Count > 0)
                NextLine();
            else
                Finish();
        }
    }

    private void HandleChoiceInput()
    {
        var choices = currentDialogue.Choices;
        if (choices.Count == 0) return;

        int? directionSlot = null;

        if (directionSlot.HasValue && directionSlot.Value < choices.Count)
        {
            selectedChoiceIndex = directionSlot.Value;
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    private void ConfirmChoice()
    {
        var choice = currentDialogue.Choices[selectedChoiceIndex];
        SoundEngine.PlaySound(SoundID.MenuOpen);

        choice.OnSelected?.Invoke();

        // Recorded rather than started here: the manager opens the branch once this box is dropped,
        // carrying the conversation's music, zoom and style into it instead of opening it bare.
        Finish(choice);
    }

    /// <summary>Closes the box as the end of what it had to say, rather than as it being cut off.</summary>
    private void Finish(ChoiceData? choice = null)
    {
        if (isRemoved)
            return;

        Completed = true;
        Chosen = choice;
        Close();
    }
    #endregion

    #region Drawing
    private void OnMouseOver()
    {
        if (PlayerInput.IgnoreMouseInterface) return;

        // A box on its way out takes no clicks, and must not swallow the release latch either -- while
        // it fades, the click belongs to whatever is replacing it.
        if (isRemoved) return;

        Main.LocalPlayer.mouseInterface = true;

        if (!Main.mouseLeft || !Main.mouseLeftRelease) return;

        Main.mouseLeftRelease = false;

        if (awaitingChoice) return; // mouse click handled in DrawChoices via hover

        if (currentDialogue != null && charIndex < currentDialogue.PlainText.Length)
            ForceComplete();
        else if (dialogueLines.Count > 0)
            NextLine();
        else
            Finish();
    }

    private void SetupPositions(Vector2 bottomAnchorPosition)
    {
        if (targetPosition == Vector2.Zero)
        {
            var panelSize = CalculateSize();
            targetPosition = bottomAnchorPosition + new Vector2(0f, -panelSize.Y * 0.5f);
            startPosition = targetPosition + new Vector2(0f, panelSize.Y + 70f);
        }
    }

    private void DrawPanel(SpriteBatch spriteBatch)
    {
        var panelSize = CalculateSize();
        var panelPosition = CalculatePosition(panelSize);
        var panelRectangle = Utils.CenteredRectangle(panelPosition, panelSize);

        var isHovering = panelRectangle.Contains(Main.MouseScreen.ToPoint());
        var panelColor = currentDialogue.SpeakerColor * (isHovering ? 0.85f : 0.7f) * Opacity;

        DrawUtils.DrawPanel(spriteBatch, panelRectangle, panelColor);

        if (isHovering) OnMouseOver();
    }

    private void DrawPortrait(SpriteBatch spriteBatch)
    {
        var panelSize = CalculateSize();
        var panelPosition = CalculatePosition(panelSize);
        var panelRectangle = Utils.CenteredRectangle(panelPosition, panelSize);

        var isHovering = panelRectangle.Contains(Main.MouseScreen.ToPoint());
        var panelColor = currentDialogue.SpeakerColor * (isHovering ? 0.85f : 0.7f) * Opacity;

        Vector2 iconSize = new(92, 92);
        Vector2 iconPosition = new((panelRectangle.Left - iconSize.X) + 5, panelRectangle.Bottom - iconSize.Y - 7);

        var portraitTexture = currentDialogue.Profile?.Portrait;

        Vector2 frameSize = new(PortraitFrameTexture.Width, PortraitFrameTexture.Height);
        var framePosition = iconPosition - (frameSize - iconSize) / 2;

        if (!currentDialogue.IsPlayer)
        {
            spriteBatch.Draw(PortraitFrameInnerTexture, framePosition, null, panelColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            if (portraitTexture != null)
            {
                var sourceRect = GetFrameRect(currentDialogue.Emote, 92, 92);
                spriteBatch.Draw(portraitTexture.Value, new(iconPosition.X - 13, iconPosition.Y + 1), sourceRect, Color.White * Opacity, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(PortraitFrameTexture, framePosition, null, Color.White * Opacity, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            DrawSpeakerName(spriteBatch, iconPosition, iconSize);
        }
    }

    private void DrawSpeakerName(SpriteBatch spriteBatch, Vector2 iconPosition, Vector2 iconSize)
    {
        var speakerName = currentDialogue.Speaker;
        var nameTextSize = FontAssets.ItemStack.Value.MeasureString(speakerName);
        Vector2 nameTextPosition = new((iconPosition.X + iconSize.X / 2 - nameTextSize.X / 2) - 18, iconPosition.Y + iconSize.Y + 18f);

        Utils.DrawBorderString(spriteBatch, speakerName, nameTextPosition, Color.White * Opacity, 1f, anchorx: 0f, anchory: 0.5f);
    }

    private void DrawText(SpriteBatch spriteBatch)
    {
        var panelSize = CalculateSize();
        var panelPosition = CalculatePosition(panelSize);
        var panelRectangle = Utils.CenteredRectangle(panelPosition, panelSize);

        var displayText = currentDialogue.PlainText[..Math.Min(charIndex, currentDialogue.PlainText.Length)];
        var textPosition = panelRectangle.TopLeft() + new Vector2(15f, 32f);
        var wrappedText = WrapText(displayText, PANEL_WIDTH - 90f, Font);

        var globalCharIndex = 0;

        for (var i = 0; i < wrappedText.Length; i++)
        {
            var linePos = textPosition + new Vector2(-2f, i * Font.LineSpacing);
            var charOffset = 0;

            foreach (var c in wrappedText[i])
            {
                var charPos = linePos + new Vector2(charOffset, 0f);

                // Gather draw-time affects covering this glyph (position / color).
                renderContext.Reset(globalCharIndex, currentDialogue.TextColor);
                var modifiers = currentDialogue.Modifiers;
                for (int m = 0; m < modifiers.Count; m++)
                    if (modifiers[m].Covers(globalCharIndex))
                        modifiers[m].ApplyDraw(renderContext);

                charPos += renderContext.Offset;

                DrawGlyph(spriteBatch, Font, c.ToString(), charPos, renderContext.Color * Opacity, .98f);
                charOffset += (int)Font.MeasureString(c.ToString()).X;
                globalCharIndex++;
            }
        }
    }

    /// <summary>
    /// Draws choices in a cross layout above the box.
    /// Choice slot array: 0 = Top, 1 = Left, 2 = Right, 3 = Bottom.
    /// </summary>
    private void DrawChoices(SpriteBatch spriteBatch)
    {
        if (currentDialogue == null || !currentDialogue.HasChoices) return;

        var choices = currentDialogue.Choices;

        // pre-calculate slot positions based on choice count to avoid doing math in the draw loop
        Vector2[] slotOffsets = new Vector2[choices.Count];

        if (choices.Count == 1)
        {
            slotOffsets[0] = new Vector2(0f, CHOICE_CENTER_OFFSET_Y);
        }
        else if (choices.Count == 2)
        {
            slotOffsets[0] = new Vector2(-CHOICE_ARM_LENGTH, CHOICE_CENTER_OFFSET_Y);
            slotOffsets[1] = new Vector2(CHOICE_ARM_LENGTH, CHOICE_CENTER_OFFSET_Y);
        }
        else if (choices.Count == 3)
        {
            slotOffsets[0] = new Vector2(-CHOICE_ARM_LENGTH, CHOICE_CENTER_OFFSET_Y);
            slotOffsets[1] = new Vector2(0f, CHOICE_CENTER_OFFSET_Y - CHOICE_ARM_LENGTH / 2);
            slotOffsets[2] = new Vector2(CHOICE_ARM_LENGTH, CHOICE_CENTER_OFFSET_Y);
        }
        else if (choices.Count == 4)
        {
            slotOffsets[0] = new Vector2(0f, CHOICE_CENTER_OFFSET_Y - CHOICE_ARM_LENGTH);
            slotOffsets[1] = new Vector2(-CHOICE_ARM_LENGTH, CHOICE_CENTER_OFFSET_Y / 2);
            slotOffsets[2] = new Vector2(CHOICE_ARM_LENGTH, CHOICE_CENTER_OFFSET_Y / 2);
            slotOffsets[3] = new Vector2(0f, CHOICE_CENTER_OFFSET_Y + CHOICE_ARM_LENGTH);
        }

        Vector2 center = new(
            Main.screenWidth / 2.015f,
            Main.screenHeight / 1.1f + CHOICE_CENTER_OFFSET_Y
        );

        for (int i = 0; i < choices.Count; i++)
        {
            var slotCenter = center + slotOffsets[i];
            var slotRect = new Rectangle(
                (int)(slotCenter.X - CHOICE_SLOT_WIDTH / 2f),
                (int)(slotCenter.Y - CHOICE_SLOT_HEIGHT / 2f),
                (int)CHOICE_SLOT_WIDTH,
                (int)CHOICE_SLOT_HEIGHT
            );

            var isHovered = slotRect.Contains(Main.MouseScreen.ToPoint());
            var isSelected = i == selectedChoiceIndex;

            // A box on its way out takes no clicks. The choices keep drawing through the fade, and a
            // second click there would confirm again: the callback re-runs and the answer is replaced.
            if (isHovered && !isRemoved)
            {
                if (i != selectedChoiceIndex)
                {
                    selectedChoiceIndex = i;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }

                Main.LocalPlayer.mouseInterface = true;
                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    Main.mouseLeftRelease = false;
                    ConfirmChoice();
                    return;
                }
            }

            // select context color
            var panelAlpha = isSelected ? 0.75f : 0.4f;

            Utils.DrawSettingsPanel(spriteBatch, new Vector2(slotCenter.X - CHOICE_SLOT_WIDTH / 2f,
                slotCenter.Y - CHOICE_SLOT_HEIGHT / 1.75f), CHOICE_SLOT_WIDTH, currentDialogue.SpeakerColor * panelAlpha * Opacity);

            // Label
            var textColor = isSelected ? Color.White : new Color(180, 180, 180);
            Utils.DrawBorderString(
                spriteBatch,
                choices[i].Text,
                slotCenter,
                textColor * Opacity,
                0.85f,
                anchorx: 0.5f,
                anchory: 0.5f
            );
        }
    }

    private void DrawSkip(SpriteBatch spriteBatch)
    {
        if (currentDialogue == null || charIndex < currentDialogue.PlainText.Length || dialogueLines.Count == 0 && isLastDialogue)
            return;

        var panelSize = CalculateSize();
        var panelPosition = CalculatePosition(panelSize);
        var panelRectangle = Utils.CenteredRectangle(panelPosition, panelSize);

        var arrowScale = 0.65f;
        var arrowPosition = panelRectangle.BottomRight() - new Vector2(ArrowTexture.Width * arrowScale + 10, ArrowTexture.Height * arrowScale + 20);

        var yOffset = (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 3f;
        arrowPosition.Y += yOffset;

        Utils.DrawBorderString(spriteBatch, $"{KeybindsSystem.FFDialogueKeybind.GetAssignedKeys().FirstOrDefault() ?? "[None]"}", new(arrowPosition.X - 20, arrowPosition.Y + 12), Color.White * Opacity, 0.75f, anchorx: 0f, anchory: 0.5f);
        spriteBatch.Draw(ArrowTexture, arrowPosition, null, Color.White * Opacity, 0f, Vector2.Zero, arrowScale, SpriteEffects.None, 0f);
    }
    #endregion

    #region Helper Methods

    private Rectangle GetFrameRect(int emoteFrame, int frameWidth, int frameHeight)
    {
        var frameCount = currentDialogue.Profile?.FrameCount ?? 1;
        var safeFrame = Math.Max(0, Math.Min(emoteFrame, frameCount - 1));
        return new Rectangle(0, safeFrame * frameHeight, frameWidth, frameHeight);
    }

    /// <remarks>
    ///     Measured with the same font the text is drawn in. It used to take its line height from the
    ///     item-stack face while <see cref="DrawText" /> laid the lines out with the tooltip face, so
    ///     a panel was sized about ten pixels per line short of what it was holding and anything past
    ///     two lines ran out of the bottom of it.
    /// </remarks>
    private Vector2 CalculateSize()
    {
        if (currentDialogue == null) return new Vector2(PANEL_WIDTH + 18, 120);

        var displayText = currentDialogue.PlainText[..Math.Min(charIndex, currentDialogue.PlainText.Length)];
        var wrappedText = WrapText(displayText, PANEL_WIDTH - 90f, Font);
        var lineCount = wrappedText.Length;
        var textHeight = Font.LineSpacing * lineCount;

        var panelHeight = textHeight + 52f;
        return new Vector2(PANEL_WIDTH, Math.Max(panelHeight, 120));
    }

    private Vector2 CalculatePosition(Vector2 panelSize)
    {
        var t = isRemoved ? animationProgress / ANIMATION_DURATION : 1f - animationProgress / ANIMATION_DURATION;
        var currentPosition = Vector2.Lerp(targetPosition, startPosition, t);
        return currentPosition - new Vector2(0f, panelSize.Y * 0.65f);
    }

    private static string[] WrapText(string text, float maxWidth, DynamicSpriteFont font)
    {
        List<string> lines = [];
        StringBuilder currentLine = new();
        var spaceWidth = font.MeasureString(" ").X;

        foreach (var word in text.Split(' '))
        {
            var wordWidth = font.MeasureString(word).X;
            if (currentLine.Length > 0 && font.MeasureString(currentLine.ToString()).X + wordWidth + spaceWidth > maxWidth)
            {
                lines.Add(currentLine.ToString());
                currentLine.Clear();
            }
            if (currentLine.Length > 0)
                currentLine.Append(" ");
            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
            lines.Add(currentLine.ToString());

        return [.. lines];
    }
    #endregion
}
