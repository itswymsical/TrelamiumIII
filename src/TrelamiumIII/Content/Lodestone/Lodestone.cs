using ReLogic.Utilities;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;
using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Map;

namespace TrelamiumIII.Content.Lodestone;

public class LodestoneItem : ModItem
{
    private const float BASE_MAGNET_RANGE = 95f;
    private const float BASE_MAX_SPEED = 1.7f;

    private const float MAX_MAGNET_RANGE = 380f;
    private const float MAX_SPEED = 6f;

    private const float EXPONENTIAL_BASE = 1.09f;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.value = Item.sellPrice(silver: 2);

        Item.DefaultToPlaceableTile(ModContent.TileType<LodestoneTile>());

        Item.rare = ItemRarityID.Blue;
        Item.holdStyle = ItemHoldStyleID.HoldFront;
    }
  
    public override void HoldItem(Player player)
    {
        base.HoldItem(player);

        MagnetizeItemsHeld(player);
    }

    public override void Update(ref float gravity, ref float maxFallSpeed)
    {
        base.Update(ref gravity, ref maxFallSpeed);
        if (Item.IsAir && !Item.active)
        {
            return;
        }

        MagnetizeItems_World();
    }

    private void MagnetizeItemsHeld(Player player)
    {
        (var range, var maxSpeed) = CalculateEnhancedValues();

        MagnetizeItems(player.Center, range, maxSpeed);
    }

    private void MagnetizeItems_World()
    {
        (var range, var maxSpeed) = CalculateEnhancedValues();

        MagnetizeItems(Item.Center, range, maxSpeed);
    }

    private void MagnetizeItems(Vector2 center, float range, float maxSpeed)
    {
        for (var i = 0; i < Main.maxItems; i++)
        {
            var targetItem = Main.item[i];

            if (targetItem.active && !targetItem.beingGrabbed && targetItem.noGrabDelay == 0 &&
                targetItem != Item && ItemUtils.IsAMetalItem(targetItem))
            {
                var distance = Vector2.Distance(center, targetItem.Center);
                if (distance <= range)
                {
                    var movement = Vector2.Normalize(center - targetItem.Center);
                    var speedFactor = 1f - distance / range;
                    var speed = maxSpeed * speedFactor;
                    targetItem.velocity = movement * speed;
                }
            }
        }
    }

    private(float range, float maxSpeed) CalculateEnhancedValues()
    {
        float stackMultiplier = Math.Min(Item.stack, 500) - 1;

        var enhancedRange = Math.Min(BASE_MAGNET_RANGE * (float)Math.Pow(EXPONENTIAL_BASE, stackMultiplier), MAX_MAGNET_RANGE);
        var enhancedMaxSpeed = Math.Min(BASE_MAX_SPEED * (float)Math.Pow(EXPONENTIAL_BASE, stackMultiplier), MAX_SPEED);

        return (enhancedRange, enhancedMaxSpeed);
    }
}

public class LodestoneTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;
        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileBlockLight[Type] = true;
        DustType = DustID.Tin;
        MineResist = 2f;
        MinPick = 42;
        HitSound = SoundID.Tink;
        Main.tileSpelunker[Type] = true;
        AddMapEntry(new Color(55, 55, 55));
    }
    public override bool IsTileSpelunkable(int i, int j) => true;
}

public class MagnetizedCoilItem : ModItem
{
    public override void SetDefaults()
    {
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(silver: 3);
        Item.width = Item.height = 28;
        Item.maxStack = 999;
    }

    public override void AddRecipes()
    {

        var recipe = CreateRecipe(2);
        recipe.AddIngredient(ItemID.Chain, 2);
        recipe.AddRecipeGroup(nameof(ItemID.CopperBar), 1);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}


public class LodestoneMagnetizerItem : ModItem
{
    public override void SetDefaults()
    {
        Item.useTime = Item.useAnimation = 20;
        Item.width = Item.height = 32;

        Item.autoReuse = Item.useTurn = true;

        Item.value = Item.sellPrice(gold: 4);

        Item.useStyle = ItemUseStyleID.Shoot;
        Item.rare = ItemRarityID.Green;
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;
        Item.shootSpeed = 7f;
        Item.shoot = ModContent.ProjectileType<LodestoneMagnetizerProjectile>();
    }
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        var muzzleOffset = Vector2.Normalize(new Vector2(velocity.X, velocity.Y)) * 25f;
        if (Collision.CanHit(position, 0, 0, position + muzzleOffset, 0, 0))
        {
            position += muzzleOffset;
        }
        Projectile.NewProjectile(source, position, Vector2.Zero, ModContent.ProjectileType<LodestoneMagnetizerProjectile>(), damage, knockback, player.whoAmI);
        return false;
    }
    public override bool CanUseItem(Player player)
        => player.ownedProjectileCounts[ModContent.ProjectileType<LodestoneMagnetizerProjectile>()] <= 0;

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient<LodestoneItem>(25)
        .AddIngredient<MagnetizedCoilItem>(8)
        .AddRecipeGroup(nameof(ItemID.CopperBar), 15)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class LodestoneMagnetizerProjectile : ModProjectile, IDrawPrimitive
{
    public override string Texture => AssetReferences.Content.Lodestone.LodestoneMagnetizerItem.KEY;
    private const float MAGNET_RANGE = 200f;
    private const float ITEM_VACUUM_RANGE = 300f;
    private const float ITEM_VACUUM_SPEED = 8f;
    private const float ARC_ANGLE = MathHelper.Pi / 6;
    private const int MINING_SPEED = 5;
    private int miningTimer = 0;
    private readonly HashSet<int> magnetizedItems = [];

    private Color vacuumColor1 = new(94, 94, 94);
    private Color currentTargetColor = new(150, 150, 150);
    private readonly Vector2 Size = new(100, 50);

    // Sound tracking
    private SlotId startupSoundId = SlotId.Invalid;
    private SlotId loopSoundId = SlotId.Invalid;
    private bool hasPlayedStartup = false;
    private bool hasStartedLoop = false;
    private int soundTimer = 0;

    // Sine wave parameters
    private readonly int SINE_POINTS = 30;
    private float sineAmplitude = 25f;
    private float sineFrequency = 0.8f;
    private float animationSpeed = 0.15f;
    private List<Vector2>? sineTrailPoints;

    readonly Vector2[] _sinePositions = new Vector2[30]; // SINE_POINTS
    VertexTrail? _trail;
    public override void SetDefaults()
    {
        Projectile.width = 26;
        Projectile.height = 26;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.penetrate = -1;
        Projectile.ownerHitCheck = true;
    }

    public override void AI()
    {
        ManageCaches();
        ManageTrail();
        HandleSounds();
        UpdateTargetColor();

        var owner = Main.player[Projectile.owner];
        if (!owner.active || owner.dead || !owner.channel)
        {
            Projectile.Kill();
            return;
        }

        var aimDirection = Vector2.Normalize(Main.MouseWorld - owner.Center);
        Projectile.Center = owner.Center + aimDirection * 32f;

        if (Main.myPlayer == Projectile.owner)
        {
            Projectile.rotation = aimDirection.ToRotation();
            Projectile.netUpdate = true;

            Projectile.direction = Main.MouseWorld.X > owner.Center.X ? 1 : -1;
        }

        owner.ChangeDir(Projectile.direction);

        if (Projectile.owner == Main.myPlayer)
        {
            MagnetizeTiles(owner);
            VacuumItems();
        }

        SetOwnerAnimation(owner);

        miningTimer++;
    }

    private void MagnetizeTiles(Player owner) // quite whimsical \(~.~)/ 
    {
        var start = Projectile.Center - new Vector2(Projectile.width, Projectile.height) / 2;
        var end = Main.MouseWorld;
        var direction = Vector2.Normalize(end - start);
        var distance = Math.Min(Vector2.Distance(start, end), MAGNET_RANGE);

        var leftAngle = direction.ToRotation() - ARC_ANGLE / 2;
        var rightAngle = direction.ToRotation() + ARC_ANGLE / 2;

        for (var angle = leftAngle; angle <= rightAngle; angle += 0.1f)
        {
            var arcDirection = angle.ToRotationVector2();
            for (float i = 0; i <= distance; i += 16f)
            {
                var checkPos = start + arcDirection * i;
                var tileX = (int)(checkPos.X / 16f);
                var tileY = (int)(checkPos.Y / 16f);
                Tile tile = Main.tile[tileX, tileY];
                // The herb exclusion was written as `is not 82 or 83 or 84`, which parses as
                // `(is not 82) or 83 or 84` and so only ever excluded 82. Regrouped to exclude all
                // three, and the Hellstone branch now sits under HasTile with the rest -- as an
                // or-clause at the top level it was reading TileType out of empty tiles.
                if (tile.HasTile
                    && (Main.tileSpelunker[tile.TileType]
                            && tile.TileType is not (TileID.Plants or TileID.MatureHerbs or TileID.BloomingHerbs)
                        || tile.TileType is TileID.Hellstone))
                {
                    if (miningTimer % MINING_SPEED is 0)
                    {
                        BreakTile(tileX, tileY, owner);
                    }
                }
            }
        }
    }

    private static void BreakTile(int x, int y, Player player)
    {

        player.PickTile(x, y, 10);
        if (Main.netMode is NetmodeID.MultiplayerClient)
        {
            NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);
        }
    }

    #region Helper Methods
    private void VacuumItems()
    {
        magnetizedItems.Clear();

        for (var i = 0; i < Main.maxItems; i++)
        {
            var item = Main.item[i];
            if (item.active && item.noGrabDelay is 0 && item.type is not ItemID.None && (ItemUtils.IsAMetalItem(item) || ItemUtils.IsGem(item)))
            {
                var distance = Vector2.Distance(item.Center, Projectile.Center);
                if (distance <= ITEM_VACUUM_RANGE)
                {
                    item.beingGrabbed = true;
                    var directionToMagnet = Vector2.Normalize(Projectile.Center - item.Center);
                    var speed = MathHelper.Lerp(ITEM_VACUUM_SPEED, 1f, distance / ITEM_VACUUM_RANGE);
                    item.velocity = directionToMagnet * speed;
                    // Counteract gravity
                    item.velocity.Y -= 0.2f;

                    magnetizedItems.Add(i);
                }
            }
        }
    }

    private void HandleSounds()
    {
        if (Projectile.owner != Main.myPlayer) return;

        soundTimer++;

        if (!hasPlayedStartup)
        {
            var startupStyle = Assets.SFX.OreVacuum_Start.Asset with
            {
                Volume = 0.4f,
                MaxInstances = 1,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            };

            startupSoundId = SoundEngine.PlaySound(startupStyle, Projectile.Center);
            hasPlayedStartup = true;
        }

        int startupDurationTicks = 77;

        if (hasPlayedStartup && !hasStartedLoop && soundTimer >= startupDurationTicks)
        {
            var loopStyle = Assets.SFX.OreVacuum_Loop.Asset with
            {
                Volume = 0.4f,
                IsLooped = true,
                MaxInstances = 1,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            };

            loopSoundId = SoundEngine.PlaySound(loopStyle, Projectile.Center);
            hasStartedLoop = true;
        }

        if (hasStartedLoop && SoundEngine.TryGetActiveSound(loopSoundId, out var activeSound))
        {
            if (activeSound != null)
            {
                activeSound.Position = Projectile.Center;
            }
        }
    }

    public override void OnKill(int timeLeft)
    {
        if (Projectile.owner == Main.myPlayer)
        {
            if (SoundEngine.TryGetActiveSound(loopSoundId, out var loopSound))
            {
                loopSound?.Stop();
            }

            var endStyle = Assets.SFX.OreVacuum_End.Asset with
            {
                Volume = 0.4f,
                MaxInstances = 1
            };

            SoundEngine.PlaySound(endStyle, Projectile.Center);
        }

        foreach (var itemIndex in magnetizedItems)
        {
            if (itemIndex < Main.item.Length && Main.item[itemIndex].active)
            {
                var item = Main.item[itemIndex];
                item.beingGrabbed = false;

                item.velocity *= 0.5f;

                item.velocity.Y -= 1f;

                item.noGrabDelay = 0;
            }
        }

        magnetizedItems.Clear();
        base.OnKill(timeLeft);
    }

    private void SetOwnerAnimation(Player owner)
    {
        owner.ChangeDir(Projectile.direction);
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = 2;
        owner.itemAnimation = 2;

        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, (owner.Center - Main.MouseWorld).ToRotation() + MathHelper.PiOver2);
    }
    #endregion

    #region Color Helper Methods
    private void UpdateTargetColor()
    {
        Vector2 mousePos = Main.MouseWorld;
        int tileX = (int)(mousePos.X / 16f);
        int tileY = (int)(mousePos.Y / 16f);

        if (tileX >= 0 && tileX < Main.maxTilesX && tileY >= 0 && tileY < Main.maxTilesY)
        {
            Tile tile = Main.tile[tileX, tileY];
            Color targetColor = new Color(135, 206, 235);

            if (tile != null && tile.HasTile)
            {
                targetColor = GetTileMapColor(tile.TileType, tileX, tileY);
                targetColor = EnhanceColor(targetColor);
            }
            else if (tile != null && tile.WallType != WallID.None)
            {
                targetColor = GetWallMapColor(tile.WallType);
                targetColor = EnhanceColor(targetColor);
            }

            float lerpSpeed = 0.1f;
            currentTargetColor = Color.Lerp(currentTargetColor, targetColor, lerpSpeed);
        }
    }

    private Color GetTileMapColor(int tileType, int x, int y)
    {
        try
        {
            MapTile mapTile = Main.Map[x, y];

            Color mapColor = MapHelper.GetMapTileXnaColor(ref mapTile);

            if (mapColor != Color.Black && mapColor != Color.Transparent &&
                (mapColor.R > 0 || mapColor.G > 0 || mapColor.B > 0))
            {
                return mapColor;
            }

            ModTile modTile = TileLoader.GetTile(tileType);
            if (modTile != null)
            {
                return GetFallbackTileColor(tileType);
            }
        }
        catch
        {

        }

        return GetFallbackTileColor(tileType);
    }

    private Color GetFallbackTileColor(int tileType)
    {
        // Provide nice fallback colors for common tiles
        return tileType switch
        {
            TileID.Dirt => new Color(139, 91, 71),
            TileID.Grass => new Color(71, 133, 71),
            TileID.Iron => new Color(205, 127, 50),
            TileID.Copper => new Color(184, 115, 51),
            TileID.Gold => new Color(255, 215, 0),
            TileID.Silver => new Color(192, 192, 192),
            TileID.Sand => new Color(255, 218, 143),
            TileID.WoodBlock => new Color(150, 111, 51),
            TileID.CorruptGrass => new Color(104, 86, 164),
            TileID.CrimsonGrass => new Color(200, 45, 85),
            TileID.HallowedGrass => new Color(120, 185, 225),
            TileID.Tin => new Color(145, 145, 105),
            TileID.Lead => new Color(85, 89, 118),
            TileID.Tungsten => new Color(132, 220, 85),
            TileID.Platinum => new Color(154, 192, 220),
            TileID.Demonite => new Color(123, 97, 163),
            TileID.Crimtane => new Color(200, 45, 85),
            TileID.Meteorite => new Color(95, 61, 53),
            TileID.Obsidian => new Color(49, 40, 58),
            TileID.Hellstone => new Color(200, 89, 89),
            TileID.Cobalt => new Color(43, 109, 167),
            TileID.Mythril => new Color(89, 140, 89),
            TileID.Adamantite => new Color(214, 109, 109),
            TileID.Chlorophyte => new Color(128, 200, 89),
            _ => new Color(150, 150, 150) // air
        };
    }

    private Color GetWallMapColor(int wallType)
    {
        try
        {
            return wallType switch
            {
                WallID.DirtUnsafe => new Color(139, 91, 71),
                WallID.Stone => new Color(100, 100, 100),
                WallID.Wood => new Color(120, 91, 51),
                WallID.Grass => new Color(51, 113, 51),
                WallID.EbonstoneUnsafe => new Color(84, 66, 144),
                WallID.CrimstoneUnsafe => new Color(180, 25, 65),
                WallID.PearlstoneBrickUnsafe => new Color(100, 165, 205),
                _ => new Color(64, 64, 64)
            };
        }
        catch
        {

        }

        return new Color(64, 64, 64);
    }

    private Color EnhanceColor(Color originalColor)
    {
        Vector3 hsv = Main.rgbToHsl(originalColor);

        hsv.Y = Math.Max(hsv.Y, 0.6f);
        hsv.Z = Math.Max(hsv.Z, 0.7f);

        Color enhanced = Main.hslToRgb(hsv);
        enhanced.A = 255;

        return enhanced;
    }
    #endregion

    #region Rendering
    private void UpdateSineTrail()
    {
        if (sineTrailPoints == null)
        {
            sineTrailPoints = new List<Vector2>();
        }

        sineTrailPoints.Clear();

        Vector2 startPos = Projectile.Center;
        Vector2 endPos = Main.MouseWorld;
        Vector2 direction = Vector2.Normalize(endPos - startPos);
        Vector2 perpendicular = new Vector2(-direction.Y, direction.X); // Perpendicular for sine wave oscillation

        float totalDistance = Vector2.Distance(startPos, endPos);
        totalDistance = Math.Min(totalDistance, MAGNET_RANGE); // Clamp to max range

        float timeOffset = Main.GameUpdateCount * animationSpeed;

        for (int i = 0; i < SINE_POINTS; i++)
        {
            float progress = (float)i / (SINE_POINTS - 1);

            // Base position along the line from projectile to mouse
            Vector2 basePos = Vector2.Lerp(startPos, startPos + direction * totalDistance, progress);

            // Calculate sine wave offset
            float sineInput = progress * sineFrequency * MathHelper.TwoPi + timeOffset;
            float sineValue = (float)Math.Sin(sineInput);

            // Create vacuum effect: amplitude INCREASES as we get closer to the target
            float vacuumAmplitude = sineAmplitude * (0.2f + progress * 1.2f); // Start small, get bigger toward end

            // Add animated pulsing effect
            float pulse = 1f + 0.3f * (float)Math.Sin(timeOffset * 2f);
            vacuumAmplitude *= pulse;

            // Apply sine wave perpendicular to the main direction
            Vector2 offset = perpendicular * sineValue * vacuumAmplitude;

            Vector2 finalPos = basePos + offset;
            sineTrailPoints.Add(finalPos);
        }
    }

    private void ManageCaches()
    {
        UpdateSineTrail(); // still computes sineTrailPoints as before

        if (sineTrailPoints == null || sineTrailPoints.Count == 0) return;

        // Copy into pre-allocated array  no per-frame List allocation
        int count = Math.Min(sineTrailPoints.Count, _sinePositions.Length);
        for (int i = 0; i < count; i++)
            _sinePositions[i] = sineTrailPoints[i];
    }

    private void ManageTrail()
    {
        if (sineTrailPoints == null || sineTrailPoints.Count == 0) return;

        _trail ??= new VertexTrail(SINE_POINTS,
            f => MathHelper.Lerp(20f, 40f, f),
            f =>
            {
                if (f >= 0.98f || f <= 0.05f) return Color.Transparent;
                var lerpedColor = Color.Lerp(vacuumColor1, currentTargetColor, f);
                float intensity = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.2f + f * 3f);
                return lerpedColor * intensity;
            });

        _trail.Positions = _sinePositions;
    }

    public void DrawPrimitives()
    {
        var effect = Assets.Shaders.pixelTrail.Asset.Value;

        if (effect != null && _trail != null)
        {
            var world = Matrix.CreateTranslation(-Main.screenPosition.ToVector3());
            var view = Main.GameViewMatrix.TransformationMatrix;
            var projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

            effect.Parameters["time"]?.SetValue(Main.GameUpdateCount * 0.9f);
            effect.Parameters["repeats"]?.SetValue(3f);
            effect.Parameters["pixelation"]?.SetValue(2f);
            effect.Parameters["resolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
            effect.Parameters["sampleTexture"]?.SetValue(ModContent.Request<Texture2D>($"{Assets.Textures.VFX.RibbonTrail.KEY}").Value);

            _trail?.Render(effect);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        var texture = ModContent.Request<Texture2D>(Texture).Value;
        var origin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
        var offset = origin + new Vector2(16, 0);
        var drawPos = Projectile.Center - Main.screenPosition;
        var spriteEffects = Main.player[Projectile.owner].direction == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

        Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, offset, Projectile.scale, spriteEffects, 0);
        return false;
    }
    #endregion
}
