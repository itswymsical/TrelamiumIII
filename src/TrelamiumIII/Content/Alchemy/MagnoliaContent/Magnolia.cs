using ReLogic.Graphics;
using System.Collections.Generic;
using Terraria.GameContent;
using Terraria.GameContent.Metadata;
using Terraria.ObjectData;
using TrelamiumIII.Content.FarmingOverhaul.Seedbeds;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Graphics.Metaballs;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;

namespace TrelamiumIII.Content.Alchemy.MagnoliaContent;

public class MagnoliaItem : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        Potency = 4,
        Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Catalytic,
        Principle = PrincipleType.Harmony | PrincipleType.Vital,
        Rarity = ItemRarityID.Green,
        Hidden = false,
    };

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Item.ResearchUnlockCount = 100;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width = Item.height = 26;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 15);

        Item.maxStack = Item.CommonMaxStack;
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        if (line.Name == "ItemName")
        {
            var pos = new Vector2(line.X, line.Y);
            var text = line.Text;
            var textWidth = FontAssets.MouseText.Value.MeasureString(text).X;

            Main.spriteBatch.DrawString(
                FontAssets.MouseText.Value,
                text,
                pos,
                Color.White
            );

            var time = Main.GlobalTimeWrappedHourly;
            var frameWidth = 10;
            var frameHeight = 14;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, null, null, null, null, Main.UIScaleMatrix);

            for (var i = 0; i < 4; i++)
            {
                var individualTime = time + i * 1.5f;
                var swayAmount = (float)Math.Sin(individualTime * 1.5f) * 15f;
                var verticalDrift = individualTime * 8f % 40f;
                var horizontalDrift = individualTime * 15f % textWidth;

                var offset = new Vector2(
                    swayAmount + horizontalDrift,
                    verticalDrift
                );

                var rotationSpeed = 0.8f;
                var rotation = individualTime * rotationSpeed + i * MathHelper.PiOver2;

                var particleColor = Color.PaleGoldenrod * (0.5f + (float)Math.Sin(time * 1.2f) * 0.3f);

                var frame = (int)(Main.GameUpdateCount / 6 + i) % 8;
                var sourceRect = new Rectangle(
                    0,
                    frameHeight * frame,
                    frameWidth,
                    frameHeight
                );

                Main.spriteBatch.Draw(
                    ModContent.Request<Texture2D>($"{Assets.Textures.MagnoliaLeaf.KEY}").Value,
                    pos + offset,
                    sourceRect,
                    particleColor * 1.15f,
                    rotation,
                    new Vector2(frameWidth / 2f, frameHeight / 2f),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

            return false;
        }

        return base.PreDrawTooltipLine(line, ref yOffset);
    }
}

public class MagnoliaSeedsItem : ModItem
{
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Item.ResearchUnlockCount = 100;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width = Item.height = 26;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 15);

        Item.maxStack = Item.CommonMaxStack;

        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 19;

        Item.createTile = ModContent.TileType<MagnoliaTile>();
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        if (line.Name == "ItemName")
        {
            var pos = new Vector2(line.X, line.Y);
            var text = line.Text;
            var textWidth = FontAssets.MouseText.Value.MeasureString(text).X;

            Main.spriteBatch.DrawString(
                FontAssets.MouseText.Value,
                text,
                pos,
                Color.White
            );

            var time = Main.GlobalTimeWrappedHourly;
            var frameWidth = 10;
            var frameHeight = 14;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, null, null, null, null, Main.UIScaleMatrix);

            for (var i = 0; i < 4; i++)
            {
                var individualTime = time + i * 1.5f;
                var swayAmount = (float)Math.Sin(individualTime * 1.5f) * 15f;
                var verticalDrift = individualTime * 8f % 40f;
                var horizontalDrift = individualTime * 15f % textWidth;

                var offset = new Vector2(
                    swayAmount + horizontalDrift,
                    verticalDrift
                );

                var rotationSpeed = 0.8f;
                var rotation = individualTime * rotationSpeed + i * MathHelper.PiOver2;

                var particleColor = Color.PaleGoldenrod * (0.5f + (float)Math.Sin(time * 1.2f) * 0.3f);

                var frame = (int)(Main.GameUpdateCount / 6 + i) % 8;
                var sourceRect = new Rectangle(
                    0,
                    frameHeight * frame,
                    frameWidth,
                    frameHeight
                );

                Main.spriteBatch.Draw(
                    ModContent.Request<Texture2D>($"{Assets.Textures.MagnoliaLeaf.KEY}").Value,
                    pos + offset,
                    sourceRect,
                    particleColor * 1.15f,
                    rotation,
                    new Vector2(frameWidth / 2f, frameHeight / 2f),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

            return false;
        }

        return base.PreDrawTooltipLine(line, ref yOffset);
    }
}

public class MagnoliaPowderItem : ModItem
{
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Item.ResearchUnlockCount = 100;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width = Item.height = 26;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 18);
        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 19;
        Item.UseSound = SoundID.Grass;
        Item.maxStack = Item.CommonMaxStack;
        Item.shootSpeed = 5f;
        Item.shoot = ModContent.ProjectileType<MagnoliaPowderProjectile>();
    }

    public override bool PreDrawTooltipLine(DrawableTooltipLine line, ref int yOffset)
    {
        if (line.Name == "ItemName")
        {
            var pos = new Vector2(line.X, line.Y);
            var text = line.Text;
            var textWidth = FontAssets.MouseText.Value.MeasureString(text).X;

            Main.spriteBatch.DrawString(
                FontAssets.MouseText.Value,
                text,
                pos,
                Color.White
            );

            var time = Main.GlobalTimeWrappedHourly;
            var frameWidth = 10;
            var frameHeight = 14;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, null, null, null, null, Main.UIScaleMatrix);

            for (var i = 0; i < 8; i++)
            {
                var individualTime = time + i * 1.5f;
                var swayAmount = (float)Math.Sin(individualTime * 1.5f) * 15f;
                var verticalDrift = individualTime * 8f % 40f;
                var horizontalDrift = individualTime * 15f % textWidth;

                var offset = new Vector2(
                    swayAmount + horizontalDrift,
                    verticalDrift
                );

                var rotationSpeed = 0.8f;
                var rotation = individualTime * rotationSpeed + i * MathHelper.PiOver2;

                var particleColor = Color.PaleGoldenrod * (0.5f + (float)Math.Sin(time * 1.2f) * 0.3f);

                var frame = (int)(Main.GameUpdateCount / 6 + i) % 8;
                var sourceRect = new Rectangle(
                    0,
                    frameHeight * frame,
                    frameWidth,
                    frameHeight
                );

                Main.spriteBatch.Draw(
                    ModContent.Request<Texture2D>($"{Assets.Textures.MagnoliaLeaf.KEY}").Value,
                    pos + offset,
                    sourceRect,
                    particleColor * 1.15f,
                    rotation,
                    new Vector2(frameWidth / 2f, frameHeight / 2f),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, null, null, Main.UIScaleMatrix);

            return false;
        }

        return base.PreDrawTooltipLine(line, ref yOffset);
    }

    public override void AddRecipes()
    {
        CreateRecipe(3)
            .AddIngredient(ModContent.ItemType<MagnoliaItem>(), 2)
            .AddTile(TileID.Bottles)
            .Register();
    }
}

public class MagnoliaPowderProjectile : ModProjectile
{
    public override string Texture => Assets.Textures.NullTex.KEY;
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 32;
        Projectile.friendly = true;
        Projectile.aiStyle = -1;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 18;

        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
    }

    private int delay = 0;
    public override bool? CanCutTiles() => false;

    public override void AI()
    {
        base.AI();

        var layer = MetaballSystem.GetLayer<MagnoliaLayer>();
        if (layer == null) return;

        delay++;

        if (delay >= 2)
        {
            Vector2 tilePos = Projectile.Center / 16f;
            if (Projectile.Hitbox.Intersects(
                new Rectangle((int)(tilePos.X - 0.5f) * 16, (int)(tilePos.Y - 0.5f) * 16, 16, 16)))
            {
                int i = (int)tilePos.X;
                int j = (int)tilePos.Y;

                Tile tile = Main.tile[i, j];

                if (tile != null && tile.HasTile && (tile.TileType == TileID.ImmatureHerbs
                    || tile.TileType == TileID.MatureHerbs || tile.TileType == TileID.BloomingHerbs))
                {
                    if (tile.TileType == TileID.ImmatureHerbs)
                        tile.TileType = TileID.MatureHerbs;
                    else if (tile.TileType == TileID.MatureHerbs)
                        tile.TileType = TileID.BloomingHerbs;
                }
            }
            delay = 0;
        }

        for (int count = 0; count < 7; count++)
        {
            Vector2 drift = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.4f, 1.2f)
              + Main.rand.NextVector2Circular(2.5f, 5.5f);

            layer.SpawnNode(Projectile.Center, drift, radius: 6.7f, life: 32f);
        }
    }
}

public enum PlantStage : byte
{
    Planted,
    Growing,
    Grown
}

public class MagnoliaTile : ModTile
{
    private const int FrameWidth = 18;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] =
            Main.tileObsidianKill[Type] =
            Main.tileCut[Type] =
            Main.tileNoFail[Type] = true;

        TileID.Sets.ReplaceTileBreakUp[Type] =
            TileID.Sets.IgnoredInHouseScore[Type] =
            TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

        TileMaterials.SetForTileId(Type, TileMaterials._materialsByName["Plant"]);

        var name = CreateMapEntryName();
        AddMapEntry(new Color(128, 128, 128), name);

        TileObjectData.newTile.CopyFrom(TileObjectData.StyleAlch);
        TileObjectData.newTile.AnchorValidTiles = [
            TileID.Grass,
        ];

        TileObjectData.newTile.AnchorAlternateTiles = [
            TileID.ClayPot,
                TileID.PlanterBox,
                ModContent.TileType<SeedBedTile_Ash>(),
                ModContent.TileType<SeedBedTile_Dirt>(),
                ModContent.TileType<SeedBedTile_Sand>(),
                ModContent.TileType<SeedBedTile_Mud>(),
            ];

        TileObjectData.addTile(Type);

        HitSound = SoundID.Grass;
        DustType = DustID.BubbleBurst_White;
    }

    public override bool CanPlace(int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j);

        if (tile.HasTile)
        {
            int tileType = tile.TileType;
            if (tileType == Type)
            {
                var stage = GetStage(i, j);
                return stage == PlantStage.Grown;
            }

            else
            {
                if (Main.tileCut[tileType] || TileID.Sets.BreakableWhenPlacing[tileType] || tileType == TileID.WaterDrip || tileType == TileID.LavaDrip || tileType == TileID.HoneyDrip || tileType == TileID.SandDrip)
                {
                    var foliageGrass = tileType == TileID.Plants || tileType == TileID.Plants2;
                    var moddedFoliage = tileType >= TileID.Count && (Main.tileCut[tileType] || TileID.Sets.BreakableWhenPlacing[tileType]);
                    var harvestableVanillaHerb = Main.tileAlch[tileType] && WorldGen.IsHarvestableHerbWithSeed(tileType, tile.TileFrameX / 18);

                    if (foliageGrass || moddedFoliage || harvestableVanillaHerb)
                    {
                        WorldGen.KillTile(i, j);
                        if (!tile.HasTile && Main.netMode == NetmodeID.MultiplayerClient)
                        {
                            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, i, j);
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        return true;
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 0)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
        }
    }

    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
        => offsetY = -2;

    public override bool CanDrop(int i, int j)
    {
        var stage = GetStage(i, j);

        if (stage == PlantStage.Planted)
            return false;

        return true;
    }

    public override IEnumerable<Item> GetItemDrops(int i, int j)
    {
        var stage = GetStage(i, j);

        var worldPosition = new Vector2(i, j).ToWorldCoordinates();
        var nearestPlayer = Main.player[Player.FindClosest(worldPosition, 16, 16)];

        var herbItemType = ModContent.ItemType<MagnoliaItem>();
        var seedItemType = ModContent.ItemType<MagnoliaSeedsItem>();

        var herbItemStack = 1;
        var seedItemStack = Main.rand.Next(1, 2);

        if (nearestPlayer.active && (nearestPlayer.HeldItem.type == ItemID.StaffofRegrowth || nearestPlayer.HeldItem.type == ItemID.AcornAxe))
        {
            herbItemStack = Main.rand.Next(1, 3);
            seedItemStack = Main.rand.Next(3, 5);

        }
        else if (stage == PlantStage.Grown)
        {
            herbItemStack = 1;
            seedItemStack = Main.rand.Next(2, 3);
        }

        if (herbItemType > 0 && herbItemStack > 0)
        {
            yield return new Item(herbItemType, herbItemStack);
        }
        if (seedItemType > 0 && seedItemStack > 0)
        {
            yield return new Item(seedItemType, seedItemStack);
        }
    }

    public override bool IsTileSpelunkable(int i, int j)
    {
        var stage = GetStage(i, j);

        return stage == PlantStage.Grown;
    }

    public override void RandomUpdate(int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j);
        var stage = GetStage(i, j);

        if (stage != PlantStage.Grown)
        {
            tile.TileFrameX += FrameWidth;


            if (Main.netMode != NetmodeID.SinglePlayer)
            {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
        }
    }

    private static PlantStage GetStage(int i, int j)
    {
        var tile = Framing.GetTileSafely(i, j);
        return (PlantStage)(tile.TileFrameX / FrameWidth);
    }
}