using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.GameInput;

namespace TrelamiumIII.Content.DevTools.VfxLab.UI;

/// <summary> The lab's colours. Muted and dark, so whatever's being tuned behind the panels is the brightest thing on screen. </summary>
internal static class LabTheme
{
    public static readonly Color Panel = new Color(18, 20, 30) * 0.94f;
    public static readonly Color PanelEdge = new(58, 64, 88);
    public static readonly Color Inset = new(30, 33, 48);
    public static readonly Color Hover = new(44, 49, 70);
    public static readonly Color Control = new(38, 42, 60);
    public static readonly Color Fill = new(62, 74, 112);
    public static readonly Color Selected = new(62, 84, 140);
    public static readonly Color Accent = new(120, 190, 255);
    public static readonly Color Changed = new(255, 190, 90);
    public static readonly Color Live = new(110, 215, 130);
    public static readonly Color Failure = new(255, 120, 110);
    public static readonly Color Text = new(225, 228, 240);
    public static readonly Color Muted = new(145, 150, 175);
}

/// <summary> One frame of the mouse and keys, in UI coordinates. A harness can supply these to drive the panels with no real input. </summary>
internal struct LabInput
{
    public Point Mouse;
    public bool LeftDown;
    public bool LeftPressed;
    public bool LeftReleased;
    public bool RightDown;
    public bool RightPressed;
    public int Scroll;
    public bool Shift;
    public bool Control;
    public bool EnterDown;
    public bool EnterPressed;
    public bool EscapeDown;
    public bool EscapePressed;
    public bool ZDown;
    public bool YDown;

    /// <summary> Ctrl+Z. </summary>
    public bool UndoPressed;

    /// <summary> Ctrl+Y. </summary>
    public bool RedoPressed;

    /// <summary> What a focused text field should now hold, or null to read the keyboard. </summary>
    public string? ScriptedText;

    public static LabInput Read(in LabInput last)
    {
        bool left = Main.mouseLeft && Main.hasFocus;
        bool right = Main.mouseRight && Main.hasFocus;
        bool enter = Main.keyState.IsKeyDown(Keys.Enter);
        bool escape = Main.keyState.IsKeyDown(Keys.Escape);
        bool control = Main.keyState.PressingControl();
        bool z = Main.keyState.IsKeyDown(Keys.Z);
        bool y = Main.keyState.IsKeyDown(Keys.Y);

        return new LabInput
        {
            Mouse = new Point((int)(PlayerInput.MouseX / Main.UIScale), (int)(PlayerInput.MouseY / Main.UIScale)),
            LeftDown = left,
            LeftPressed = left && !last.LeftDown,
            LeftReleased = !left && last.LeftDown,
            RightDown = right,
            RightPressed = right && !last.RightDown,
            Scroll = Math.Sign(PlayerInput.ScrollWheelDeltaForUI),
            Shift = Main.keyState.PressingShift(),
            Control = control,
            EnterDown = enter,
            EnterPressed = enter && !last.EnterDown,
            EscapeDown = escape,
            EscapePressed = escape && !last.EscapeDown,
            ZDown = z,
            YDown = y,
            UndoPressed = control && z && !last.ZDown,
            RedoPressed = control && y && !last.YDown,
        };
    }
}

/// <summary>
///     Immediate-mode controls for the lab's panels: each call draws a control and answers what happened to it this frame. Panels
///     here are rebuilt from whatever they're showing every frame, so nothing has to be kept in step with it.
/// </summary>
/// <remarks> Lists cull whole rows rather than clipping, as a scissor would mean restarting the interface's batch for every list. </remarks>
internal sealed class LabGui
{
    public const float TextScale = 0.72f;
    public const float SmallScale = 0.62f;

    /// <summary> What one pixel of drag is worth, as a fraction of the value, when a number has no range to measure against. </summary>
    private const float DragRate = 0.006f;

    /// <summary> The smallest value a rate is taken from, so a drag can leave zero. </summary>
    private const float DragFloor = 0.2f;

    /// <summary> Pixels a press may wander and still count as a click, which starts typing instead. </summary>
    private const int ClickSlop = 2;

    private string? active;
    private int dragStartX;
    private float dragStartValue;
    private bool dragMoved;

    private string? editing;
    private string editText = string.Empty;
    private bool editedThisFrame;

    private string? tooltip;

    /// <summary> The screen in UI coordinates. <see cref="Main.screenWidth" /> changes meaning while interface layers draw, so this goes back to the real size. </summary>
    public static Point ScreenSize => new((int)(PlayerInput.RealScreenWidth / Main.UIScale), (int)(PlayerInput.RealScreenHeight / Main.UIScale));

    /// <summary> Where each control was last drawn, by id, for a harness to aim at. Null except under a harness. </summary>
    public Dictionary<string, Rectangle>? Recorded { get; set; }

    public SpriteBatch Batch { get; private set; } = null!;

    public LabInput Input { get; private set; }

    /// <summary> Off while a popup has the mouse, so nothing underneath reacts to it. </summary>
    public bool Enabled { get; set; } = true;

    /// <summary> Whether a text field has the keyboard, which holds off the game's own bindings. </summary>
    public bool IsEditing => editing is not null;

    public void Begin(SpriteBatch batch, in LabInput input)
    {
        Batch = batch;
        Input = input;
        Enabled = true;
        tooltip = null;
        editedThisFrame = false;
    }

    public void End()
    {
        if (!Input.LeftDown)
            active = null;

        // Its row scrolled away or the target changed. Left focused, it would hold the keyboard with nothing on screen to show for it.
        if (!editedThisFrame)
            editing = null;

        if (tooltip is { } text)
            DrawTooltip(text);
    }

    public void CancelEdit() => editing = null;

    public bool Hovered(Rectangle area) => Enabled && area.Contains(Input.Mouse);

    public void Record(string id, Rectangle area)
    {
        if (Recorded is { } recorded)
            recorded[id] = area;
    }

    public void Tooltip(Rectangle area, string? text)
    {
        if (text is not null && Hovered(area) && active is null)
            tooltip = text;
    }

    #region Drawing

    public void Fill(Rectangle area, Color color) => Batch.Draw(TextureAssets.MagicPixel.Value, area, color);

    public void Outline(Rectangle area, Color color)
    {
        Fill(new Rectangle(area.X, area.Y, area.Width, 1), color);
        Fill(new Rectangle(area.X, area.Bottom - 1, area.Width, 1), color);
        Fill(new Rectangle(area.X, area.Y, 1, area.Height), color);
        Fill(new Rectangle(area.Right - 1, area.Y, 1, area.Height), color);
    }

    public void Panel(Rectangle area)
    {
        Fill(area, LabTheme.Panel);
        Outline(area, LabTheme.PanelEdge);
    }

    public static float TextWidth(string text, float scale = TextScale) => FontAssets.MouseText.Value.MeasureString(text).X * scale;

    /// <summary> Text with its vertical middle on <paramref name="centerY" />. </summary>
    public void Text(string text, int x, int centerY, Color color, float scale = TextScale)
        => Utils.DrawBorderString(Batch, text, new Vector2(x, centerY - 10f * scale), color, scale);

    public void TextCentered(string text, Rectangle area, Color color, float scale = TextScale)
        => Text(text, area.Center.X - (int)(TextWidth(text, scale) / 2f), area.Center.Y, color, scale);

    public void TextRight(string text, int right, int centerY, Color color, float scale = TextScale)
        => Text(text, right - (int)TextWidth(text, scale), centerY, color, scale);

    /// <summary> Shortened to fit, keeping the start. </summary>
    public static string Fit(string text, float width, float scale = TextScale)
    {
        if (width <= 0f)
            return string.Empty;

        if (TextWidth(text, scale) <= width)
            return text;

        DynamicSpriteFont font = FontAssets.MouseText.Value;
        int length = text.Length;

        while (length > 1 && font.MeasureString(text[..length]).X * scale + 8f > width)
            length--;

        return text[..length] + "..";
    }

    private void DrawTooltip(string text)
    {
        var size = new Point((int)TextWidth(text) + 12, 22);
        var area = new Rectangle(Math.Min(Input.Mouse.X + 14, ScreenSize.X - size.X - 4), Input.Mouse.Y + 16, size.X, size.Y);

        Fill(area, new Color(10, 11, 18));
        Outline(area, LabTheme.PanelEdge);
        Text(text, area.X + 6, area.Center.Y, LabTheme.Text);
    }

    #endregion

    #region Controls

    public bool Button(string id, Rectangle area, string label, bool selected = false, string? tip = null)
    {
        Record(id, area);

        bool hovered = Hovered(area);

        Fill(area, selected ? LabTheme.Selected : hovered ? LabTheme.Hover : LabTheme.Control);
        Outline(area, LabTheme.PanelEdge);
        TextCentered(Fit(label, area.Width - 6), area, selected || hovered ? Color.White : LabTheme.Text);
        Tooltip(area, tip);

        return hovered && Input.LeftPressed && Press(id);
    }

    /// <summary> A row in a list. True the frame it's clicked. </summary>
    public bool ListRow(Rectangle area, string label, bool selected, Color? dot = null, string? right = null, string? tip = null)
    {
        Record($"row:{label}", area);

        bool hovered = Hovered(area);

        if (selected)
            Fill(area, LabTheme.Selected);
        else if (hovered)
            Fill(area, LabTheme.Hover);

        int textX = area.X + 16;

        if (dot is { } color)
            Fill(new Rectangle(area.X + 6, area.Center.Y - 2, 5, 5), color);

        float rightWidth = right is null ? 0f : TextWidth(right, SmallScale) + 6f;

        Text(Fit(label, area.Width - 20 - rightWidth), textX, area.Center.Y, selected || hovered ? Color.White : LabTheme.Text);

        if (right is not null)
            TextRight(right, area.Right - 5, area.Center.Y, LabTheme.Changed, SmallScale);

        Tooltip(area, tip);

        return hovered && Input.LeftPressed;
    }

    /// <summary>
    ///     A number, dragged sideways or clicked to type. Shift drags finer and Ctrl coarser. With a range it fills like a slider and
    ///     the range sets its speed; without one, the value's own size does, as nothing says how big it ought to be.
    /// </summary>
    /// <returns> True on any frame the value changed. </returns>
    public bool Number(string id, Rectangle area, ref float value, float? min = null, float? max = null)
    {
        Record(id, area);

        if (editing == id)
            return EditNumber(area, ref value);

        bool hovered = Hovered(area);

        Fill(area, hovered || active == id ? LabTheme.Hover : LabTheme.Inset);

        if (min is { } low && max is { } high && high > low)
        {
            float amount = MathHelper.Clamp((value - low) / (high - low), 0f, 1f);
            Fill(new Rectangle(area.X, area.Y, (int)(area.Width * amount), area.Height), LabTheme.Fill);
        }

        TextCentered(Format(value), area, LabTheme.Text, SmallScale);

        if (hovered && Input.LeftPressed && Press(id))
        {
            dragStartX = Input.Mouse.X;
            dragStartValue = value;
            dragMoved = false;
        }

        if (active != id)
            return false;

        int moved = Input.Mouse.X - dragStartX;

        if (Input.LeftReleased)
        {
            active = null;

            if (!dragMoved)
            {
                BeginEdit(id, value.ToString("0.#####", CultureInfo.InvariantCulture));
                editedThisFrame = true;
            }

            return false;
        }

        dragMoved |= Math.Abs(moved) > ClickSlop;

        if (!dragMoved)
            return false;

        float next = dragStartValue + moved * PixelValue(area, min, max);

        if (min is { } floor)
            next = MathF.Max(next, floor);

        if (max is { } ceiling)
            next = MathF.Min(next, ceiling);

        bool changed = next != value;
        value = next;

        return changed;
    }

    /// <summary> A one-line text box. True on any frame the text changed. </summary>
    public bool TextBox(string id, Rectangle area, ref string text, string placeholder)
    {
        Record(id, area);

        bool hovered = Hovered(area);

        Fill(area, editing == id || hovered ? LabTheme.Hover : LabTheme.Inset);
        Outline(area, editing == id ? LabTheme.Accent : LabTheme.PanelEdge);

        if (hovered && Input.LeftPressed && Press(id))
            BeginEdit(id, text);

        if (editing != id)
        {
            Text(text.Length > 0 ? Fit(text, area.Width - 10) : placeholder, area.X + 5, area.Center.Y, text.Length > 0 ? LabTheme.Text : LabTheme.Muted);
            return false;
        }

        editedThisFrame = true;

        string next = ReadText();
        bool changed = next != text;

        text = next;
        editText = next;
        Text(Fit(text, area.Width - 14) + "|", area.X + 5, area.Center.Y, Color.White);

        if (Input.EnterPressed || Input.EscapePressed || (Input.LeftPressed && !area.Contains(Input.Mouse)))
            editing = null;

        return changed;
    }

    /// <summary> Takes the mouse for this control, unless another already has it. </summary>
    private bool Press(string id)
    {
        if (active is not null)
            return false;

        active = id;
        return true;
    }

    private float PixelValue(Rectangle area, float? min, float? max)
    {
        float speed = Input.Shift ? 0.1f : Input.Control ? 5f : 1f;

        if (min is { } low && max is { } high && high > low)
            return (high - low) / Math.Max(area.Width, 1) * speed;

        return MathF.Max(MathF.Abs(dragStartValue), DragFloor) * DragRate * speed;
    }

    public static string Format(float value)
    {
        string text = value.ToString("0.###", CultureInfo.InvariantCulture);

        return text == "-0" ? "0" : text;
    }

    private void BeginEdit(string id, string text)
    {
        editing = id;
        editText = text;
    }

    private bool EditNumber(Rectangle area, ref float value)
    {
        editedThisFrame = true;

        Fill(area, LabTheme.Hover);
        Outline(area, LabTheme.Accent);

        editText = ReadText();
        TextCentered(editText + "|", area, Color.White, SmallScale);

        bool leftField = Input.LeftPressed && !area.Contains(Input.Mouse);

        if (!Input.EnterPressed && !leftField && !Input.EscapePressed)
            return false;

        string typed = editText;
        bool cancelled = Input.EscapePressed;

        editing = null;

        if (cancelled || !float.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            return false;

        bool changed = parsed != value;
        value = parsed;

        return changed;
    }

    private string ReadText()
    {
        if (Input.ScriptedText is { } scripted)
            return scripted;

        PlayerInput.WritingText = true;
        Main.instance.HandleIME();

        return Main.GetInputText(editText);
    }

    #endregion
}
