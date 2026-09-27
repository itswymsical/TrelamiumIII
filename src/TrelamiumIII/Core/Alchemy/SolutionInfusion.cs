using ReLogic.Content;
using ReLogic.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria.GameContent;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>Label and colour per <see cref="Temperament"/> and <see cref="ChemicalTrait"/> flag, for tooltips.</summary>
public static class ChemicalTypeInfo
{
    private static readonly (Temperament Flag, string Label, Color Color)[] _temperaments =
    {
        (Temperament.Acidic,    "Acidic",    new Color(180, 230,  90)),
        (Temperament.Alkaline,  "Alkaline",  new Color(120, 180, 255)),
        (Temperament.Volatile,  "Volatile",  new Color(255, 160,  70)),
        (Temperament.Kinetic,   "Kinetic",   new Color(190, 120, 230)),
        (Temperament.Catalytic, "Catalytic", new Color(255, 215, 110)),
        (Temperament.Inert,     "Inert",     new Color(170, 170, 170)),
    };

    private static readonly (ChemicalTrait Flag, string Label, Color Color)[] _traits =
    {
        (ChemicalTrait.Flammable, "Flammable", new Color(255, 135, 31)),
        (ChemicalTrait.Cryogen,   "Cryogen",   new Color(157, 202, 238)),
        (ChemicalTrait.Inhibitor, "Inhibitor", new Color(103, 77, 98)),
    };

    /// <summary>False, with the flag's name in white, for a flag with no entry.</summary>
    public static bool TryGet(Temperament flag, out string label, out Color color) => TryGet(_temperaments, flag, out label, out color);

    /// <inheritdoc cref="TryGet(Temperament, out string, out Color)"/>
    public static bool TryGet(ChemicalTrait flag, out string label, out Color color) => TryGet(_traits, flag, out label, out color);

    private static bool TryGet<T>((T Flag, string Label, Color Color)[] table, T flag, out string label, out Color color)
        where T : struct, Enum
    {
        foreach (var m in table)
        {
            if (!EqualityComparer<T>.Default.Equals(m.Flag, flag)) continue;
            label = m.Label;
            color = m.Color;
            return true;
        }
        label = flag.ToString();
        color = Color.White;
        return false;
    }
}

/// <summary>Optional sprite and/or shader override for one (item type, infusion) pair, wherever the instance draws.</summary>
public sealed class InfusionVisual
{
    /// <summary>Replacement sprite path. Null keeps the item's own texture.</summary>
    public string TexturePath { get; init; }

    /// <summary>Optional shader applied while the sprite draws (lazy, so load order is a non-issue).</summary>
    public Func<Asset<Effect>> Shader { get; init; }

    /// <summary>Per-draw shader parameters, GasLayer-style.</summary>
    public Action<Effect> ApplyShaderParameters { get; init; }

    private Asset<Texture2D> _texture;
    public Texture2D Texture => TexturePath == null
        ? null
        : (_texture ??= ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad)).Value;
}

/// <summary>Registry of authored <see cref="InfusionVisual"/>s, keyed by (item type, infusion).</summary>
public static class InfusionVisuals
{
    private static readonly Dictionary<(int, Temperament), InfusionVisual> _visuals = new();

    public static void Register(int itemType, Temperament infusion, InfusionVisual visual)
    {
        if (itemType > 0 && infusion != Temperament.None && visual != null)
            _visuals[(itemType, infusion)] = visual;
    }

    public static InfusionVisual Get(int itemType, Temperament infusion) =>
        infusion != Temperament.None && _visuals.TryGetValue((itemType, infusion), out var v) ? v : null;

    public static void Clear() => _visuals.Clear();
}

/// <summary>Stock infusion visuals. PARKED: nothing registers while the infusion front-end is shelved; the hooks stay wired.</summary>
public class InfusionVisualsSystem : ModSystem
{
    public override void Unload() => InfusionVisuals.Clear();
}

/// <summary>A per-instance <see cref="Temperament"/> infusion that turns a bottled solvent into a suffixed one.</summary>
public class SolutionGlobalItem : GlobalItem
{
    public override bool InstancePerEntity => true;

    // Bottled solvents only: filled bottles can carry an infusion, while an empty Bottle cannot.
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        => entity.type is ItemID.BottledWater or ItemID.BottledHoney;

    /// <summary>The infusion on this instance; None = plain water.</summary>
    public Temperament Infusion;

    /// <summary>The infusion carried by any item (None for non-water and plain instances).</summary>
    public static Temperament GetInfusion(Item item) =>
        item is { IsAir: false } && item.TryGetGlobalItem(out SolutionGlobalItem g)
            ? g.Infusion
            : Temperament.None;

    // Only identically-infused waters merge (plain and infused never mix in a stack).
    public override bool CanStack(Item destination, Item source) =>
        GetInfusion(destination) == GetInfusion(source);
    public override bool CanStackInWorld(Item destination, Item source) =>
        GetInfusion(destination) == GetInfusion(source);

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (Infusion == Temperament.None) return;
        ChemicalTypeInfo.TryGet(Infusion, out string label, out _);

        foreach (var line in tooltips)
        {
            if (line.Name != "ItemName" || line.Mod != "Terraria") continue;
            line.Text = $"{line.Text} ({label})";
            break;
        }
    }

    public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
    {
        if (Infusion == Temperament.None || line.Name != "ItemName") return true;
        ChemicalTypeInfo.TryGet(Infusion, out _, out Color color);

        var font = FontAssets.MouseText.Value;
        var pos = new Vector2(line.X, line.Y);
        string text = line.Text;
        float textWidth = font.MeasureString(text).X;

        Main.spriteBatch.DrawString(font, text, pos, Color.White);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
            null, null, null, null, Main.UIScaleMatrix);

        float time = Main.GlobalTimeWrappedHourly;

        // Pulsing ghost of the name in the infusion colour.
        float pulse = 0.22f + 0.12f * (float)Math.Sin(time * 2.3f);
        Main.spriteBatch.DrawString(font, text, pos, color * pulse);

        // Fizz: small square motes rising out of the text, wrapping over its width.
        var px = TextureAssets.MagicPixel.Value;
        var motePixel = new Rectangle(0, 0, 1, 1);
        for (int i = 0; i < 5; i++)
        {
            float t = time * 0.9f + i * 1.37f;
            float x = (t * 26f + i * 31f) % textWidth;
            float rise = (t * 14f) % 26f;
            float fade = 1f - rise / 26f;

            Main.spriteBatch.Draw(px, pos + new Vector2(x, 12f - rise), motePixel,
                color * (0.7f * fade), 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        }

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
            null, null, null, null, Main.UIScaleMatrix);

        return false;
    }

    public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position,
        Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        var vis = InfusionVisuals.Get(item.type, Infusion);
        if (vis?.Texture is not Texture2D tex) return true;

        // Land our sprite's centre where vanilla would have drawn the original frame's centre.
        Vector2 center = position + (frame.Size() * 0.5f - origin) * scale;

        bool shaded = TryApplyShader(vis, spriteBatch, Main.UIScaleMatrix);
        spriteBatch.Draw(tex, center, null, drawColor, 0f, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        if (shaded) RestoreBatch(spriteBatch, Main.UIScaleMatrix);
        return false;
    }

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor,
        Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        var vis = InfusionVisuals.Get(item.type, Infusion);
        if (vis?.Texture is not Texture2D tex) return true;

        Vector2 pos = item.Center - Main.screenPosition;
        var world = Main.GameViewMatrix.TransformationMatrix;

        bool shaded = TryApplyShader(vis, spriteBatch, world);
        spriteBatch.Draw(tex, pos, null, lightColor, rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        if (shaded) RestoreBatch(spriteBatch, world);
        return false;
    }

    // Shader passes need an Immediate batch; both hooks restart around the single draw and
    // restore the caller's Deferred state after (same dance as the tooltip VFX above).
    private static bool TryApplyShader(InfusionVisual vis, SpriteBatch sb, Matrix matrix)
    {
        var effect = vis.Shader?.Invoke()?.Value;
        if (effect == null) return false;

        vis.ApplyShaderParameters?.Invoke(effect);
        sb.End();
        sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, null, matrix);
        effect.CurrentTechnique.Passes[0].Apply();
        return true;
    }

    private static void RestoreBatch(SpriteBatch sb, Matrix matrix)
    {
        sb.End();
        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, matrix);
    }

    public override void SaveData(Item item, TagCompound tag)
    {
        if (Infusion != Temperament.None)
            tag["infusion"] = (int)Infusion;
    }

    public override void LoadData(Item item, TagCompound tag)
        => Infusion = (Temperament)tag.GetInt("infusion");

    public override void NetSend(Item item, BinaryWriter writer)
        => writer.Write((int)Infusion);

    public override void NetReceive(Item item, BinaryReader reader)
        => Infusion = (Temperament)reader.ReadInt32();
}
