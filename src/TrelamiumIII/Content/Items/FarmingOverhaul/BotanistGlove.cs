using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Common.DamageMeter;

namespace TrelamiumIII.Content.Items.FarmingOverhaul;

/// <summary>Silvian's first job reward. While equipped, right-clicking with a soil block throws it (<see cref="ThrowableSoil" />).</summary>
public class BotanistGlove : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 24;
        Item.accessory = true;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 50);
    }

    public override void UpdateAccessory(Player player, bool hideVisual) => player.GetModPlayer<BotanistGlovePlayer>().Equipped = true;
}

/// <param name="Damage">Base damage before the player's controlled damage bonus.</param>
/// <param name="SplashRadius">World pixels around the impact that take half damage.</param>
/// <param name="DrawProjectile">Vanilla projectile whose sprite the clod uses.</param>
public readonly record struct SoilClod(int Damage, float Speed, float SplashRadius, short DrawProjectile, short Dust, int Debuff = 0, int DebuffTime = 0);

/// <summary>The soil blocks the <see cref="BotanistGlove" /> can throw. They carry no damage stats of their own.</summary>
public static class ThrowableSoil
{
    private static readonly Dictionary<int, SoilClod> clods = new()
    {
        [ItemID.DirtBlock] = new(8, 9f, 32f, ProjectileID.DirtBall, DustID.Dirt),
        [ItemID.MudBlock] = new(9, 8f, 48f, ProjectileID.MudBall, DustID.Mud),
        [ItemID.SandBlock] = new(7, 9.5f, 32f, ProjectileID.SandBallFalling, DustID.Sand, BuffID.Confused, 60),
        [ItemID.EbonsandBlock] = new(7, 9.5f, 32f, ProjectileID.EbonsandBallFalling, DustID.Corruption, BuffID.Confused, 60),
        [ItemID.CrimsandBlock] = new(7, 9.5f, 32f, ProjectileID.CrimsandBallFalling, DustID.Crimson, BuffID.Confused, 60),
        [ItemID.PearlsandBlock] = new(7, 9.5f, 32f, ProjectileID.PearlSandBallFalling, DustID.Pearlsand, BuffID.Confused, 60),
        [ItemID.SiltBlock] = new(7, 10.5f, 32f, ProjectileID.SiltBall, DustID.Silt),
        [ItemID.SlushBlock] = new(7, 10.5f, 32f, ProjectileID.SlushBall, DustID.Slush, BuffID.Frostburn, 90),
        [ItemID.SnowBlock] = new(7, 9.5f, 32f, ProjectileID.SnowBallFriendly, DustID.Snow, BuffID.Frostburn, 120),
        [ItemID.AshBlock] = new(8, 9f, 32f, ProjectileID.AshBallFalling, DustID.Ash, BuffID.Confused, 60)
    };

    public static bool TryGet(int itemType, out SoilClod clod) => clods.TryGetValue(itemType, out clod);

    public static int DamageFor(Player player, SoilClod clod)
        => (int)player.GetTotalDamage<ChemicalDamageClass>().ApplyTo(clod.Damage);
}

public sealed class BotanistGlovePlayer : ModPlayer
{
    public const int ThrowTime = 18;

    /// <summary>Multiplier on vanilla snowballs thrown while the glove is equipped.</summary>
    public const float SnowballDamage = 1.5f;

    public bool Equipped;

    /// <summary>Set by a right-click that started a throw, so holding the button keeps throwing.</summary>
    private bool throwing;

    public override void ResetEffects() => Equipped = false;

    /// <summary>
    ///     Runs after vanilla has handled right-click interactions, so a click that opened a chest or door has already
    ///     cleared <see cref="Player.releaseUseTile" />. Left click is untouched, so the block still places.
    /// </summary>
    public override bool PreItemCheck()
    {
        if (Player.whoAmI == Main.myPlayer)
            TryThrow();

        return true;
    }

    private void TryThrow()
    {
        if (!Player.controlUseTile)
        {
            throwing = false;
            return;
        }

        Item held = Player.HeldItem;

        if (!Equipped || !ThrowableSoil.TryGet(held.type, out SoilClod clod) || Player.mouseInterface || Player.noItems
            || Player.CCed || Player.talkNPC != -1 || Main.HoveringOverAnNPC || Main.SmartInteractShowingGenuine)
            return;

        if (!throwing)
        {
            if (!Player.releaseUseTile || Player.tileInteractionHappened)
                return;

            throwing = true;
        }

        if (Player.itemAnimation > 0 || Player.itemTime > 0)
            return;

        Throw(held, clod);
        Player.releaseUseTile = false;
    }

    private void Throw(Item held, SoilClod clod)
    {
        Vector2 aim = (Main.MouseWorld - Player.MountedCenter).SafeNormalize(Vector2.UnitX * Player.direction);

        Player.ChangeDir(aim.X >= 0f ? 1 : -1);
        Player.itemAnimation = Player.itemAnimationMax = ThrowTime;
        Player.itemTime = Player.itemTimeMax = ThrowTime;

        Projectile.NewProjectile(Player.GetSource_ItemUse(held), Player.MountedCenter, aim * clod.Speed,
            ModContent.ProjectileType<SoilClodProjectile>(), ThrowableSoil.DamageFor(Player, clod), 0f, Player.whoAmI, held.type);

        SoundEngine.PlaySound(SoundID.Item1, Player.Center);

        if (--held.stack <= 0)
            held.TurnToAir();

        if (Main.netMode == NetmodeID.MultiplayerClient)
            NetMessage.SendData(MessageID.ShotAnimationAndSound, number: Player.whoAmI);
    }
}

/// <summary>Damage lines on soil blocks only while the glove is equipped, and the glove's snowball bonus.</summary>
public sealed class SoilThrowGlobalItem : GlobalItem
{
    public override bool AppliesToEntity(Item item, bool lateInstantiation)
        => item.type == ItemID.Snowball || ThrowableSoil.TryGet(item.type, out _);

    public override void ModifyWeaponDamage(Item item, Player player, ref Terraria.ModLoader.StatModifier damage)
    {
        if (item.type == ItemID.Snowball && player.GetModPlayer<BotanistGlovePlayer>().Equipped)
            damage *= BotanistGlovePlayer.SnowballDamage;
    }

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (Main.gameMenu || !ThrowableSoil.TryGet(item.type, out SoilClod clod) || !Main.LocalPlayer.GetModPlayer<BotanistGlovePlayer>().Equipped)
            return;

        tooltips.Add(new TooltipLine(Mod, "SoilThrow", Mod.GetLocalizationValue("SoilThrow.Throw", ThrowableSoil.DamageFor(Main.LocalPlayer, clod))));

        if (clod.Debuff > 0)
            tooltips.Add(new TooltipLine(Mod, "SoilThrowDebuff", Mod.GetLocalizationValue("SoilThrow.Inflicts", Lang.GetBuffName(clod.Debuff))));
    }
}

/// <summary>A thrown soil block. <c>ai[0]</c> is the soil's item type. Bursts on contact, splashing nearby enemies.</summary>
public sealed class SoilClodProjectile : ModProjectile
{
    public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DirtBall;

    private int SoilItem => (int)Projectile.ai[0];

    /// <summary>The NPC the clod hit directly, which the splash skips. Owner client only.</summary>
    private int struckTarget = -1;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = 1;
        Projectile.timeLeft = 300;
    }

    public override void AI()
    {
        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.25f, 16f);
        Projectile.rotation += Projectile.velocity.X * 0.04f;

        if (ThrowableSoil.TryGet(SoilItem, out SoilClod clod) && Main.rand.NextBool(4))
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, clod.Dust, Scale: 0.8f);
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        struckTarget = target.whoAmI;

        if (ThrowableSoil.TryGet(SoilItem, out SoilClod clod) && clod.Debuff > 0)
            target.AddBuff(clod.Debuff, clod.DebuffTime);
    }

    public override void OnKill(int timeLeft)
    {
        if (!ThrowableSoil.TryGet(SoilItem, out SoilClod clod))
            return;

        SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);

        for (int i = 0; i < 12; i++)
        {
            Dust dust = Dust.NewDustDirect(Projectile.Center - new Vector2(4f), 8, 8, clod.Dust);
            dust.velocity = Main.rand.NextVector2Circular(clod.SplashRadius / 12f, clod.SplashRadius / 12f);
        }

        if (Projectile.owner == Main.myPlayer)
            Splash(clod);
    }

    private void Splash(SoilClod clod)
    {
        int damage = Math.Max(1, Projectile.damage / 2);

        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.whoAmI == struckTarget || !(npc.CanBeChasedBy() || npc.type == NPCID.TargetDummy))
                continue;

            if (Vector2.Distance(npc.Center, Projectile.Center) > clod.SplashRadius + npc.width * 0.5f)
                continue;

            int dealt = npc.SimpleStrikeNPC(damage, Math.Sign(npc.Center.X - Projectile.Center.X), knockBack: 0f,
                damageType: ModContent.GetInstance<ChemicalDamageClass>());

            DamageMeterSystem.Credit(Projectile.owner, npc, dealt, DamageMeterSource.Hit);

            if (clod.Debuff > 0)
                npc.AddBuff(clod.Debuff, clod.DebuffTime);
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        int sprite = ThrowableSoil.TryGet(SoilItem, out SoilClod clod) ? clod.DrawProjectile : ProjectileID.DirtBall;

        Main.instance.LoadProjectile(sprite);
        Texture2D texture = TextureAssets.Projectile[sprite].Value;

        Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor),
            Projectile.rotation, texture.Size() / 2f, Projectile.scale, SpriteEffects.None);

        return false;
    }
}
