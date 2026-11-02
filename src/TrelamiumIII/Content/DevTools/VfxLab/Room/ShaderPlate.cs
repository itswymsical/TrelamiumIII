using System.Collections.Generic;
using ReLogic.Content;
using Terraria.GameContent;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.UI;

namespace TrelamiumIII.Content.DevTools.VfxLab.Room;

/// <summary>
///     Somewhere to see a shader with no host: a plate on the Shaders room's wall, drawing the selected shader over a test image. It sets the
///     uniforms most of the mod's shaders share, and what it can't guess can be set on the inspector, as overrides reach it like any draw.
/// </summary>
public sealed class ShaderPlate : ModSystem
{
    /// <summary> In tiles. Small enough to sit in the gap between the panels on a normal window, seen from where you arrive. </summary>
    private const int PlateHeight = 22;

    private const int PlateWidth = 38;
    private const int PlateLift = 3;

    /// <summary> Tiles left of where you arrive for its centre, as the inspector is wider than the list and the gap between them sits left of the middle. </summary>
    private const int PlateOffset = 5;

    private static readonly Dictionary<string, float> CommonNumbers = new(StringComparer.Ordinal)
    {
        ["uOpacity"] = 1f,
        ["uIntensity"] = 1f,
        ["uStrength"] = 1f,
        ["Opacity"] = 1f,
        ["Intensity"] = 1f,
        ["uAlpha"] = 1f,
        ["uScale"] = 1f,
        ["uBrightness"] = 1f,
    };

    private static Texture2D? testImage;

    /// <summary> The plate, in world pixels. </summary>
    private static Rectangle PlateArea
    {
        get
        {
            Point arrival = LabRooms.ArrivalOf(LabRoom.Shaders);
            int x = arrival.X - PlateOffset - PlateWidth / 2;
            int y = arrival.Y - PlateLift - PlateHeight;

            return new Rectangle(x * 16, y * 16, PlateWidth * 16, PlateHeight * 16);
        }
    }

    public override void Unload()
    {
        Main.QueueMainThreadAction(static () => testImage?.Dispose());
        testImage = null;
    }

    public override void PostDrawTiles()
    {
        if (!VfxLabUI.Open || !LabRooms.InLab || LabRooms.Current != LabRoom.Shaders)
            return;

        Rectangle plate = PlateArea;
        var onScreen = new Rectangle(plate.X - (int)Main.screenPosition.X, plate.Y - (int)Main.screenPosition.Y, plate.Width, plate.Height);
        Matrix view = Main.GameViewMatrix.TransformationMatrix;
        SpriteBatch batch = Main.spriteBatch;

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, null, view);
        batch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(onScreen.X - 2, onScreen.Y - 2, onScreen.Width + 4, onScreen.Height + 4), Color.Black * 0.8f);

        string title = ShaderBench.Selected is ShaderTarget target ? target.Name : "Pick a shader from the list";
        Utils.DrawBorderString(batch, title, new Vector2(onScreen.X, onScreen.Bottom + 6), LabTheme.Muted, 0.8f);

        if (ShaderBench.Selected is ShaderTarget { NeedsItsHost: true })
            Utils.DrawBorderString(batch, "This one brings its own vertex shader, so it can only be seen in its real host.", new Vector2(onScreen.X + 8, onScreen.Y + 8), LabTheme.Text, 0.8f);

        batch.End();

        if (ShaderBench.Selected is ShaderTarget { NeedsItsHost: false, Effect: { } effect })
            Draw(batch, effect, onScreen, view);
    }

    /// <summary> Draws through <paramref name="effect" />. With no batch open, and either to the screen or to a target. </summary>
    internal static void Draw(SpriteBatch batch, Effect effect, Rectangle destination, Matrix transform)
    {
        testImage ??= BuildTestImage(Main.instance.GraphicsDevice);

        Prepare(effect);

        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, null, transform);
        effect.CurrentTechnique.Passes[0].Apply();
        batch.Draw(testImage, destination, Color.White);
        batch.End();
    }

    /// <summary> Gives a shader what most of them expect to find. Whatever's on the inspector lands after this, at the moment the pass applies. </summary>
    private static void Prepare(Effect effect)
    {
        float seconds = LabClock.IsManual ? LabClock.Seconds : (float)Main.timeForVisualEffects / 60f;

        effect.Parameters["uTime"]?.SetValue(seconds);
        effect.Parameters["Time"]?.SetValue(seconds);

        // Shaders disagree on whether this is a ratio or a pair, and zero squashes what's drawn to a streak.
        if (effect.Parameters["uAspect"] is { ColumnCount: 2 } aspectPair)
            aspectPair.SetValue(new Vector2(PlateWidth / (float)PlateHeight, 1f));
        else
            effect.Parameters["uAspect"]?.SetValue(PlateWidth / (float)PlateHeight);

        effect.Parameters["Aspect"]?.SetValue(PlateWidth / (float)PlateHeight);

        foreach ((string name, float value) in CommonNumbers)
            effect.Parameters[name]?.SetValue(value);

        foreach (EffectParameter parameter in effect.Parameters)
        {
            if (parameter.ParameterType is EffectParameterType.Texture or EffectParameterType.Texture2D)
            {
                if (parameter.GetValueTexture2D() is not { IsDisposed: false })
                    parameter.SetValue(Assets.Textures.Noise.Perlin.Asset.Value);
            }
            else if (IsColourName(parameter.Name) && parameter.ParameterType == EffectParameterType.Single && parameter.ColumnCount == 3 && parameter.GetValueVector3() == Vector3.Zero)
            {
                // Black is what a colour nothing has set reads as, and a shader multiplying by it just draws a black box.
                parameter.SetValue(new Vector3(0.7f));
            }
        }
    }

    private static bool IsColourName(string name)
        => name.Contains("Color", StringComparison.OrdinalIgnoreCase) || name.Contains("Colour", StringComparison.OrdinalIgnoreCase) || name.Contains("Tint", StringComparison.OrdinalIgnoreCase);

    /// <summary> A soft gradient with a checker over it: colour and structure for a shader to work on, and dim enough not to glare. </summary>
    private static Texture2D BuildTestImage(GraphicsDevice device)
    {
        const int Width = 320;
        const int Height = 180;
        const int Cell = 20;

        var pixels = new Color[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)Width;
                float v = y / (float)Height;
                float checker = (x / Cell + y / Cell) % 2 == 0 ? 1f : 0.82f;

                pixels[y * Width + x] = new Color(new Vector3(0.25f + 0.45f * u, 0.3f + 0.3f * v, 0.55f - 0.3f * u) * checker);
            }
        }

        var image = new Texture2D(device, Width, Height);
        image.SetData(pixels);

        return image;
    }
}
