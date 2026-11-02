using System.Collections.Generic;
using ReLogic.Content;
using TrelamiumIII.Core.Books.Authoring;

namespace TrelamiumIII.Content.DevTools.VfxLab.UI;

internal enum FieldKind
{
    /// <summary> A section title that folds what follows it, with optional small buttons at its right. </summary>
    Header,

    /// <summary> One to four numbers side by side. </summary>
    Numbers,

    /// <summary> Three numbers, with a swatch that opens a picker. </summary>
    Color,

    Texture,

    /// <summary> One of a few named options, all on show. </summary>
    Choice,

    /// <summary> A row of buttons, the whole width of the panel. </summary>
    Buttons,

    /// <summary> A line of text that says how something's going. </summary>
    Note,
}

internal sealed record InspectorButton(string Id, string Label, Action Act, string? Tip = null, Func<bool>? Selected = null);

/// <summary>
///     One row of the inspector, and all it knows about what's being edited. Anything that can say what its values are and take new ones
///     can be inspected: a running shader's uniforms, a recipe's stages, a field's physics.
/// </summary>
internal sealed class InspectorField
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required FieldKind Kind { get; init; }

    public string? Tip { get; init; }

    public Func<float[]>? Get { get; init; }

    public Action<float[]>? Set { get; init; }

    public float? Min { get; init; }

    public float? Max { get; init; }

    /// <summary> The texture's name, for a <see cref="FieldKind.Texture" />. </summary>
    public Func<string?>? GetTexture { get; init; }

    public Action<string>? SetTexture { get; init; }

    /// <summary> Whether this differs from what it was before the lab touched it. </summary>
    public Func<bool>? IsChanged { get; init; }

    /// <summary> Puts it back. Right-click. </summary>
    public Action? Reset { get; init; }

    /// <summary> For a header: starts folded. </summary>
    public bool Folded { get; set; }

    /// <summary> For a header: told when it folds, so a rebuilt list can keep it that way. </summary>
    public Action<bool>? FoldChanged { get; init; }

    /// <summary> For a header: it sits inside the section above it, so folding that hides this too, and folding this only hides its own rows. </summary>
    public bool Nested { get; init; }

    /// <summary> Belongs to the section rather than the nested header above it, so folding that one leaves this showing. </summary>
    public bool SectionRow { get; init; }

    /// <summary> For a <see cref="FieldKind.Buttons" /> row, or the small ones on a header. </summary>
    public IReadOnlyList<InspectorButton>? Buttons { get; init; }

    public IReadOnlyList<string>? Choices { get; init; }

    public Func<int>? GetChoice { get; init; }

    public Action<int>? SetChoice { get; init; }

    /// <summary> For a <see cref="FieldKind.Note" />. </summary>
    public Func<string>? GetNote { get; init; }

    public Func<bool>? NoteIsBad { get; init; }
}

/// <summary> A scrolling column of <see cref="InspectorField" />s, and the popups they open. </summary>
internal sealed class Inspector
{
    public const int RowHeight = 24;

    private const int LabelWidth = 118;
    private const int Gap = 3;
    private const int SwatchWidth = 24;
    private const int HeaderButtonWidth = 22;
    private const int ColorPopupWidth = 210;
    private const int ColorPopupHeight = 150;
    private const int ThumbSize = 44;
    private const int ThumbColumns = 5;
    private const int ListPopupWidth = 210;
    private const int ListRowHeight = 20;
    private const int ListPopupRows = 18;

    private readonly ColorWheelWidget wheel = new();
    private readonly List<InspectorField> visible = [];

    private InspectorField? colorField;
    private InspectorField? textureField;
    private string? listTitle;
    private IReadOnlyList<(string Label, Action Pick)>? listItems;
    private int listScroll;
    private Rectangle popup;
    private bool justOpened;
    private int left;
    private int scroll;

    /// <summary> Every noise texture a picker can offer, by the name a recipe uses. </summary>
    public IReadOnlyList<(string Name, Asset<Texture2D> Asset)> Textures { get; set; } = [];

    /// <summary> Where the last button in the column was, for a list to open beside. </summary>
    public Rectangle LastPressed { get; private set; }

    public bool PopupOpen => colorField is not null || textureField is not null || listItems is not null;

    public Rectangle PopupArea => popup;

    public void ScrollToTop() => scroll = 0;

    public void ClosePopups()
    {
        colorField = null;
        textureField = null;
        listItems = null;
    }

    /// <summary> A list to pick one thing from. </summary>
    public void OpenList(string title, IReadOnlyList<(string Label, Action Pick)> items, Rectangle anchor)
    {
        ClosePopups();
        listTitle = title;
        listItems = items;
        listScroll = 0;
        justOpened = true;
        popup = PopupNear(anchor, ListPopupWidth, Math.Min(items.Count, ListPopupRows) * ListRowHeight + 30);
    }

    public void Draw(LabGui gui, Rectangle area, IReadOnlyList<InspectorField> fields)
    {
        left = area.X;
        visible.Clear();

        bool sectionFolded = false;
        bool nestedFolded = false;

        foreach (InspectorField field in fields)
        {
            if (field.Kind == FieldKind.Header && !field.Nested)
            {
                sectionFolded = field.Folded;
                nestedFolded = false;
                visible.Add(field);
            }
            else if (field.Kind == FieldKind.Header)
            {
                nestedFolded = field.Folded;

                if (!sectionFolded)
                    visible.Add(field);
            }
            else if (!sectionFolded && (!nestedFolded || field.SectionRow))
            {
                visible.Add(field);
            }
        }

        int rows = Math.Max(area.Height / RowHeight, 1);
        int maxScroll = Math.Max(visible.Count - rows, 0);

        if (gui.Hovered(area))
            scroll -= gui.Input.Scroll;

        scroll = Math.Clamp(scroll, 0, maxScroll);

        int width = maxScroll > 0 ? area.Width - 6 : area.Width;

        for (int i = 0; i < rows && i + scroll < visible.Count; i++)
            DrawRow(gui, new Rectangle(area.X, area.Y + i * RowHeight, width, RowHeight), visible[i + scroll]);

        if (maxScroll > 0)
        {
            int barHeight = Math.Max(area.Height * rows / visible.Count, 16);
            int barY = area.Y + (area.Height - barHeight) * scroll / maxScroll;

            gui.Fill(new Rectangle(area.Right - 3, area.Y, 3, area.Height), LabTheme.Inset);
            gui.Fill(new Rectangle(area.Right - 3, barY, 3, barHeight), LabTheme.PanelEdge);
        }
    }

    /// <summary> Drawn after everything else, so it's on top. </summary>
    public void DrawPopups(LabGui gui)
    {
        // The click that opened it is still this frame's, and it landed outside the popup. It mustn't close it again.
        if (justOpened)
        {
            justOpened = false;
            gui.Panel(popup);
            return;
        }

        if (colorField is { } color)
            DrawColorPopup(gui, color);
        else if (textureField is { } texture)
            DrawTexturePopup(gui, texture);
        else if (listItems is { } items)
            DrawListPopup(gui, items);
    }

    private void DrawRow(LabGui gui, Rectangle row, InspectorField field)
    {
        switch (field.Kind)
        {
            case FieldKind.Header:
                DrawHeader(gui, row, field);
                return;
            case FieldKind.Buttons:
                DrawButtons(gui, row, field);
                return;
            case FieldKind.Note:
                gui.Text(LabGui.Fit(field.GetNote!(), row.Width - 10), row.X + 5, row.Center.Y, field.NoteIsBad?.Invoke() == true ? LabTheme.Failure : LabTheme.Muted, LabGui.SmallScale);
                gui.Tooltip(row, field.GetNote!());
                return;
        }

        bool changed = field.IsChanged?.Invoke() == true;

        if (changed)
            gui.Fill(new Rectangle(row.X, row.Y + 5, 2, row.Height - 10), LabTheme.Changed);

        var labelArea = new Rectangle(row.X + 5, row.Y, LabelWidth - 5, row.Height);
        gui.Text(LabGui.Fit(field.Label, labelArea.Width - 2), labelArea.X, labelArea.Center.Y, changed ? LabTheme.Text : LabTheme.Muted);
        gui.Tooltip(labelArea, field.Tip ?? field.Label);

        var value = new Rectangle(row.X + LabelWidth, row.Y + 2, row.Width - LabelWidth - 3, row.Height - 4);

        if (gui.Hovered(row) && gui.Input.RightPressed)
            field.Reset?.Invoke();

        switch (field.Kind)
        {
            case FieldKind.Numbers:
                DrawNumbers(gui, value, field);
                break;
            case FieldKind.Color:
                DrawColor(gui, value, field);
                break;
            case FieldKind.Texture:
                DrawTextureRow(gui, value, field);
                break;
            case FieldKind.Choice:
                DrawChoice(gui, value, field);
                break;
        }
    }

    private void DrawHeader(LabGui gui, Rectangle row, InspectorField field)
    {
        int indent = field.Nested ? 6 : 0;
        var bar = new Rectangle(row.X + indent, row.Y + 2, row.Width - indent, row.Height - 4);
        int buttons = field.Buttons?.Count ?? 0;
        var titleArea = new Rectangle(bar.X, bar.Y, bar.Width - buttons * (HeaderButtonWidth + 1), bar.Height);

        gui.Record(field.Id, titleArea);
        gui.Fill(bar, gui.Hovered(titleArea) ? LabTheme.Hover : LabTheme.Control);
        gui.Text(LabGui.Fit($"{(field.Folded ? ">" : "v")}  {field.Label}", titleArea.Width - 8), bar.X + 5, bar.Center.Y, LabTheme.Muted);
        gui.Tooltip(titleArea, field.Tip);

        if (gui.Hovered(titleArea) && gui.Input.LeftPressed)
        {
            field.Folded = !field.Folded;
            field.FoldChanged?.Invoke(field.Folded);
        }

        for (int i = 0; i < buttons; i++)
        {
            InspectorButton button = field.Buttons![i];
            var area = new Rectangle(bar.Right - (buttons - i) * (HeaderButtonWidth + 1) + 1, bar.Y, HeaderButtonWidth, bar.Height);

            if (gui.Button(button.Id, area, button.Label, button.Selected?.Invoke() == true, button.Tip))
            {
                LastPressed = area;
                button.Act();
            }
        }
    }

    private void DrawButtons(LabGui gui, Rectangle row, InspectorField field)
    {
        IReadOnlyList<InspectorButton> buttons = field.Buttons!;
        int width = (row.Width - Gap * (buttons.Count - 1)) / Math.Max(buttons.Count, 1);

        for (int i = 0; i < buttons.Count; i++)
        {
            InspectorButton button = buttons[i];
            var area = new Rectangle(row.X + i * (width + Gap), row.Y + 2, width, row.Height - 4);

            if (gui.Button(button.Id, area, button.Label, button.Selected?.Invoke() == true, button.Tip))
            {
                LastPressed = area;
                button.Act();
            }
        }
    }

    private static void DrawChoice(LabGui gui, Rectangle area, InspectorField field)
    {
        IReadOnlyList<string> choices = field.Choices!;
        int current = field.GetChoice!();
        int width = (area.Width - Gap * (choices.Count - 1)) / Math.Max(choices.Count, 1);

        for (int i = 0; i < choices.Count; i++)
        {
            if (gui.Button($"{field.Id}#{i}", new Rectangle(area.X + i * (width + Gap), area.Y, width, area.Height), choices[i], i == current))
                field.SetChoice!(i);
        }
    }

    private static void DrawNumbers(LabGui gui, Rectangle area, InspectorField field)
    {
        float[] values = field.Get!();
        int width = (area.Width - Gap * (values.Length - 1)) / Math.Max(values.Length, 1);
        bool changed = false;

        for (int i = 0; i < values.Length; i++)
        {
            var cell = new Rectangle(area.X + i * (width + Gap), area.Y, width, area.Height);

            changed |= gui.Number($"{field.Id}#{i}", cell, ref values[i], field.Min, field.Max);
        }

        if (changed)
            field.Set!(values);
    }

    private void DrawColor(LabGui gui, Rectangle area, InspectorField field)
    {
        float[] values = field.Get!();
        var swatch = new Rectangle(area.X, area.Y, SwatchWidth, area.Height);

        gui.Record($"{field.Id}#swatch", swatch);
        gui.Fill(swatch, new Color(values[0], values[1], values[2]));
        gui.Outline(swatch, colorField == field ? LabTheme.Accent : gui.Hovered(swatch) ? Color.White : LabTheme.PanelEdge);
        gui.Tooltip(swatch, "Pick a colour");

        // Popups don't take the rest of the panel away, so going from one colour to the next is one click.
        if (gui.Hovered(swatch) && gui.Input.LeftPressed)
        {
            ClosePopups();
            colorField = field;
            justOpened = true;
            wheel.SetColor(new Color(values[0], values[1], values[2]));
            popup = PopupNear(swatch, ColorPopupWidth, ColorPopupHeight);
        }

        var numbers = new Rectangle(swatch.Right + Gap, area.Y, area.Width - SwatchWidth - Gap, area.Height);
        int width = (numbers.Width - Gap * 2) / 3;
        bool changed = false;

        for (int i = 0; i < 3; i++)
            changed |= gui.Number($"{field.Id}#{i}", new Rectangle(numbers.X + i * (width + Gap), numbers.Y, width, numbers.Height), ref values[i], 0f, null);

        if (changed)
        {
            field.Set!(values);

            if (colorField == field)
                wheel.SetColor(new Color(values[0], values[1], values[2]));
        }
    }

    private void DrawTextureRow(LabGui gui, Rectangle area, InspectorField field)
    {
        string? name = field.GetTexture!();

        gui.Record(field.Id, area);

        bool hovered = gui.Hovered(area);

        gui.Fill(area, hovered ? LabTheme.Hover : LabTheme.Inset);
        gui.Outline(area, textureField == field ? LabTheme.Accent : LabTheme.PanelEdge);

        var thumb = new Rectangle(area.X + 1, area.Y + 1, area.Height - 2, area.Height - 2);

        foreach ((string candidate, Asset<Texture2D> asset) in Textures)
        {
            if (candidate == name && asset.IsLoaded)
                gui.Batch.Draw(asset.Value, thumb, Color.White);
        }

        gui.Text(LabGui.Fit(name ?? "(set by the game)", area.Width - thumb.Width - 8), thumb.Right + 4, area.Center.Y, name is null ? LabTheme.Muted : LabTheme.Text, LabGui.SmallScale);

        if (hovered && gui.Input.LeftPressed)
        {
            ClosePopups();
            textureField = field;
            justOpened = true;

            int rows = (Textures.Count + ThumbColumns - 1) / ThumbColumns;
            popup = PopupNear(area, ThumbColumns * (ThumbSize + Gap) + Gap + 8, rows * (ThumbSize + Gap) + Gap + 26);
        }
    }

    /// <summary> Just left of the inspector, as it sits at the screen's right edge, level with what opened it and kept on screen. </summary>
    private Rectangle PopupNear(Rectangle anchor, int width, int height)
        => new(left - width - 12, Math.Clamp(anchor.Y - 10, 4, Math.Max(LabGui.ScreenSize.Y - height - 4, 4)), width, height);

    private void DrawColorPopup(LabGui gui, InspectorField field)
    {
        gui.Panel(popup);

        Rectangle picker = popup;
        picker.Inflate(-8, -8);

        LabInput input = gui.Input;

        if (input.LeftPressed)
        {
            if (!wheel.Press(input.Mouse, picker) && !popup.Contains(input.Mouse))
            {
                ClosePopups();
                return;
            }
        }
        else if (input.LeftDown && wheel.Dragging)
        {
            wheel.Drag(input.Mouse, picker);
        }
        else if (!input.LeftDown)
        {
            wheel.Release();
        }

        if (wheel.Dragging)
        {
            Vector3 picked = wheel.Value.ToVector3();
            field.Set!([picked.X, picked.Y, picked.Z]);
        }

        wheel.Draw(gui.Batch, picker, 1f);
    }

    private void DrawTexturePopup(LabGui gui, InspectorField field)
    {
        gui.Panel(popup);
        gui.Text("Noise texture", popup.X + 8, popup.Y + 12, LabTheme.Muted);

        string? current = field.GetTexture!();
        string? hovered = null;

        for (int i = 0; i < Textures.Count; i++)
        {
            (string name, Asset<Texture2D> asset) = Textures[i];

            var cell = new Rectangle(
                popup.X + 4 + Gap + i % ThumbColumns * (ThumbSize + Gap),
                popup.Y + 24 + i / ThumbColumns * (ThumbSize + Gap),
                ThumbSize,
                ThumbSize);

            gui.Fill(cell, LabTheme.Inset);

            if (asset.IsLoaded)
                gui.Batch.Draw(asset.Value, cell, Color.White);

            bool over = cell.Contains(gui.Input.Mouse);

            if (name == current || over)
                gui.Outline(cell, name == current ? LabTheme.Accent : Color.White);

            if (!over)
                continue;

            hovered = name;

            if (gui.Input.LeftPressed)
            {
                field.SetTexture!(name);
                ClosePopups();
                return;
            }
        }

        if (hovered is not null)
            gui.TextRight(hovered, popup.Right - 8, popup.Y + 12, LabTheme.Text);

        if (gui.Input.LeftPressed && !popup.Contains(gui.Input.Mouse))
            ClosePopups();
    }

    private void DrawListPopup(LabGui gui, IReadOnlyList<(string Label, Action Pick)> items)
    {
        gui.Panel(popup);
        gui.Text(listTitle ?? string.Empty, popup.X + 8, popup.Y + 12, LabTheme.Muted);

        if (popup.Contains(gui.Input.Mouse))
            listScroll -= gui.Input.Scroll;

        listScroll = Math.Clamp(listScroll, 0, Math.Max(items.Count - ListPopupRows, 0));

        for (int i = 0; i < ListPopupRows && i + listScroll < items.Count; i++)
        {
            (string label, Action pick) = items[i + listScroll];
            var row = new Rectangle(popup.X + 4, popup.Y + 24 + i * ListRowHeight, popup.Width - 8, ListRowHeight);

            gui.Record($"list:{label}", row);

            bool over = row.Contains(gui.Input.Mouse);

            if (over)
                gui.Fill(row, LabTheme.Hover);

            gui.Text(LabGui.Fit(label, row.Width - 10), row.X + 5, row.Center.Y, over ? Color.White : LabTheme.Text);

            if (over && gui.Input.LeftPressed)
            {
                ClosePopups();
                pick();
                return;
            }
        }

        if (gui.Input.LeftPressed && !popup.Contains(gui.Input.Mouse))
            ClosePopups();
    }
}
