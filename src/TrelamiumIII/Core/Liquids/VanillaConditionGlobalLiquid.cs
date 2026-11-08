using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using Terraria.Graphics;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     For vanilla liquids with a <see cref="InternalLiquidSet" />, the per-internal liquid slope and retro tint, light,
///     waterfall and contact effects that <see cref="InternalLiquid" /> gives its own type.
/// </summary>
public sealed class VanillaConditionGlobalLiquid : GlobalLiquid
{
    private static InternalLiquidSet? Hosted(int type) => type < LiquidID.Count ? InternalLiquidSet.ByType(type) : null;

    public override bool PreSlopeDraw(int i, int j, int type, bool behindBlocks, ref Vector2 drawPosition, ref Rectangle liquidSize, ref VertexColors colors)
    {
        if (Hosted(type) is { } set)
            InternalLiquid.Tint(ref colors, set.InternalLiqAt(i, j).Tint);

        return true;
    }

    public override void RetroDrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref RetroLiquidDrawInfo drawData, float liquidAmountModified, int liquidGFXQuality)
    {
        if (Hosted(type) is { } set)
            drawData.liquidColor = InternalLiquid.Multiply(drawData.liquidColor, set.InternalLiqAt(i, j).Tint);
    }

    public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
    {
        if (Hosted(type) is not { } set)
            return;

        Vector3 light = set.InternalLiqAt(i, j).Light;
        r = Math.Max(r, light.X);
        g = Math.Max(g, light.Y);
        b = Math.Max(b, light.Z);
    }

    public override int? ChooseWaterfallStyle(int i, int j, int type)
        => Hosted(type) is { } set ? set.WaterfallStyle(set.InternalLiqAt(i, j)) : null;

    public override void OnPlayerCollision(Player player, int type)
    {
        if (Hosted(type) is { } set)
            set.ApplyContact(player, set.InternalLiqIn(player.Hitbox));
    }

    public override void OnNPCCollision(NPC npc, int type)
    {
        if (Hosted(type) is { } set)
            set.ApplyContact(npc, set.InternalLiqIn(npc.Hitbox));
    }
}