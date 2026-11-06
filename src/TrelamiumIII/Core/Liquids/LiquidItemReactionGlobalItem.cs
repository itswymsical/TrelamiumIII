using Terraria.DataStructures;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     Steps <see cref="ItemReaction" />s for dropped items submerged in a conditioned liquid, and floats reaction
///     products at the surface. As with shimmer, steps run only where the item is authoritative (the server, or single
///     player); clients draw the bubbles and receive the result.
/// </summary>
public sealed class LiquidItemReactionGlobalItem : GlobalItem
{
    /// <summary>Vertical speed toward the surface per pixel of distance from it.</summary>
    private const float FloatStiffness = 0.08f;

    private const float MaxFloatSpeed = 2.5f;

    private const float BobAmplitude = 1.5f;

    /// <summary>Tiles searched upward for the surface above a floating item.</summary>
    private const int SurfaceSearch = 40;

    private int progress;

    public override bool InstancePerEntity => true;

    public override void Update(Item item, ref float gravity, ref float maxFallSpeed)
    {
        if (!TryFloatSurface(item, out float surface))
            return;

        float target = surface + MathF.Sin(Main.GameUpdateCount / 30f + item.whoAmI) * BobAmplitude;
        float speed = Math.Clamp((target - item.Center.Y) * FloatStiffness, -MaxFloatSpeed, MaxFloatSpeed);

        gravity = 0f;
        item.velocity.Y = MathHelper.Lerp(item.velocity.Y, speed, 0.2f);
    }

    public override void PostUpdate(Item item)
    {
        if (item.beingGrabbed || FindContact(item) is not { } contact)
        {
            progress = 0;
            return;
        }

        if (!Main.dedServ && Main.rand.NextBool(6))
            Bubble(item, contact.Condition);

        if (Main.netMode == NetmodeID.MultiplayerClient || item.playerIndexTheItemIsReservedFor != Main.myPlayer || ++progress < contact.Reaction.Ticks)
            return;

        progress = 0;
        Step(item, contact);
    }

    /// <summary>The reaction a stack would step with where its centre is submerged, or null.</summary>
    private static Contact? FindContact(Item item)
    {
        Point tile = item.Center.ToTileCoordinates();

        if (!WorldGen.InWorld(tile.X, tile.Y, 2))
            return null;

        Tile liquidTile = Main.tile[tile.X, tile.Y];

        if (liquidTile.LiquidAmount == 0 || SurfaceY(tile.Y, liquidTile.LiquidAmount) > item.Center.Y || InternalLiquidSet.ByType(liquidTile.LiquidType) is not { } liquid)
            return null;

        InternalLiquidType condition = liquid.Get(liquidTile.LiquidCondition());

        if (liquid.ItemReactionFor(condition, item.type) is not { } reaction || item.stack < reaction.Amount)
            return null;

        return new Contact(liquid, condition, reaction, tile);
    }

    private static void Step(Item item, Contact contact)
    {
        (InternalLiquidSet liquid, InternalLiquidType condition, ItemReaction reaction, Point tile) = contact;

        if (reaction.Spent > 0 && !liquid.TrySpend(tile.X, tile.Y, reaction.Spent))
            return;

        if (reaction.Becomes is { } becomes && becomes != condition)
            liquid.Spread(tile.X, tile.Y, condition.Index, becomes.Index);

        Vector2 center = item.Center;
        item.stack -= reaction.Amount;

        if (item.stack <= 0)
        {
            item.TurnToAir();
            item.active = false;
        }

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncItem, number: item.whoAmI);

        if (reaction.Product > ItemID.None)
            SpawnProduct(center, reaction);

        LiquidConditionSystem.Fizz(tile.X, tile.Y);
    }

    private static void SpawnProduct(Vector2 position, ItemReaction reaction)
    {
        int index = Item.NewItem(new EntitySource_Misc("LiquidItemReaction"), position, reaction.Product, reaction.ProductStack, noBroadcast: true);
        if (index >= Main.maxItems)
            return;

        Main.item[index].velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -2f);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncItem, number: index);
    }

    /// <summary>The surface of the liquid a floating item is in or riding on, when that liquid floats its type.</summary>
    private static bool TryFloatSurface(Item item, out float surface)
    {
        surface = 0f;
        int x = (int)(item.Center.X / 16f);
        int y = (int)(item.Center.Y / 16f);

        if (!WorldGen.InWorld(x, y, 2))
            return false;

        // Riding above the surface, the item's own row is dry and the liquid is in the row below.
        if (Main.tile[x, y].LiquidAmount == 0)
            y++;

        Tile tile = Main.tile[x, y];
        if (tile.LiquidAmount == 0 || InternalLiquidSet.ByType(tile.LiquidType) is not { } liquid || !liquid.Floats(item.type))
            return false;

        int top = y;
        for (int k = 0; k < SurfaceSearch && top > 1 && Main.tile[x, top - 1].LiquidAmount > 0 && Main.tile[x, top - 1].LiquidType == tile.LiquidType; k++)
            top--;

        surface = SurfaceY(top, Main.tile[x, top].LiquidAmount);
        return true;
    }

    /// <summary>World Y of the liquid surface in row <paramref name="j" /> when it holds <paramref name="amount" />.</summary>
    private static float SurfaceY(int j, byte amount) => j * 16f + (byte.MaxValue - amount) / 255f * 16f;

    private static void Bubble(Item item, InternalLiquidType condition)
    {
        Dust dust = Dust.NewDustDirect(item.position, item.width, item.height, DustID.Smoke, 0f, -1f, 150, condition.Tint, 0.8f);
        dust.velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.5f));
        dust.noGravity = true;
    }

    private readonly record struct Contact(InternalLiquidSet Liquid, InternalLiquidType Condition, ItemReaction Reaction, Point Tile);
}
