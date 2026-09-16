using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools;

/// <summary>One layer in the picker: its name, what it currently holds, and whether it is selected.</summary>
internal sealed class FieldLayerRow(int index) : UIElement
{
    public const float HEIGHT = 20f;

    /// <summary>Gas layers are tinted apart from visual ones, since that is the split being tested.</summary>
    private static readonly Color GasTint = new(120, 205, 140);

    private static readonly Color VisualTint = new(150, 175, 230);

    private FieldLayer? Layer => index < FieldSystem.Layers.Count ? FieldSystem.Layers[index] : null;

    public FieldLayerRow Build()
    {
        Width.Set(0f, 1f);
        Height.Set(HEIGHT, 0f);
        OnLeftClick += (_, _) => FieldWandSelection.Select(index);

        return this;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (Layer is not { } layer)
            return;

        CalculatedStyle bounds = GetDimensions();
        bool selected = FieldWandSelection.Index == index;
        bool hovered = ContainsPoint(Main.MouseScreen);

        if (selected || hovered)
        {
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds.ToRectangle(),
                Color.White * (selected ? 0.22f : 0.10f));
        }

        Color tint = layer is GasField ? GasTint : VisualTint;
        string count = layer is GasField gas && gas.EmitterCount > 0
            ? $"{layer.NodeCount}+{gas.EmitterCount}"
            : layer.NodeCount.ToString();

        Utils.DrawBorderString(spriteBatch, FieldWandSelection.NameOf(layer),
            new Vector2(bounds.X + 6f, bounds.Y + 3f), selected ? Color.White : tint, 0.75f);

        Utils.DrawBorderString(spriteBatch, count,
            new Vector2(bounds.X + bounds.Width - 8f, bounds.Y + 3f),
            layer.NodeCount > 0 ? Color.White : Color.Gray, 0.75f, 1f);
    }
}

/// <summary>
///     The wand's picker: every registered layer, in composite order, with live node counts.
/// </summary>
/// <remarks>
///     Rows are built on the first frame a layer exists rather than in <c>OnInitialize</c>, because
///     nothing orders this <see cref="ModSystem" />'s load against <see cref="FieldSystem" />'s.
/// </remarks>
internal sealed class FieldWandUIState : UIState
{
    private const float PANEL_WIDTH = 232f;

    private const float HEADER_HEIGHT = 40f;

    private UIPanel? panel;
    private UIElement? rows;
    private bool built;

    public override void OnInitialize()
    {
        panel = new UIPanel
        {
            BackgroundColor = new Color(28, 32, 48) * 0.92f,
            BorderColor = new Color(70, 80, 110),
        };

        panel.Left.Set(16f, 0f);
        panel.Top.Set(260f, 0f);
        panel.Width.Set(PANEL_WIDTH, 0f);
        panel.SetPadding(8f);

        rows = new UIElement();
        rows.Width.Set(0f, 1f);
        rows.Top.Set(HEADER_HEIGHT, 0f);

        panel.Append(rows);
        Append(panel);
    }

    private void BuildRows()
    {
        if (built || rows is null || FieldSystem.Layers.Count == 0)
            return;

        for (int i = 0; i < FieldSystem.Layers.Count; i++)
        {
            var row = new FieldLayerRow(i).Build();

            row.Top.Set(i * FieldLayerRow.HEIGHT, 0f);
            rows.Append(row);
        }

        float height = FieldSystem.Layers.Count * FieldLayerRow.HEIGHT;

        rows.Height.Set(height, 0f);
        panel?.Height.Set(height + HEADER_HEIGHT + 16f, 0f);

        built = true;
        Recalculate();
    }

    public override void Update(GameTime gameTime)
    {
        BuildRows();

        base.Update(gameTime);

        // Stops a click on the picker from also swinging the wand behind it.
        if (panel?.ContainsPoint(Main.MouseScreen) == true)
            Main.LocalPlayer.mouseInterface = true;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (panel is null)
            return;

        CalculatedStyle bounds = panel.GetDimensions();

        Utils.DrawBorderString(spriteBatch, "Field Wand",
            new Vector2(bounds.X + 8f, bounds.Y + 6f), Color.White, 0.85f);

        bool supported = FieldWandSelection.Supports(FieldWandSelection.Mode);

        Utils.DrawBorderString(spriteBatch, $"right click: {FieldWandSelection.Mode}",
            new Vector2(bounds.X + 8f, bounds.Y + 24f), supported ? new Color(230, 200, 120) : Color.Gray, 0.72f);
    }
}
