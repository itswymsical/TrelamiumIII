using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Construction;

public class WallBomb : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Bomb}";
    public const string TOOLTIP_KEY = $"{TrelamiumIII.NAME_PREFIX}Wall Bomb Tooltip";
    public override void SetStaticDefaults()
    {
        ItemID.Sets.OpenableBag[Type] = true;
        ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn[Type] = false;
    }

    public ushort LoadedWallType = WallID.Wood; // TODO: Wall selector/loader
    public override void SetDefaults()
    {
        Item.useStyle = ItemUseStyleID.Swing;
        Item.shootSpeed = 7f;
        Item.shoot = ModContent.ProjectileType<WallBombProj>();
        Item.width = 8;
        Item.height = 28;
        //Item.maxStack = Item.CommonMaxStack;
        //Item.consumable = true;
        Item.UseSound = SoundID.Item1;
        Item.useAnimation = 40;
        Item.useTime = 40;
        Item.noUseGraphic = true;
        Item.noMelee = true;
        Item.value = Item.buyPrice(silver: 20);
        Item.rare = ItemRarityID.Blue;
    }
    public override bool ConsumeItem(Player player) => false;

    public override bool CanRightClick() => true;

    public override void RightClick(Player player)
    {
        var modPlayer = player.GetModPlayer<WallFillPlayer>();

        // Gather all unique wall item types currently in the player's inventory
        List<int> availableWalls = new List<int>();
        for (int i = 0; i < 58; i++)
        {
            Item item = player.inventory[i];
            if (!item.IsAir && item.createWall > -1 && !availableWalls.Contains(item.type))
                availableWalls.Add(item.type);
        }

        if (availableWalls.Count == 0)
        {
            modPlayer.SelectedWallItemType = -1;
            Main.NewText("No walls found in inventory.");
            return;
        }

        // Cycle to the next available wall type
        int currentIndex = availableWalls.IndexOf(modPlayer.SelectedWallItemType);
        int nextIndex = (currentIndex + 1) % availableWalls.Count;
        modPlayer.SelectedWallItemType = availableWalls[nextIndex];

        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        var modPlayer = Main.LocalPlayer.GetModPlayer<WallFillPlayer>();
        int selectedType = modPlayer.SelectedWallItemType;

        string text = "Selected Wall: None";

        if (selectedType != -1)
        {
            int totalCount = 0;
            string itemName = Lang.GetItemNameValue(selectedType);

            // Tally up every stack of this item type in the inventory
            for (int i = 0; i < 58; i++)
            {
                if (Main.LocalPlayer.inventory[i].type == selectedType)
                    totalCount += Main.LocalPlayer.inventory[i].stack;
            }

            text = $"{itemName} [i:{selectedType}] (x{totalCount})";
        }

        tooltips.Add(new TooltipLine(Mod, TOOLTIP_KEY, text));
    }

    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        var modPlayer = Main.LocalPlayer.GetModPlayer<WallFillPlayer>();
        int selectedType = modPlayer.SelectedWallItemType;

        if (selectedType == -1)
            return;

        int totalCount = 0;
        for (int i = 0; i < 58; i++)
        {
            if (Main.LocalPlayer.inventory[i].type == selectedType)
                totalCount += Main.LocalPlayer.inventory[i].stack;
        }

        if (totalCount <= 0)
            return;

        Main.instance.LoadItem(selectedType);
        Texture2D texture = TextureAssets.Item[selectedType].Value;

        Rectangle spriteFrame = Main.itemAnimations[selectedType] == null
            ? texture.Frame()
            : Main.itemAnimations[selectedType].GetFrame(texture);

        float miniScale = scale * 0.5f;

        Vector2 spriteOffset = new Vector2(14f, 14f) * scale;
        Vector2 drawPos = position + spriteOffset;

        spriteBatch.Draw(texture, drawPos, spriteFrame, Color.White, 0f, 
            spriteFrame.Size() / 2f, miniScale, SpriteEffects.None, 0f);

        Vector2 textOffset = new Vector2(8f, 6f) * scale;
        Utils.DrawBorderString(spriteBatch, totalCount.ToString(), 
            drawPos + textOffset, Color.White, scale * 0.75f, anchorx: 0.5f, anchory: 0.5f);
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        var modPlayer = player.GetModPlayer<WallFillPlayer>();
        int itemType = modPlayer.SelectedWallItemType;

        ushort wallId = WallID.None;
        if (itemType != -1)
        {
            Item sample = ContentSamples.ItemsByType[itemType];
            if (sample != null && sample.createWall > -1)
                wallId = (ushort)sample.createWall;
        }

        int index = Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
        if (Main.projectile[index].ModProjectile is WallBombProj bomb)
        {
            bomb.LoadedWallType = wallId;
            bomb.SourceItemType = itemType; // Pass the item type so the system knows what to consume
        }

        return false;
    }
}

public class WallBombProj : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Bomb}";
    private const int RADIUS = 15;
    private const int FillRadius = 20;
    public int SourceItemType = -1;
    public ushort LoadedWallType
    {
        get => (ushort)Projectile.ai[1];
        set => Projectile.ai[1] = value;
    }
    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.PlayerHurtDamageIgnoresDifficultyScaling[Type] = true;
        ProjectileID.Sets.Explosive[Type] = true;
    }
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = RADIUS;

        Projectile.friendly = true;
        Projectile.penetrate = -1;

        Projectile.timeLeft = 300;

        DrawOffsetX = -2;
        DrawOriginOffsetY = -5;
    }
    public override void AI()
    {
        if (Projectile.owner == Main.myPlayer && Projectile.timeLeft <= 3)
            BlowUp();
        
        else
        {
            // that dust that appears on it when its rolling
            if (Main.rand.NextBool())
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1f);
                dust.scale = 0.1f + Main.rand.Next(5) * 0.1f;
                dust.fadeIn = 1.5f + Main.rand.Next(5) * 0.1f;
                dust.noGravity = true;
                dust.position = Projectile.Center + new Vector2(1, 0).RotatedBy(Projectile.rotation - 2.1f, default) * 10f;

                dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1f);
                dust.scale = 1f + Main.rand.Next(5) * 0.1f;
                dust.noGravity = true;
                dust.position = Projectile.Center + new Vector2(1, 0).RotatedBy(Projectile.rotation - 2.1f, default) * 10f;
            }
        }

        // bomb physics
        Projectile.ai[0] += 1f;
        if (Projectile.ai[0] > 10f)
        {
            Projectile.ai[0] = 10f;
            if (Projectile.velocity.Y == 0f && Projectile.velocity.X != 0f)
            {
                Projectile.velocity.X = Projectile.velocity.X * 0.96f;

                if (Projectile.velocity.X > -0.01 && Projectile.velocity.X < 0.01)
                {
                    Projectile.velocity.X = 0f;
                    Projectile.netUpdate = true;
                }
            }
            Projectile.velocity.Y = Projectile.velocity.Y + 0.2f;
        }
        Projectile.rotation += Projectile.velocity.X * 0.1f;
    }

    public void BlowUp()
    {
        Projectile.tileCollide = false;
        Projectile.alpha = 255;
    }

    public override void OnKill(int timeLeft)
    {
        ExplosionEffects();

        if (Projectile.owner == Main.myPlayer)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int tileX = (int)(Projectile.Center.X / 16f);
            int tileY = (int)(Projectile.Center.Y / 16f);

            List<Point> interior = WallFillSystem.ScanRoomInterior(tileX, tileY, FillRadius);

            if (interior.Count == 0) return;

            WallFillSystem.QueueFill(new Queue<Point>(interior), LoadedWallType, Projectile.owner, SourceItemType);
        }
    }
    private void ExplosionEffects()
    {
        for (int i = 0; i < 50; i++)
        {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 2f);
            dust.velocity *= 1.4f;
        }

        for (int i = 0; i < 80; i++)
        {
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 3f);
            dust.noGravity = true;
            dust.velocity *= 5f;
            dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 2f);
            dust.velocity *= 3f;
        }

        for (int g = 0; g < 2; g++)
        {
            var goreSpawnPosition = new Vector2(Projectile.position.X + Projectile.width / 2 - 24f, Projectile.position.Y + Projectile.height / 2 - 24f);
            Gore gore = Gore.NewGoreDirect(Projectile.GetSource_FromThis(), goreSpawnPosition, default, Main.rand.Next(61, 64), 1f);
            gore.scale = 1.5f;
            gore.velocity.X += 1.5f;
            gore.velocity.Y += 1.5f;
            gore = Gore.NewGoreDirect(Projectile.GetSource_FromThis(), goreSpawnPosition, default, Main.rand.Next(61, 64), 1f);
            gore.scale = 1.5f;
            gore.velocity.X -= 1.5f;
            gore.velocity.Y += 1.5f;
            gore = Gore.NewGoreDirect(Projectile.GetSource_FromThis(), goreSpawnPosition, default, Main.rand.Next(61, 64), 1f);
            gore.scale = 1.5f;
            gore.velocity.X += 1.5f;
            gore.velocity.Y -= 1.5f;
            gore = Gore.NewGoreDirect(Projectile.GetSource_FromThis(), goreSpawnPosition, default, Main.rand.Next(61, 64), 1f);
            gore.scale = 1.5f;
            gore.velocity.X -= 1.5f;
            gore.velocity.Y -= 1.5f;
        }
    }
}

/// <summary>
/// Scans the enclosed empty space around a point and gradually fills it with a
/// wall type, a few tiles at a time, instead of all in one frame.
/// </summary>
public class WallFillSystem : ModSystem
{
    private const int TilesPerStep = 1;
    private const int StepIntervalTicks = 2;
    private const bool OverwriteExistingWalls = false;

    private static readonly Point[] CardinalDirections =
    {
        new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1)
    };

    private class WallFill
    {
        public Queue<Point> Points;
        public ushort WallType;
        public int TickTimer;
        public int OwnerIndex;
        public int SourceItemType;
    }

    private static readonly List<WallFill> ActiveFills = new();

    public static void QueueFill(Queue<Point> points, ushort wallType, int ownerIndex, int sourceItemType)
    {
        if (sourceItemType == -1 || wallType == WallID.None) return;
        ActiveFills.Add(new WallFill { Points = points, WallType = wallType, TickTimer = 0, OwnerIndex = ownerIndex, SourceItemType = sourceItemType });
    }

    public override void PostUpdateWorld()
    {
        for (int i = ActiveFills.Count - 1; i >= 0; i--)
        {
            WallFill job = ActiveFills[i];

            if (job.Points.Count == 0)
            {
                ActiveFills.RemoveAt(i);
                continue;
            }

            Player player = Main.player[job.OwnerIndex];

            job.TickTimer++;
            if (job.TickTimer < StepIntervalTicks) continue;
            job.TickTimer = 0;

            int placed = 0;
            while (placed < TilesPerStep && job.Points.Count > 0)
            {
                // Find the first available stack of the selected item in the inventory
                int invIndex = System.Array.FindIndex(player.inventory, x => x.type == job.SourceItemType && x.stack > 0);

                if (invIndex == -1)
                {
                    ActiveFills.RemoveAt(i); // Stop filling, your out of resources bub!
                    break;
                }

                Item loadedItem = player.inventory[invIndex];
                Point p = job.Points.Dequeue();

                if (TryPlaceWall(p.X, p.Y, job.WallType))
                {
                    loadedItem.stack--;
                    if (loadedItem.stack <= 0) loadedItem.TurnToAir();
                    placed++;
                }
            }
        }
    }

    private static bool TryPlaceWall(int x, int y, ushort wallType)
    {
        if (!WorldGen.InWorld(x, y, 1)) return false;
        Tile tile = Main.tile[x, y];
        if (tile.WallType == wallType || (!OverwriteExistingWalls && tile.WallType != WallID.None)) return false;

        WorldGen.PlaceWall(x, y, wallType);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendTileSquare(-1, x, y, 1);
        return true;
    }

    public static List<Point> ScanRoomInterior(int originX, int originY, int radius)
    {
        var visited = new HashSet<Point>();
        var frontier = new Queue<Point>();

        Point origin = new Point(originX, originY);

        if (!IsOpenTile(origin.X, origin.Y))
        {
            Point? seed = FindNearbyOpenTile(origin.X, origin.Y);
            if (seed == null)
                return new List<Point>(); // embedded in solid ground/no room here

            origin = seed.Value;
        }

        visited.Add(origin);
        frontier.Enqueue(origin);

        // a cap for insurance
        int maxTiles = (radius * 2 + 1) * (radius * 2 + 1);

        while (frontier.Count > 0 && visited.Count < maxTiles)
        {
            Point current = frontier.Dequeue();

            foreach (Point dir in CardinalDirections)
            {
                Point next = new Point(current.X + dir.X, current.Y + dir.Y);

                // bleed-out prevention
                if (Math.Abs(next.X - originX) > radius || Math.Abs(next.Y - originY) > radius)
                    continue;

                if (visited.Contains(next))
                    continue;

                // Solid tile = the room's boundary. 4-directional only, cuz
                // an 8-directional fill can squeeze diagonally between two solid
                // tiles that only touch at a corner, which is exactly the kind of
                // leak this needs to avoid.
                if (!IsOpenTile(next.X, next.Y))
                    continue;

                visited.Add(next);
                frontier.Enqueue(next);
            }
        }

        var points = new List<Point>(visited);
        points.Sort((a, b) =>
        {
            float da = Vector2.DistanceSquared(a.ToVector2(), origin.ToVector2());
            float db = Vector2.DistanceSquared(b.ToVector2(), origin.ToVector2());
            return da.CompareTo(db);
        });

        return points;
    }

    private static bool IsOpenTile(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 1))
            return false;

        Tile tile = Main.tile[x, y];
        return !tile.HasTile || !Main.tileSolid[tile.TileType];
    }

    private static Point? FindNearbyOpenTile(int x, int y)
    {
        foreach (Point dir in CardinalDirections)
        {
            int nx = x + dir.X, ny = y + dir.Y;
            if (IsOpenTile(nx, ny))
                return new Point(nx, ny);
        }
        return null;
    }

    public override void OnWorldUnload()
    {
        ActiveFills.Clear();
    }
}

public class WallFillPlayer : ModPlayer
{
    public int SelectedWallItemType = -1;
}