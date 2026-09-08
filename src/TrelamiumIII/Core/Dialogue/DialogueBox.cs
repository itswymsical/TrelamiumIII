using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria.Audio;
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

    private int charDisplayTimer;
    private int charIndex;
    private float pauseTimer;

    private readonly string dialogueKey;
    private readonly Queue<DialogueData> dialogueLines = new();
    private DialogueData currentDialogue = null;
    private int currentLineIndex;

    private int voiceCursor;

    private readonly DialogueTick revealTick = new();
    private readonly DialogueRenderContext renderContext = new();

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
    public float Opacity => isRemoved
        ? 1f - animationProgress / ANIMATION_DURATION
        : MathHelper.Clamp(animationProgress / ANIMATION_DURATION, 0f, 1f);
    #endregion

    #region Public Methods
    public static DialogueBox CreateDialogue(string dialogueKey, bool zoomIn)
    {
        var lines = DialogueBuilder.BuildSequence(dialogueKey);
        if (lines.Count == 0)
            return null;

        var box = new DialogueBox(dialogueKey) { ShouldZoom = zoomIn };

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
            voiceCursor = 0;
        }
        else
        {
            isRemoved = true;
            isLastDialogue = true;
            animationProgress = 0f;
        }
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

        if (pauseTimer > 0)
        {
            pauseTimer--;
            return;
        }

        if (charIndex < currentDialogue.PlainText.Length)
        {
            // Gather reveal-time affects covering this glyph (timing / audio).
            revealTick.Reset(charIndex);
            var modifiers = currentDialogue.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
                if (modifiers[i].Covers(charIndex))
                    modifiers[i].Affect.OnReveal(revealTick);

            charDisplayTimer++;
            var currentDelay = Math.Max(1, (int)(currentDialogue.BaseDelay / (currentDialogue.Speed * revealTick.SpeedMultiplier)));

            if (charDisplayTimer >= currentDelay)
            {
                charDisplayTimer = 0;
                charIndex++;

                PlayTypeSound(revealTick);

                // A Pause affect holds the typewriter after the covered glyph appears.
                if (revealTick.HoldFrames > 0)
                    pauseTimer = revealTick.HoldFrames;
            }
        }
        else if (currentDialogue.HasChoices && !awaitingChoice)
        {
            awaitingChoice = true;
            selectedChoiceIndex = 0;
        }
    }

    private void PlayTypeSound(DialogueTick tick)
    {
        if (charIndex > currentDialogue.PlainText.Length)
            return;

        // A Sound affect overrides the voice for this glyph; otherwise the speaker's voice plays.
        var sound = tick.Sound ?? currentDialogue.Profile?.Voice.Next(ref voiceCursor);
        if (!sound.HasValue)
            return;

        var style = sound.Value;
        if (tick.Pitch != 1.0f)
            style = style with { Pitch = tick.Pitch - 1.0f };

        SoundEngine.PlaySound(style, Main.LocalPlayer.position);
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
                Close();
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

        // Run the choice's callback, then branch if it specifies one.
        choice.OnSelected?.Invoke();

        // Queue the branch before closing so EndDialogue fires it immediately.
        if (!string.IsNullOrEmpty(choice.BranchKey))
            DialogueManager.Instance.StartDialogue(choice.BranchKey);

        Close();
    }
    #endregion

    #region Drawing
    private void OnMouseOver()
    {
        if (PlayerInput.IgnoreMouseInterface) return;

        Main.LocalPlayer.mouseInterface = true;

        if (!Main.mouseLeft || !Main.mouseLeftRelease || isLastDialogue && isRemoved) return;

        Main.mouseLeftRelease = false;

        if (isRemoved) return;

        if (awaitingChoice) return; // mouse click handled in DrawChoices via hover

        if (currentDialogue != null && charIndex < currentDialogue.PlainText.Length)
            ForceComplete();
        else if (dialogueLines.Count > 0)
            NextLine();
        else
            Close();
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
        var wrappedText = WrapText(displayText, PANEL_WIDTH - 90f);

        var globalCharIndex = 0;

        for (var i = 0; i < wrappedText.Length; i++)
        {
            var linePos = textPosition + new Vector2(-2f, i * FontAssets.MouseText.Value.LineSpacing);
            var charOffset = 0;

            foreach (var c in wrappedText[i])
            {
                var charPos = linePos + new Vector2(charOffset, 0f);

                // Gather draw-time affects covering this glyph (position / color).
                renderContext.Reset(globalCharIndex, currentDialogue.TextColor);
                var modifiers = currentDialogue.Modifiers;
                for (int m = 0; m < modifiers.Count; m++)
                    if (modifiers[m].Covers(globalCharIndex))
                        modifiers[m].Affect.OnDraw(renderContext);

                charPos += renderContext.Offset;

                Utils.DrawBorderString(spriteBatch, c.ToString(), charPos, renderContext.Color * Opacity, .98f, anchorx: 0f, anchory: 0.3f);
                charOffset += (int)FontAssets.MouseText.Value.MeasureString(c.ToString()).X;
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

            if (isHovered)
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

    private Vector2 CalculateSize()
    {
        if (currentDialogue == null) return new Vector2(PANEL_WIDTH + 18, 120);

        var displayText = currentDialogue.PlainText[..Math.Min(charIndex, currentDialogue.PlainText.Length)];
        var wrappedText = WrapText(displayText, PANEL_WIDTH - 90f);
        var lineCount = wrappedText.Length;
        var textHeight = FontAssets.ItemStack.Value.LineSpacing * lineCount;

        var panelHeight = textHeight + 52f;
        return new Vector2(PANEL_WIDTH, Math.Max(panelHeight, 120));
    }

    private Vector2 CalculatePosition(Vector2 panelSize)
    {
        var t = isRemoved ? animationProgress / ANIMATION_DURATION : 1f - animationProgress / ANIMATION_DURATION;
        var currentPosition = Vector2.Lerp(targetPosition, startPosition, t);
        return currentPosition - new Vector2(0f, panelSize.Y * 0.65f);
    }

    private static string[] WrapText(string text, float maxWidth)
    {
        List<string> lines = [];
        StringBuilder currentLine = new();
        var spaceWidth = FontAssets.MouseText.Value.MeasureString(" ").X;

        foreach (var word in text.Split(' '))
        {
            var wordWidth = FontAssets.MouseText.Value.MeasureString(word).X;
            if (currentLine.Length > 0 && FontAssets.MouseText.Value.MeasureString(currentLine.ToString()).X + wordWidth + spaceWidth > maxWidth)
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
