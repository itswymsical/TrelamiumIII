using System.Collections.Generic;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.DevTools;

/// <summary>
///     Dev tool: emits into any registered <see cref="FieldLayer" /> so its physics and its shader
///     can be watched without having to craft the thing that normally produces it.
/// </summary>
/// <remarks>
///     Left click acts, right click cycles the mode, and the picker that chooses the layer appears
///     while the wand is held. Deals no damage and has no recipe.
/// </remarks>
public class FieldWand : ModItem
{
    public override string Texture => Assets.Textures.NullTex.KEY;

    /// <summary>Nodes thrown per use in the directed modes.</summary>
    private const int SPRAY_COUNT = 3;

    private const int PUFF_COUNT = 14;

    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.useTime = Item.useAnimation = 8;
        Item.useStyle = ItemUseStyleID.Shoot;
        Item.autoReuse = true;
        Item.noMelee = true;
        Item.useTurn = true;
        Item.rare = ItemRarityID.Quest;
        Item.value = 0;
    }

    /// <summary>Opens the right-click path; it arrives as <c>player.altFunctionUse == 2</c>.</summary>
    public override bool AltFunctionUse(Player player) => true;

    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse != 2)
            return true;

        FieldWandSelection.CycleMode();

        return false;
    }

    public override bool? UseItem(Player player)
    {
        if (Main.dedServ || player.whoAmI != Main.myPlayer)
            return null;

        if (FieldWandSelection.Layer is not { } layer)
            return null;

        Vector2 cursor = Main.MouseWorld;

        switch (FieldWandSelection.Mode)
        {
            case FieldWandMode.Spray:
                Spray(layer, player, cursor);
                break;

            case FieldWandMode.Puff:
                Puff(layer, cursor);
                break;

            case FieldWandMode.Burst when layer is GasField gas:
                gas.SpawnBurst(BurstProfile.FlaskShatter, cursor, Vector2.UnitY, grounded: false, owner: player.whoAmI);
                break;

            case FieldWandMode.Cling:
                Cling(layer, cursor);
                break;

            case FieldWandMode.Clear:
                layer.ClearNodes();
                break;
        }

        return true;
    }

    private static void Spray(FieldLayer layer, Player player, Vector2 cursor)
    {
        Vector2 origin = player.MountedCenter;
        Vector2 heading = (cursor - origin).SafeNormalize(Vector2.UnitX);

        for (int i = 0; i < SPRAY_COUNT; i++)
        {
            layer.SpawnNode(
                origin + heading * 16f,
                heading.RotatedByRandom(0.25f) * Main.rand.NextFloat(3f, 7f),
                radius: Main.rand.NextFloat(12f, 22f),
                life: Main.rand.NextFloat(90f, 150f),
                owner: player.whoAmI);
        }
    }

    private static void Puff(FieldLayer layer, Vector2 cursor)
    {
        for (int i = 0; i < PUFF_COUNT; i++)
        {
            layer.SpawnNode(
                cursor + Main.rand.NextVector2Circular(18f, 18f),
                Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.8f),
                radius: Main.rand.NextFloat(14f, 26f),
                life: Main.rand.NextFloat(120f, 200f),
                owner: Main.myPlayer);
        }
    }

    /// <summary>Anchors a cloud to whatever is nearest the cursor, so the anchor path gets exercised.</summary>
    private static void Cling(FieldLayer layer, Vector2 cursor)
    {
        NPC? host = null;
        float nearest = 200f * 200f;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || npc.friendly)
                continue;

            float distance = Vector2.DistanceSquared(npc.Center, cursor);

            if (distance >= nearest)
                continue;

            nearest = distance;
            host = npc;
        }

        Entity anchor = host ?? (Entity)Main.LocalPlayer;

        for (int i = 0; i < PUFF_COUNT; i++)
        {
            layer.SpawnAnchoredNode(
                anchor,
                anchor.Center + Main.rand.NextVector2Circular(anchor.width * 0.6f, anchor.height * 0.6f),
                Main.rand.NextVector2Circular(0.4f, 0.4f),
                radius: Main.rand.NextFloat(10f, 18f),
                life: Main.rand.NextFloat(120f, 200f),
                anchorTicks: 90,
                owner: Main.myPlayer);
        }
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        string layer = FieldWandSelection.Layer is { } selected ? FieldWandSelection.NameOf(selected) : "none registered";

        tooltips.Add(new TooltipLine(Mod, "FieldWandLayer", $"Layer: {layer}"));
        tooltips.Add(new TooltipLine(Mod, "FieldWandMode", $"Mode: {FieldWandSelection.Mode}"));
    }
}
