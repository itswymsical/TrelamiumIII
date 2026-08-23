using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using Terraria.GameContent;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Common.Globals;

/// <summary>
/// A special item that is used to store solutes/reagents in a container "empty bottle".
/// <para>
/// Has a right-click function that grants the result output from the <see cref="AlchemyStationUI"/>, 
/// and returns an empty bottle, that which was required to "brew" the solute/reagent.
/// </para>
/// This is for cases when the player needs to make a new reagent (such as <see cref="OxidizedIronItem"/>), but there is no *true* solvent required.
/// </summary>
public class ContainerBottleItem : GlobalItem
{

    public override bool InstancePerEntity => true;

    public int StoredItemType = -1;

    public bool IsFilled => StoredItemType != -1;

    public override bool AppliesToEntity(Item item, bool lateInstantiation)
        => item.type == ItemID.Bottle;

    public override bool CanStack(Item destination, Item source)
    {
        if (destination.TryGetGlobalItem(out ContainerBottleItem destBottle) &&
            source.TryGetGlobalItem(out ContainerBottleItem sourceBottle))
        {
            // Only allow stacking if both bottles hold the exact same item type
            // (This also naturally allows empty bottles [-1] to stack with other empty bottles [-1])
            return destBottle.StoredItemType == sourceBottle.StoredItemType;
        }
        return base.CanStack(destination, source);
    }

    public override bool CanStackInWorld(Item destination, Item source)
        => CanStack(destination, source);

    public override bool CanRightClick(Item item)
        => IsFilled;

    public override void RightClick(Item item, Player player)
    {
        // 1. Give the player the stored solute
        player.QuickSpawnItem(player.GetSource_OpenItem(item.type), StoredItemType);

        // 2. Refund the empty bottle
        player.QuickSpawnItem(player.GetSource_OpenItem(item.type), ItemID.Bottle);
    }

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!IsFilled)
            return;

        foreach (var line in tooltips)
        {
            if (line.Name != "ItemName" || line.Mod != "Terraria") continue;
            line.Text = $"{Lang.GetItemNameValue(StoredItemType)} {line.Text}";
            break;
        }
    }

    public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        var bottle = item.GetGlobalItem<ContainerBottleItem>();

        if (bottle.StoredItemType != -1)
        {
            Main.instance.LoadItem(StoredItemType);
            Texture2D texture = TextureAssets.Item[StoredItemType].Value;
            spriteBatch.Draw(texture, position, null, drawColor, 0f, texture.Size() / 2, scale, SpriteEffects.None, 0f);
        }
        return true;
    }

    public override bool PreDrawInWorld(Item item, SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        Vector2 pos = item.position - Main.screenPosition + new Vector2(item.width / 2f, item.height / 2f);

        if (StoredItemType != -1)
        {
            Main.instance.LoadItem(StoredItemType);
            Texture2D texture = TextureAssets.Item[StoredItemType].Value;
            spriteBatch.Draw(texture, pos, null, alphaColor, rotation, texture.Size() / 2, scale, SpriteEffects.None, 0f);
        }

        return base.PreDrawInWorld(item, spriteBatch, lightColor, alphaColor, ref rotation, ref scale, whoAmI);
    }

    public override void SaveData(Item item, TagCompound tag)
    {
        if (IsFilled)
            tag["ContainedItem"] = StoredItemType;
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        StoredItemType =
            tag.ContainsKey("ContainedItem")
                ? tag.GetInt("ContainedItem")
                : -1;
    }
    public override void NetSend(Item item, BinaryWriter writer)
    {
        writer.Write(StoredItemType);
    }

    public override void NetReceive(Item item, BinaryReader reader)
    {
        StoredItemType = reader.ReadInt32();
    }
}