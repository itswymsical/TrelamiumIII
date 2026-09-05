using System;
using ReLogic.Content;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.UI.AlchemyStation;

namespace TrelamiumIII.Content.Alchemy.AlchemyStations;

/// <summary>
/// Draws the Advanced Alchemy Station as one composite, anchored to the top-left tile of the base.
/// All offsets are in "composite space": pixel coordinates inside the 220x120 "FullImage.png" reference.
/// Nothing renders by default except the base: chamber, pipes, components, heat plate, and contents
/// appear only as their entity flags / slot items light up.
/// </summary>
public static class AdvancedAlchemyStationRenderer
{
    // structural layer top-lefts in composite space
    private static readonly Vector2 BaseOffset = new(-2, 90);
    private static readonly Vector2 ChamberBackOffset = new(148, 16);
    private static readonly Vector2 PipesOffset = new(16, 40);
    private static readonly Vector2 ChamberOffset = new(124, -2);
    private static readonly Vector2[] ComponentOffsets = [new(10, 30), new(36, 30), new(62, 30)];
    private static readonly Vector2 HeatPlateOffset = new(130, 88);

    // Brew progress bar sits just under the heat plate (same width/column), in composite space.
    private static readonly Vector2 HeatPlateBarOffset = new(130, 101);

    // Bottom-centre of the 96x96 chamber (ChamberOffset + ~half width, near the base).
    private static readonly Vector2 ChamberPivot = new(172, 92);

    private static readonly Vector2 ChamberLiquidOffset = new(132, 34);

    // Center of the held item inside a component cylinder (relative to component top-left).
    private static readonly Vector2 ComponentItemOffset = new(15, 38);
    private const float ComponentItemScale = 0.7f;

    // Component UI slot centre (relative to the cylinder top-left): hung over the TOP of the cylinder, separate
    // from where the tile draws the held item (ComponentItemOffset, lower down). Slots are also spread an extra
    // few px apart so they don't touch.
    private static readonly Vector2 ComponentSlotOffset = new(15, 4);
    private const float ComponentSlotSpacingExtra = 2f;

    // Output item on the right end of the base, beneath the chamber spout — the produced/previewed result.
    private static readonly Vector2 BaseEdgeOutputOffset = new(198, 68);

    /// <summary>Composite top-left relative to the base origin cell's top-left, in pixels.</summary>
    private static readonly Vector2 CompositeToCell = new(2, -88);

    /// <summary>
    /// World-space offset (from the base origin tile's top-left, in pixels) the UI panel anchors to: the
    /// output column horizontally, but the top of the chamber vertically, so the panel floats clear above the
    /// machine instead of overlapping the chamber.
    /// </summary>
    public static Vector2 UiAnchorWorldOffset => new(CompositeToCell.X + BaseEdgeOutputOffset.X, CompositeToCell.Y + ChamberOffset.Y);

    public static Vector2 ComponentSlotWorldOffset(int k)
        => CompositeToCell + ComponentOffsets[k] + ComponentSlotOffset + new Vector2((k - 1) * 18, -24f);
    public static Vector2 VialSlotWorldOffset => CompositeToCell + ChamberLiquidOffset + new Vector2(32f, 0f); // chamber fluid centre
    public static Vector2 OutputSlotWorldOffset => CompositeToCell + BaseEdgeOutputOffset + new Vector2(11f, 11f);

    // heat plate sheet: 64x34 = 3 frames of 64x10 with 2px vertical padding
    private const int HeatPlateFrameWidth = 64;
    private const int HeatPlateFrameHeight = 10;
    private const int HeatPlateFrameStrideY = 12;
    private const int HeatPlateFrames = 3;
    private const int HeatPlateTicksPerFrame = 8;

    // lazily-cached textures
    private static Asset<Texture2D>? _base, _chamberBack, _pipes, _chamber, _chamberOverlay,
        _component, _heatPlate;

    private static Texture2D GetTex(ref Asset<Texture2D>? slot, string key)
        => (slot ??= ModContent.Request<Texture2D>(key)).Value;

    public static void Draw(SpriteBatch sb, int originI, int originJ, AdvancedAlchemyStationEntity entity)
    {
        Vector2 screenOffset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        Vector2 cell = new Vector2(originI * 16, originJ * 16) - Main.screenPosition + screenOffset;
        Vector2 c = cell + CompositeToCell; // composite (0,0) in screen space
        Color light = Lighting.GetColor(originI, originJ);

        // While this station's world-overlay UI is open, its slots draw the component/output items themselves,
        // so the tile suppresses its own copies to avoid double-drawing.
        bool uiOpen = AlchemyStationSystem.Visible && AlchemyStationSystem.BoundStation == new Point16(originI, originJ);

        // The base is the only thing that always draws (it's the host tile).
        Blit(sb, GetTex(ref _base, AssetReferences.Content.Alchemy.AlchemyStations.AlchemyStation_Base.KEY), c + BaseOffset, light);

        // Chamber assembly: chamber-back + (liquid behind) + chamber body + pipes.
        // While brewing the chamber rocks (eased tilt) and vibrates (~1px) — soft at first, rougher near
        // completion. The back, fluid, and glass move as one rigid body about the pivot. Pipes stay
        // anchored to the components and don't move.
        if (entity != null && entity.HasChamber)
        {
            ChamberMotion motion = BuildChamberMotion(c, entity.IsBrewing, entity.BrewProgress);

            BlitRocked(sb, GetTex(ref _chamberBack, AssetReferences.Content.Alchemy.AlchemyStations.AlchemyStation_ChamberBack.KEY), c + ChamberBackOffset, motion, light);

            // Procedural fluid: reuses the ChamberLiquidOverlay capsule as the liquid's shape, tinted by the
            // vial and animated by brew state. Drawn behind the glass and rocked with the same motion.
            if (!entity.Vial[0].IsAir)
                AdvancedAlchemyStationLiquid.Draw(sb,
                    GetTex(ref _chamberOverlay, AssetReferences.Content.Alchemy.AlchemyStations.ChamberLiquidOverlay.KEY),
                    c + ChamberLiquidOffset, motion, entity.Vial[0].type, light, entity.IsBrewing, entity.BrewProgress);

            BlitRocked(sb, GetTex(ref _chamber, AssetReferences.Content.Alchemy.AlchemyStations.AlchemyStation_Chamber.KEY), c + ChamberOffset, motion, light);
            Blit(sb, GetTex(ref _pipes, AssetReferences.Content.Alchemy.AlchemyStations.AlchemyStation_Pipes.KEY), c + PipesOffset, light);
        }

        // Component housings (independent of chamber). The held component item is drawn BEHIND each cylinder
        // so the glass tube overlays it, and slightly downscaled to fit inside the housing.
        for (int k = 0; k < AdvancedAlchemyStationEntity.ComponentSocketCount; k++)
        {
            if (entity == null || !entity.ComponentPresent[k]) continue;

            Vector2 compTL = c + ComponentOffsets[k];

            if (!entity.Components[k].IsAir)
                BlitItem(sb, entity.Components[k], compTL + ComponentItemOffset, light,
                    scale: ComponentItemScale, center: true);

            Blit(sb, GetTex(ref _component, AssetReferences.Content.Alchemy.AlchemyStations.AlchemyStation_Component.KEY), compTL, light);
        }

        // Heat plate sits on the base under the chamber; animate while brewing.
        if (entity != null && entity.HasChamber)
        {
            int frame = entity.IsBrewing
                ? (int)((Main.GameUpdateCount / HeatPlateTicksPerFrame) % HeatPlateFrames)
                : 0;
            Rectangle heatPlateSrc = new(0, frame * HeatPlateFrameStrideY, HeatPlateFrameWidth, HeatPlateFrameHeight);
            BlitFrame(sb, GetTex(ref _heatPlate, AssetReferences.Content.Alchemy.AlchemyStations.AlchemyStation_HeatPlate.KEY),
                c + HeatPlateOffset, heatPlateSrc, light);

            // Brew progress bar, drawn on the machine just beneath the heat plate (world space → pinned + zoomed).
            if (entity.IsBrewing)
                DrawProgressBar(sb, c + HeatPlateBarOffset, HeatPlateFrameWidth, entity.BrewProgress, light);
        }

        // Output item on the base edge, beneath the spout — driven by the UI's output slot (preview/result).
        if (entity != null && !entity.Output[0].IsAir && !uiOpen)
            BlitItem(sb, entity.Output[0], c + BaseEdgeOutputOffset, light);
    }

    private static void DrawProgressBar(SpriteBatch sb, Vector2 pos, int width, float progress, Color light)
    {
        Texture2D pixel = TextureAssets.MagicPixel.Value;
        const int height = 3;
        progress = MathHelper.Clamp(progress, 0f, 1f);

        // Track + fill, dimmed by tile lighting so the bar sits in the world consistently with the machine.
        sb.Draw(pixel, pos, null, MulRgb(new Color(20, 20, 24), light) * 0.85f, 0f, Vector2.Zero, new Vector2(width, height), SpriteEffects.None, 0f);
        sb.Draw(pixel, pos, null, MulRgb(new Color(170, 230, 120), light), 0f, Vector2.Zero, new Vector2(width * progress, height), SpriteEffects.None, 0f);
    }

    private static Color MulRgb(Color c, Color light) => new(
        (byte)(c.R * light.R / 255), (byte)(c.G * light.G / 255), (byte)(c.B * light.B / 255), c.A);

    private static void Blit(SpriteBatch sb, Texture2D tex, Vector2 pos, Color color)
        => sb.Draw(tex, pos, null, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

    private static void BlitFrame(SpriteBatch sb, Texture2D tex, Vector2 pos, Rectangle src, Color color)
        => sb.Draw(tex, pos, src, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

    /// <summary>Draws a sprite under the chamber's rock+vibration transform (rigid rotation about the pivot).</summary>
    private static void BlitRocked(SpriteBatch sb, Texture2D tex, Vector2 screenTopLeft, ChamberMotion m, Color color)
        => sb.Draw(tex, m.Place(screenTopLeft), null, color, m.Angle, Vector2.Zero, 1f, SpriteEffects.None, 0f);

    /// <summary>
    /// Builds the chamber's brewing motion: a ~1px vibration plus a gentle eased tilt (rock), both
    /// smooth-stepped via <see cref="EaseFunction"/> and ramping soft → rough with brew progress. When not
    /// brewing the assembly is still, but the pivot is always supplied so the liquid composites consistently.
    /// </summary>
    private static ChamberMotion BuildChamberMotion(Vector2 c, bool brewing, float progress)
    {
        Vector2 pivot = c + ChamberPivot;
        if (!brewing)
            return new ChamberMotion(pivot, 0f, Vector2.Zero);

        float t = Main.GlobalTimeWrappedHourly;
        float p = MathHelper.Clamp(progress, 0f, 1f);

        // Vibration: ~1px, eased between stepped targets, snapped to whole pixels so it reads as a crisp buzz.
        float shakeAmp = MathHelper.Lerp(0.5f, 1f, p);
        var shake = new Vector2(
            (float)Math.Round(SteppedNoise(t, 11f, 1) * shakeAmp),
            (float)Math.Round(SteppedNoise(t, 13f, 2) * shakeAmp));

        // Rock: eased pendulum tilt, a fraction of a degree early growing to ~1.7° near completion.
        float rockAmp = MathHelper.Lerp(0.010f, 0.030f, p);
        float angle = SteppedRock(t, 1.2f) * rockAmp;

        return new ChamberMotion(pivot, angle, shake);
    }

    /// <summary>Eased value in [-1,1] that steps to a new pseudo-random target each step (smooth-stepped).</summary>
    private static float SteppedNoise(float t, float stepsPerSecond, int seed)
    {
        float s = t * stepsPerSecond;
        int i = (int)Math.Floor(s);
        float f = EaseFunction.EaseQuadInOut.Ease(s - i);
        float a = Hash01(i, seed) * 2f - 1f;
        float b = Hash01(i + 1, seed) * 2f - 1f;
        return MathHelper.Lerp(a, b, f);
    }

    /// <summary>Eased pendulum in [-1,1] that rocks between opposite extremes each step (smooth-stepped).</summary>
    private static float SteppedRock(float t, float stepsPerSecond)
    {
        float s = t * stepsPerSecond;
        int i = (int)Math.Floor(s);
        float f = EaseFunction.EaseQuadInOut.Ease(s - i);
        float a = (i & 1) == 0 ? -1f : 1f;
        return MathHelper.Lerp(a, -a, f);
    }

    /// <summary>Cheap deterministic hash → [0,1), so stepped targets are stable across frames without state.</summary>
    private static float Hash01(int n, int seed)
    {
        unchecked
        {
            uint x = (uint)(n * 73856093) ^ (uint)(seed * 19349663) ^ 0x9E3779B9u;
            x ^= x >> 16; x *= 0x7FEB352Du; x ^= x >> 15; x *= 0x846CA68Bu; x ^= x >> 16;
            return (x & 0xFFFFFFu) / (float)0x1000000;
        }
    }

    private static void BlitItem(SpriteBatch sb, Item item, Vector2 pos, Color color, float scale = 1f, bool center = false)
    {
        Main.instance.LoadItem(item.type);
        Texture2D? tex = TextureAssets.Item[item.type]?.Value;
        if (tex == null)
            return;

        var anim = Main.itemAnimations[item.type];
        Rectangle frame = anim != null ? anim.GetFrame(tex) : tex.Frame();
        Vector2 origin = center ? frame.Size() * 0.5f : Vector2.Zero;
        sb.Draw(tex, pos, frame, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }
}

/// <summary>
/// The chamber's rock (tilt rotation about <see cref="Pivot"/>) and vibration (<see cref="Shake"/>) for one
/// frame. <see cref="Place"/> maps any unrotated screen top-left into the rocked+shaken frame, so a sprite
/// drawn there with rotation <see cref="Angle"/> and a zero origin rotates rigidly about the shared pivot.
/// </summary>
internal readonly struct ChamberMotion(Vector2 pivot, float angle, Vector2 shake)
{
    public readonly Vector2 Pivot = pivot;
    public readonly float Angle = angle;
    public readonly Vector2 Shake = shake;

    public Vector2 Place(Vector2 screenTopLeft)
        => Pivot + (screenTopLeft - Pivot).RotatedBy(Angle) + Shake;
}
