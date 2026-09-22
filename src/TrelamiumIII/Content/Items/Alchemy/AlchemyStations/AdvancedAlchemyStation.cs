using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Core;
using TrelamiumIII.Core.UI.AlchemyStation;

namespace TrelamiumIII.Content.Items.Alchemy.AlchemyStations;

public class AdvancedAlchemyStationItem : ModItem
{
    public override string Texture => AssetReferences.Content.Items.Alchemy.AlchemyStations.FullImage.KEY;

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 28;
        Item.maxStack = Item.CommonMaxStack;
        Item.rare = ItemRarityID.Yellow;
        Item.value = Item.sellPrice(gold: 3);
        Item.DefaultToPlaceableTile(ModContent.TileType<AdvancedAlchemyStationBase>());
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.GoldBar, 2)
            .AddIngredient(ItemID.IronBar, 20)
            .AddIngredient(ItemID.TungstenBar, 4)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>Right-click an Advanced Alchemy Station while holding this to install the chamber segment.</summary>
public class AdvancedAlchemyStationChamberItem : ModItem
{
    public override string Texture => AssetReferences.Content.Items.Alchemy.AlchemyStations.AlchemyStation_Chamber.KEY;

    public override void SetDefaults()
    {
        Item.width = 32;
        Item.height = 32;
        Item.maxStack = 99;
        Item.rare = ItemRarityID.Yellow;
        Item.value = Item.sellPrice(gold: 1, silver: 80);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Glass, 30)
            .AddRecipeGroup(RecipeGroupID.IronBar, 8)
            .AddIngredient(ItemID.GoldBar, 2)
            .AddIngredient(ItemID.PlatinumBrick, 20)
            .AddIngredient(ItemID.Bottle, 1)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>Right-click an Advanced Alchemy Station while holding this to install one component pump in the next empty socket.</summary>
public class AdvancedAlchemyStationComponentItem : ModItem
{
    public override string Texture => AssetReferences.Content.Items.Alchemy.AlchemyStations.AlchemyStation_Component.KEY;

    public override void SetDefaults()
    {
        Item.width = 20;
        Item.height = 32;
        Item.maxStack = 99;
        Item.rare = ItemRarityID.Yellow;
        Item.value = Item.sellPrice(silver: 88);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.GoldBar, 2)
            .AddIngredient(ItemID.Glass, 8)
            .AddIngredient(ItemID.PlatinumBar, 2)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

/// <summary>
/// The only placeable station tile: hosts the <see cref="AdvancedAlchemyStationEntity"/>, draws the whole composite
/// from its origin cell, and handles opening the UI and installing segments.
/// </summary>
public class AdvancedAlchemyStationBase : ModTile
{
    /// <summary>Footprint of the base multitile, in tiles.</summary>
    public const int Width = 14;
    public const int Height = 2;

    /// <summary>Divisor applied to an output's <see cref="Core.Alchemy.AlmanacEntry.BrewTicks" />.</summary>
    public const float BrewSpeed = 2f;

    public override string Texture => AssetReferences.Content.Items.Alchemy.AlchemyStations.AlchemyStation_Base.KEY;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileSolidTop[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileLavaDeath[Type] = false;
        DustType = DustID.Stone;

        TileID.Sets.DisableSmartCursor[Type] = true;
        MineResist = 2f;

        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinateHeights = [16, 14];
        TileObjectData.newTile.CoordinatePadding = 2;
        TileObjectData.newTile.Origin = new Point16(Width / 2, 1);
        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.AnchorBottom = new AnchorData(
            AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.Table, Width, 0);

        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(
            ModContent.GetInstance<AdvancedAlchemyStationEntity>().Hook_AfterPlacement, -1, 0, false);

        TileObjectData.addTile(Type);

        AddMapEntry(new Color(140, 200, 180), CreateMapEntryName());
    }

    public override void KillMultiTile(int i, int j, int frameX, int frameY)
    {
        ModContent.GetInstance<AdvancedAlchemyStationEntity>().Kill(i, j);
    }

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        // Draw the whole station once, from the top-left cell; suppress vanilla draw for every cell.
        Tile tile = Main.tile[i, j];
        if (tile.TileFrameX == 0 && tile.TileFrameY == 0)
        {
            AdvancedAlchemyStationEntity.TryGetHost(i, j, out AdvancedAlchemyStationEntity entity);
            AdvancedAlchemyStationRenderer.Draw(spriteBatch, i, j, entity);
        }
        return false;
    }

    /// <summary>True once the chamber and every component socket are installed.</summary>
    private static bool IsFunctional(AdvancedAlchemyStationEntity entity)
    {
        if (!entity.HasChamber)
            return false;

        foreach (bool present in entity.ComponentPresent)
        {
            if (!present)
                return false;
        }

        return true;
    }

    public override bool RightClick(int i, int j)
    {
        if (!AdvancedAlchemyStationEntity.TryGetHost(i, j, out AdvancedAlchemyStationEntity entity))
            return false;

        Player player = Main.LocalPlayer;
        Item held = player.HeldItem;

        if (held != null && !held.IsAir)
        {
            if (held.type == ModContent.ItemType<AdvancedAlchemyStationChamberItem>() && TryInstallChamber(entity, held))
                return true;

            if (held.type == ModContent.ItemType<AdvancedAlchemyStationComponentItem>() && TryInstallComponent(entity, held))
                return true;
        }

        if (!IsFunctional(entity))
        {
            Main.NewText("This station is missing parts.", 200, 200, 200);
            return true;
        }

        SoundEngine.PlaySound(SoundID.MenuTick);
        AlchemyStationSystem.ToggleUI(entity.Position, entity);
        player.mouseInterface = true;
        return true;
    }

    public override void MouseOver(int i, int j)
    {
        Player player = Main.LocalPlayer;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ItemID.AlchemyTable;
    }

    private static bool TryInstallChamber(AdvancedAlchemyStationEntity entity, Item held)
    {
        if (entity.HasChamber)
            return false;

        entity.HasChamber = true;
        ConsumeOne(held);
        SoundEngine.PlaySound(SoundID.Research);
        entity.SyncToClients();
        return true;
    }

    private static bool TryInstallComponent(AdvancedAlchemyStationEntity entity, Item held)
    {
        for (int k = 0; k < entity.ComponentPresent.Length; k++)
        {
            if (entity.ComponentPresent[k])
                continue;

            entity.ComponentPresent[k] = true;
            ConsumeOne(held);
            SoundEngine.PlaySound(SoundID.Research);
            entity.SyncToClients();
            return true;
        }
        return false; // all sockets full
    }

    private static void ConsumeOne(Item item)
    {
        item.stack--;
        if (item.stack <= 0)
            item.TurnToAir();
    }
}
