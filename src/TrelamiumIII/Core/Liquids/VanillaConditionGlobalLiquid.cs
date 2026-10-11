using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using Terraria.Graphics;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     For vanilla liquids with a <see cref="LiquidConditionSet" />, the per-condition slope and retro tint, light,
///     waterfall and contact effects that <see cref="ConditionalLiquid" /> gives its own type.
/// </summary>
public sealed class VanillaConditionGlobalLiquid : GlobalLiquid
{
    private static LiquidConditionSet? Hosted(int type) => type < LiquidID.Count ? LiquidConditionSet.ByType(type) : null;

    public override bool PreSlopeDraw(int i, int j, int type, bool behindBlocks, ref Vector2 drawPosition, ref Rectangle liquidSize, ref VertexColors colors)
    {
        if (Hosted(type) is { } set)
            ConditionalLiquid.Tint(ref colors, set.ConditionAt(i, j).Tint);

        return true;
    }

    public override void RetroDrawEffects(int i, int j, int type, SpriteBatch spriteBatch, ref RetroLiquidDrawInfo drawData, float liquidAmountModified, int liquidGFXQuality)
    {
        if (Hosted(type) is { } set)
            drawData.liquidColor = ConditionalLiquid.Multiply(drawData.liquidColor, set.ConditionAt(i, j).Tint);
    }

    public override void ModifyLight(int i, int j, int type, ref float r, ref float g, ref float b)
    {
        if (Hosted(type) is not { } set)
            return;

        Vector3 light = set.ConditionAt(i, j).Light;
        r = Math.Max(r, light.X);
        g = Math.Max(g, light.Y);
        b = Math.Max(b, light.Z);
    }

    public override int? ChooseWaterfallStyle(int i, int j, int type)
        => Hosted(type) is { } set ? set.WaterfallStyle(set.ConditionAt(i, j)) : null;

    public override void OnPlayerCollision(Player player, int type)
    {
        if (Hosted(type) is { } set)
            set.ApplyContact(player, set.ConditionIn(player.Hitbox));
    }

    public override void OnNPCCollision(NPC npc, int type)
    {
        if (Hosted(type) is { } set)
            set.ApplyContact(npc, set.ConditionIn(npc.Hitbox));
    }
}
