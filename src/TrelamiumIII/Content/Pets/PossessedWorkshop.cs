using System.Diagnostics.CodeAnalysis;
using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Pets;

public class PossessedWorkshop : ModItem
{
    public const int SLOT_COUNT = 5;

    private static readonly int[] StartingStations =
    [
        ItemID.WorkBench,
        ItemID.IronAnvil,
        ItemID.Furnace,
        ItemID.Bottle,
    ];

    public Item[] Stations = [];

    public override void SetStaticDefaults() => ItemID.Sets.OpenableBag[Type] = false;

    public override void SetDefaults()
    {
        Item.width = Item.height = 28;
        Item.maxStack = 1;
        Item.rare = ItemRarityID.Green;
        Item.value = Item.sellPrice(gold: 2);

        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 20;
        Item.UseSound = SoundID.Item2;
        Item.noMelee = true;

        Item.buffType = ModContent.BuffType<PossessedWorkshopBuff>();

        EnsureSlots(fill: true);
    }

    /// <summary>
    ///     Guarantees the array exists and every entry is a real <see cref="Item" />. Safe from any
    ///     entry point, and the only place the starting stations are handed out.
    /// </summary>
    private void EnsureSlots(bool fill = false)
    {
        if (Stations.Length != SLOT_COUNT)
            Stations = new Item[SLOT_COUNT];

        for (int i = 0; i < SLOT_COUNT; i++)
            Stations[i] ??= new Item();

        if (!fill)
            return;

        // Only ever on a workshop that has not been through a save or a network packet: both of
        // those overwrite all five slots, so a workshop deliberately emptied stays empty.
        for (int i = 0; i < StartingStations.Length && i < SLOT_COUNT; i++)
            Stations[i] = new Item(StartingStations[i]);
    }

    public override bool? UseItem(Player player)
    {
        if (player.whoAmI != Main.myPlayer)
            return null;

        player.AddBuff(Item.buffType, 3600);

        return true;
    }

    public override ModItem Clone(Item newEntity)
    {
        var clone = (PossessedWorkshop)base.Clone(newEntity);

        // The base clone is shallow, which would leave two workshops sharing one set of stations
        // and emptying one would empty the other.
        clone.Stations = new Item[SLOT_COUNT];

        for (int i = 0; i < SLOT_COUNT; i++)
            clone.Stations[i] = Stations[i].Clone();

        return clone;
    }

    #region serialization

    public override void SaveData(TagCompound tag)
    {
        for (int i = 0; i < SLOT_COUNT; i++)
        {
            if (Stations[i] is { IsAir: false })
                tag[$"station{i}"] = ItemIO.Save(Stations[i]);
        }
    }

    public override void LoadData(TagCompound tag)
    {
        EnsureSlots();

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            Stations[i] = tag.TryGet($"station{i}", out TagCompound stationTag)
                ? ItemIO.Load(stationTag)
                : new Item();
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        EnsureSlots();

        for (int i = 0; i < SLOT_COUNT; i++)
            ItemIO.Send(Stations[i], writer, writeStack: true);
    }

    public override void NetReceive(BinaryReader reader)
    {
        EnsureSlots();

        for (int i = 0; i < SLOT_COUNT; i++)
        {
            Stations[i] = new Item();
            ItemIO.Receive(Stations[i], reader, readStack: true);
        }
    }

    #endregion
}

public class PossessedWorkshopBuff : ModBuff
{
    public override string Texture => AssetReferences.Content.Pets.PossessedWorkshop.KEY;

    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;

        Main.vanityPet[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.buffTime[buffIndex] = 18000;

        int type = ModContent.ProjectileType<PossessedWorkshopPet>();

        if (player.whoAmI == Main.myPlayer && player.ownedProjectileCounts[type] <= 0)
        {
            Projectile.NewProjectile(
                player.GetSource_Buff(buffIndex),
                player.Center,
                Vector2.Zero,
                type,
                0,
                0f,
                player.whoAmI
            );
        }
    }
}


/// <summary>
///     The apparition: the thing actually doing the possessing. It drifts along just above the
///     player, holds the panel, and the stations answer to it -- nothing it carries survives it.
/// </summary>
public class PossessedWorkshopPet : ModProjectile
{
    public enum WorkshopState
    {
        /// <summary>Drifting along beside the player.</summary>
        Float,

        /// <summary>Holding still while its panel is open, so the panel does not swim about.</summary>
        Idle,
    }

    #region tuning

    /// <summary>Walk further than this from an open panel and it closes itself.</summary>
    public const float PANEL_RANGE = 320f;

    /// <summary>How far above its head the panel sits, in world pixels.</summary>
    public const float PANEL_GAP = 14f;

    /// <summary>Where it likes to sit: behind the player's shoulder and over their head.</summary>
    private const float HOVER_BEHIND = 38f;
    private const float HOVER_ABOVE = 58f;

    /// <summary>Fraction of the remaining gap closed each tick. Low, so it trails rather than sticks.</summary>
    private const float DRIFT = 0.06f;

    private const float MAX_DRIFT_SPEED = 13f;

    /// <summary>Slack around the hover point, so it stops fidgeting once it has arrived.</summary>
    private const float HOVER_SLACK = 6f;

    private const float BOB_AMPLITUDE = 5f;
    private const float BOB_SPEED = 0.045f;

    private const float SWAY_AMPLITUDE = 0.07f;
    private const float SWAY_SPEED = 0.03f;

    private const float TELEPORT_RANGE = 2400f;

    #endregion

    public override string Texture => AssetReferences.Content.Pets.PossessedWorkshopPet.KEY;

    public Player Owner => Main.player[Projectile.owner];

    public WorkshopState State
    {
        get => (WorkshopState)(int)Projectile.ai[0];
        private set => Projectile.ai[0] = (int)value;
    }

    public ref float StateTimer => ref Projectile.ai[1];

    private int facing = 1;

    /// <summary>Runs on regardless of state, so the bob does not restart every time the panel opens.</summary>
    private float driftTimer;

    /// <summary>
    ///     The workshop pet <paramref name="player" /> currently owns, if any.
    /// </summary>
    public static bool TryGetPet(Player player, [NotNullWhen(true)] out Projectile? pet)
    {
        pet = null;

        int type = ModContent.ProjectileType<PossessedWorkshopPet>();

        if (player.ownedProjectileCounts[type] <= 0)
            return false;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (!projectile.active || projectile.type != type || projectile.owner != player.whoAmI)
                continue;

            pet = projectile;

            return true;
        }

        return false;
    }

    public override void SetStaticDefaults()
    {
        Main.projPet[Type] = true;
        Main.projFrames[Type] = 4;

        ProjectileID.Sets.CharacterPreviewAnimations[Type] = ProjectileID.Sets.SimpleLoop(0, 1);
    }

    public override void SetDefaults()
    {
        Projectile.width = 30;
        Projectile.height = 46;

        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 2;

        // It is a ghost. Nothing about it touches the world.
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;

        Projectile.aiStyle = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanCutTiles() => false;

    private bool CheckActive(Player owner)
    {
        if (owner.dead || !owner.active)
        {
            owner.ClearBuff(ModContent.BuffType<PossessedWorkshopBuff>());

            return false;
        }

        if (owner.HasBuff(ModContent.BuffType<PossessedWorkshopBuff>()))
            Projectile.timeLeft = 2;

        return true;
    }

    public override void AI()
    {
        Player owner = Owner;

        if (!CheckActive(owner))
            return;

        driftTimer++;

        if (++Projectile.frameCounter >= 5)
        {
            Projectile.frameCounter = 0;
            Projectile.frame = ++Projectile.frame % Main.projFrames[Type];
        }

        // Only the owner has a panel, so only the owner holds still for one. Left unsynced
        // deliberately: pushing it out would have remote clients, where nothing is open, fight the
        // state back every tick.
        if (Projectile.owner == Main.myPlayer)
        {
            bool tending = PossessedWorkshopUI.IsTending(Projectile);

            if (tending && State != WorkshopState.Idle)
                SetState(WorkshopState.Idle);
            else if (!tending && State == WorkshopState.Idle)
                SetState(WorkshopState.Float);
        }

        StateTimer++;

        if (State == WorkshopState.Idle)
            UpdateIdle(owner);
        else
            UpdateFloat(owner);

        Animate();
    }

    /// <summary>Settles to a stop, so the panel hanging off it has something steady to hang from.</summary>
    private void UpdateIdle(Player owner)
    {
        Projectile.velocity *= 0.8f;

        FaceToward(owner.Center);
    }

    private void UpdateFloat(Player owner)
    {
        Recall(owner);

        // Behind whichever way the player is facing, so it tucks in over their shoulder rather than
        // hanging in front of what they are looking at.
        Vector2 hover = owner.MountedCenter
                        + new Vector2(-owner.direction * HOVER_BEHIND, -HOVER_ABOVE)
                        + new Vector2(0f, (float)Math.Sin(driftTimer * BOB_SPEED) * BOB_AMPLITUDE);

        Vector2 toHover = hover - Projectile.Center;

        if (toHover.Length() < HOVER_SLACK)
        {
            Projectile.velocity *= 0.9f;
        }
        else
        {
            // Eased rather than driven: it should arrive on its own weight, not snap to the point.
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, toHover * DRIFT, 0.25f);

            if (Projectile.velocity.Length() > MAX_DRIFT_SPEED)
                Projectile.velocity = Vector2.Normalize(Projectile.velocity) * MAX_DRIFT_SPEED;
        }

        FaceToward(owner.Center);
    }

    private void Recall(Player owner)
    {
        if (Projectile.Distance(owner.Center) < TELEPORT_RANGE)
            return;

        Projectile.Center = owner.Center;
        Projectile.velocity = Vector2.Zero;
        Projectile.netUpdate = true;
    }

    private void FaceToward(Vector2 position)
    {
        float delta = position.X - Projectile.Center.X;

        if (Math.Abs(delta) > 12f)
            facing = Math.Sign(delta);
    }

    private void Animate()
    {
        Projectile.spriteDirection = facing;

        // A slow list either way, on its own clock rather than on its velocity, so it keeps drifting
        // even while it is holding still for the panel.
        Projectile.rotation = (float)Math.Sin(driftTimer * SWAY_SPEED) * SWAY_AMPLITUDE;
    }

    private void SetState(WorkshopState state)
    {
        State = state;
        StateTimer = 0f;
    }

    public override Color? GetAlpha(Color lightColor)
    {
        // Lifted off the world light and never fully opaque: it is not really there.
        var glow = Color.Lerp(lightColor, Color.White, 0.45f);

        return glow * 0.82f;
    }
}
