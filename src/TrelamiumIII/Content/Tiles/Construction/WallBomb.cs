using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace TrelamiumIII.Content.Tiles.Construction;

public class WallBomb : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Bomb}";
    public const string TOOLTIP_KEY = $"{TrelamiumIII.NAME_PREFIX}Wall Bomb Tooltip";
    public const string CANCEL_TOOLTIP_KEY = $"{TrelamiumIII.NAME_PREFIX}Wall Bomb Cancel Tooltip";

    public override void SetStaticDefaults()
    {
        ItemID.Sets.OpenableBag[Type] = true;
        ItemID.Sets.ItemsThatCountAsBombsForDemolitionistToSpawn[Type] = false;
    }

    public override void SetDefaults()
    {
        Item.useStyle = ItemUseStyleID.Swing;
        Item.shootSpeed = 7f;
        Item.shoot = ModContent.ProjectileType<WallBombProj>();
        Item.width = 8;
        Item.height = 28;
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

        List<int> availableWalls = new List<int>();
        for (int i = 0; i < WallFillPlayer.INVENTORY_SLOTS; i++)
        {
            Item item = player.inventory[i];
            if (!item.IsAir && GetWallIdForItem(item.type) != WallID.None && !availableWalls.Contains(item.type))
                availableWalls.Add(item.type);
        }

        if (availableWalls.Count == 0)
        {
            modPlayer.SelectedWallItemType = ItemID.None;
            Main.NewText("No walls found in inventory.");
            return;
        }

        int currentIndex = availableWalls.IndexOf(modPlayer.SelectedWallItemType);
        int nextIndex = (currentIndex + 1) % availableWalls.Count;
        modPlayer.SelectedWallItemType = availableWalls[nextIndex];

        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    public override bool AltFunctionUse(Player player) => true;

    public override float UseSpeedMultiplier(Player player) => player.altFunctionUse == 2 ? 4f : 1f;

    public override bool CanUseItem(Player player)
    {
        var modPlayer = player.GetModPlayer<WallFillPlayer>();

        if (player.altFunctionUse == 2)
            return modPlayer.HasActiveFill;

        int selectedType = modPlayer.SelectedWallItemType;

        if (GetWallIdForItem(selectedType) == WallID.None)
        {
            if (player.whoAmI == Main.myPlayer)
                Main.NewText("Right click the Wall Bomb in your inventory to pick a wall.");

            return false;
        }

        if (WallFillPlayer.CountInInventory(player, selectedType) > 0)
            return true;

        if (player.whoAmI == Main.myPlayer)
            Main.NewText($"Out of {Lang.GetItemNameValue(selectedType)}.");

        return false;
    }

    public override bool? UseItem(Player player)
    {
        if (player.altFunctionUse != 2)
            return null;

        player.GetModPlayer<WallFillPlayer>().CancelFill();

        if (player.whoAmI == Main.myPlayer)
        {
            SoundEngine.PlaySound(SoundID.MenuClose);
            Main.NewText("Wall placement cancelled.");
        }

        return true;
    }

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        var modPlayer = Main.LocalPlayer.GetModPlayer<WallFillPlayer>();
        int selectedType = modPlayer.SelectedWallItemType;

        string text = "Selected Wall: None";

        if (GetWallIdForItem(selectedType) != WallID.None)
        {
            int totalCount = WallFillPlayer.CountInInventory(Main.LocalPlayer, selectedType);
            string itemName = Lang.GetItemNameValue(selectedType);

            text = $"{itemName} [i:{selectedType}] (x{totalCount})";
        }

        tooltips.Add(new TooltipLine(Mod, TOOLTIP_KEY, text));

        if (modPlayer.HasActiveFill)
            tooltips.Add(new TooltipLine(Mod, CANCEL_TOOLTIP_KEY, "Right click to cancel the running fill"));
    }

    public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        int selectedType = Main.LocalPlayer.GetModPlayer<WallFillPlayer>().SelectedWallItemType;

        if (GetWallIdForItem(selectedType) == WallID.None)
            return;

        int totalCount = WallFillPlayer.CountInInventory(Main.LocalPlayer, selectedType);
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
        if (player.altFunctionUse == 2)
            return false;

        int itemType = player.GetModPlayer<WallFillPlayer>().SelectedWallItemType;
        ushort wallId = GetWallIdForItem(itemType);

        if (wallId == WallID.None)
            return false;

        SoundEngine.PlaySound(SoundID.Item1, player.Center);

        Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, ai1: wallId, ai2: itemType);

        return false;
    }

    public static ushort GetWallIdForItem(int itemType)
    {
        if (!ContentSamples.ItemsByType.TryGetValue(itemType, out Item? sample) || sample is null)
            return WallID.None;

        return sample.createWall > WallID.None ? (ushort)sample.createWall : WallID.None;
    }
}

public class WallBombProj : ModProjectile
{
    public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.Bomb}";

    private const int HITBOX_SIZE = 15;
    private const int FILL_RADIUS = 20;

    public ushort LoadedWallType
    {
        get => (ushort)Projectile.ai[1];
        set => Projectile.ai[1] = value;
    }

    public int SourceItemType
    {
        get => (int)Projectile.ai[2];
        set => Projectile.ai[2] = value;
    }

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.Explosive[Type] = true;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = HITBOX_SIZE;

        Projectile.penetrate = -1;

        Projectile.timeLeft = 300;

        DrawOffsetX = -2;
        DrawOriginOffsetY = -5;
    }

    public override void AI()
    {
        if (Projectile.timeLeft <= 3)
        {
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }
        else if (Main.rand.NextBool())
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

    public override void OnKill(int timeLeft)
    {
        ExplosionEffects();

        if (Projectile.owner != Main.myPlayer)
            return;

        Player owner = Main.player[Projectile.owner];
        if (!owner.active)
            return;

        owner.GetModPlayer<WallFillPlayer>().BeginFill(
            (int)(Projectile.Center.X / 16f),
            (int)(Projectile.Center.Y / 16f),
            LoadedWallType,
            SourceItemType,
            FILL_RADIUS);
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

public class WallFillPlayer : ModPlayer
{
    private const int TILES_PER_STEP = 1;
    private const int STEP_INTERVAL_TICKS = 2;
    private const bool OVERWRITE_EXISTING_WALLS = false;

    private const int MAX_SCANS_PER_STEP = 32;

    private const int SOUND_INTERVAL = 4;

    public const int INVENTORY_SLOTS = 58;

    private static readonly Point[] CardinalDirections =
    {
        new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1)
    };

    public int SelectedWallItemType = ItemID.None;

    private WallFill? activeFill;

    public bool HasActiveFill => activeFill is not null;

    private sealed class WallFill
    {
        public Point Origin;
        public int Radius;
        public ushort WallType;
        public int SourceItemType;
        public int TickTimer;
        public int PlacedCount;

        public readonly Queue<Point> Frontier = new();

        public readonly HashSet<Point> Reached = new();
    }

    public void BeginFill(int originX, int originY, ushort wallType, int sourceItemType, int radius)
    {
        CancelFill();

        if (wallType == WallID.None || sourceItemType <= ItemID.None || radius <= 0)
            return;

        Point? seed = FindSeed(originX, originY);
        if (seed is null)
            return; // buried in solid ground, there is no room here to fill

        var job = new WallFill
        {
            Origin = new Point(originX, originY),
            Radius = radius,
            WallType = wallType,
            SourceItemType = sourceItemType
        };

        job.Reached.Add(seed.Value);
        job.Frontier.Enqueue(seed.Value);

        activeFill = job;
    }

    public void CancelFill() => activeFill = null;

    public override void PostUpdate()
    {
        if (activeFill is null)
            return;

        if (Player.whoAmI != Main.myPlayer)
            return;

        if (++activeFill.TickTimer < STEP_INTERVAL_TICKS)
            return;

        activeFill.TickTimer = 0;
        StepFill(activeFill);
    }

    public override void OnEnterWorld() => CancelFill();

    private void StepFill(WallFill job)
    {
        int placed = 0;
        int scanned = 0;

        while (placed < TILES_PER_STEP && scanned++ < MAX_SCANS_PER_STEP && job.Frontier.Count > 0)
        {
            Point tile = job.Frontier.Dequeue();

            Expand(job, tile);

            if (!CanWall(tile.X, tile.Y, job.WallType))
                continue;

            int slot = FindStack(job.SourceItemType);
            if (slot < 0)
            {
                Main.NewText($"Out of {Lang.GetItemNameValue(job.SourceItemType)}.");
                CancelFill();
                return;
            }

            PlaceWall(tile.X, tile.Y, job.WallType);

            Item stack = Player.inventory[slot];
            if (--stack.stack <= 0)
                stack.TurnToAir();

            if (job.PlacedCount++ % SOUND_INTERVAL == 0)
                SoundEngine.PlaySound(SoundID.Dig, new Vector2(tile.X * 16, tile.Y * 16));

            placed++;
        }

        if (job.Frontier.Count == 0)
            CancelFill();
    }

    private static void Expand(WallFill job, Point from)
    {
        foreach (Point dir in CardinalDirections)
        {
            var next = new Point(from.X + dir.X, from.Y + dir.Y);

            if (Math.Abs(next.X - job.Origin.X) > job.Radius || Math.Abs(next.Y - job.Origin.Y) > job.Radius)
                continue;

            if (!job.Reached.Add(next))
                continue;

            if (IsOpenTile(next.X, next.Y))
                job.Frontier.Enqueue(next);
        }
    }

    private static bool CanWall(int x, int y, ushort wallType)
    {
        if (!WorldGen.InWorld(x, y, 1))
            return false;

        Tile tile = Main.tile[x, y];

        if (tile.WallType == wallType)
            return false;

        return OVERWRITE_EXISTING_WALLS || tile.WallType == WallID.None;
    }

    private static void PlaceWall(int x, int y, ushort wallType)
    {
        Main.tile[x, y].WallType = wallType;
        WorldGen.SquareWallFrame(x, y);

        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendTileSquare(-1, x, y, 1);
    }

    private static bool IsOpenTile(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 1))
            return false;

        Tile tile = Main.tile[x, y];
        return !tile.HasTile || !Main.tileSolid[tile.TileType];
    }

    private static Point? FindSeed(int x, int y)
    {
        if (IsOpenTile(x, y))
            return new Point(x, y);

        foreach (Point dir in CardinalDirections)
        {
            int nx = x + dir.X, ny = y + dir.Y;
            if (IsOpenTile(nx, ny))
                return new Point(nx, ny);
        }

        return null;
    }

    private int FindStack(int itemType)
    {
        for (int i = 0; i < INVENTORY_SLOTS; i++)
        {
            Item item = Player.inventory[i];
            if (item.type == itemType && item.stack > 0)
                return i;
        }

        return -1;
    }

    public static int CountInInventory(Player player, int itemType)
    {
        if (itemType <= ItemID.None)
            return 0;

        int total = 0;
        for (int i = 0; i < INVENTORY_SLOTS; i++)
        {
            if (player.inventory[i].type == itemType)
                total += player.inventory[i].stack;
        }

        return total;
    }
}
