using System.Collections.Generic;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Core.ExperientialSystem;

namespace TrelamiumIII.Core.UI.ExperientialMeter;

/// <summary>
/// The Experiential Meter. Shows the Threshold reached and how full the meter is - which is both
/// progress toward the next Threshold and the Exp currently available to spend on skills.
/// </summary>
internal class ExperientialMeterUI : UIState
{
    private UIText expText;
    private UIText levelText;
    private UIElement area;
    private UIImage barFrame;

    public override void OnInitialize()
    {
        area = new UIElement();
        area.Top.Set(16, 0f);
        area.Width.Set(154, 0f);
        area.Height.Set(74, 0f);
        area.Left.Set(-area.Width.Pixels * 3.15f, 1f);

        barFrame = new UIImage(Assets.Textures.UI.ExperientialMeter.ExperienceBar.Asset);
        barFrame.Left.Set(16, 0f);
        barFrame.Top.Set(0, 0f);
        barFrame.Width.Set(154, 0f);
        barFrame.Height.Set(74, 0f);

        // XP progress text
        expText = new UIText("0/0", 0.91f);
        expText.Width.Set(154, 0f);
        expText.Height.Set(74, 0f);
        expText.Top.Set(45, 0f);
        expText.Left.Set(-8f, 0f);

        // Level display
        levelText = new UIText("1", 0.75f);
        levelText.Width.Set(154, 0f);
        levelText.Height.Set(74f, 0f);
        levelText.Top.Set(4f, 0f);
        levelText.Left.Set(78f, 0f);

        area.Append(barFrame);
        area.Append(levelText);
        area.Append(expText);
        Append(area);
    }

    public override void Draw(SpriteBatch spriteBatch) => base.Draw(spriteBatch);

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        var modPlayer = Main.LocalPlayer.GetModPlayer<ExperientialPlayer>();

        // percentage based fill
        var xpPercentage = modPlayer.MeterFill;
        xpPercentage = MathHelper.Clamp(xpPercentage, 0f, 1f);

        var hitbox = barFrame.GetInnerDimensions().ToRectangle();

        levelText.Width.Set(154, 0f);
        levelText.Height.Set(74f, 0f);
        levelText.Top.Set(14f, 0f);
        levelText.Left.Set(72f, 0f);

        if (area.IsMouseHovering)
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.instance.MouseText($"Experiential Threshold {modPlayer.Threshold}{(modPlayer.AtMaxThreshold ? " (max)" : string.Empty)}" +
                $"\n({modPlayer.Exp:N0} / {modPlayer.Capacity:N0} Exp)");
        }

        // Draw empty bar background
        spriteBatch.Draw(
            Assets.Textures.UI.ExperientialMeter.ExperienceBar_Empty.Asset.Value, hitbox, Color.White);

        var fillableWidth = hitbox.Width / 2 + 20;
        var fillableHeight = 12;
        var fillWidth = (int)(fillableWidth * xpPercentage);

        var xOffset = 6;
        var yOffset = hitbox.Height - fillableHeight - 18;

        // Draw fill bar
        for (var i = 0; i < fillWidth; i += 12)
        {
            var segmentWidth = Math.Min(12, fillWidth - i);
            var sourceRect = new Rectangle(0, 0, segmentWidth, 12);
            var destRect = new Rectangle(hitbox.X + xOffset + i, hitbox.Y + yOffset, segmentWidth, 12);
            spriteBatch.Draw(
                Assets.Textures.UI.ExperientialMeter.ExperienceBar_Fill.Asset.Value,
                destRect,
                sourceRect,
                 Color.White
            );
        }
    }

    public override void Update(GameTime gameTime)
    {
        var modPlayer = Main.LocalPlayer.GetModPlayer<ExperientialPlayer>();

        // At max Threshold the meter stops being a gate, but it is still a real wallet with a real
        // capacity, so the numbers stay meaningful rather than collapsing to "Max Level".
        expText.SetText($"{modPlayer.Exp:N0} / {modPlayer.Capacity:N0}", 0.6f, false);

        levelText.SetText($"{modPlayer.Threshold}", 0.35f, true);

        base.Update(gameTime);
    }
}

[Autoload(Side = ModSide.Client)]
internal class ExperientialMeterSystem : ModSystem
{
    private UserInterface meterInterface;
    private ExperientialMeterUI meterState;

    public override void Load()
    {
        meterState = new();
        meterInterface = new();
        meterInterface.SetState(meterState);
    }

    public override void UpdateUI(GameTime gameTime) => meterInterface?.Update(gameTime);

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        var resourceBarIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));
        if (resourceBarIndex != -1)
        {
            layers.Insert(resourceBarIndex, new LegacyGameInterfaceLayer(
                "TrelamiumIII: Experiential Meter",
                delegate
                {
                    meterInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI)
            );
        }
    }
}