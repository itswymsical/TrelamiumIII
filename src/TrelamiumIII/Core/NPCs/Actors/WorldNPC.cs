using System.Collections.Generic;
using System.IO;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Controllers;
using TrelamiumIII.Core.NPCs.Controllers.Modules;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Base class for NPCs rendered using player models.
/// Draws using a dummy Player (same type you see in a wardrobe or character customization screen).
/// </summary>
public abstract class WorldNPC : ModNPC
{
    public override string Texture => AssetReferences.Core.NPCs.Actors.WorldNPC.KEY;

    /// <summary>
    /// Drives the dummy player's item animation, pose and attacks. Everything the
    /// vanilla renderer reads is written here, once per tick, from <see cref="PostAI"/>.
    /// </summary>
    public PlayerPuppet Puppet { get; private set; }

    /// <summary>
    /// All appearance and equipment state lives here. Mutate this instance.
    /// </summary>
    public Player? DummyPlayer => Puppet?.Player;

    /// <summary>
    /// The tile this NPC considers home. Decoupled from vanilla's
    /// NPC.homeTileX/Y, which is kept at -1 to prevent housing assignment.
    /// </summary>
    public Point AnchorTile { get; private set; }

    public bool IsAtAnchor
    {
        get
        {
            var current = NPC.Center.ToTileCoordinates();
            return Math.Abs(current.X - AnchorTile.X) <= 2
                && Math.Abs(current.Y - AnchorTile.Y) <= 4;
        }
    }

    public float DistanceFromAnchor => Vector2.Distance(NPC.Center, AnchorTile.ToWorldCoordinates());

    public void SetAnchorTile(Point tile)
    {
        AnchorTile = tile;
    }

    public void SetAnchorTile(Vector2 worldPosition)
    {
        SetAnchorTile(worldPosition.ToTileCoordinates());
    }

    /// <summary>
    /// Cached director reference, enabled on first access.
    /// <para>
    /// A world NPC is not much use without one - every anchor, patrol and combat
    /// loop runs through it - so this attaches the component rather than waiting to
    /// be handed one. Override <see cref="UsesDirector"/> to opt out.
    /// </para>
    /// </summary>
    private NPCDirector _director;
    private bool _directorResolved;

    /// <summary>Set false for actors that drive themselves from <see cref="ModNPC.AI"/>.</summary>
    protected virtual bool UsesDirector => true;

    protected NPCDirector Director
    {
        get
        {
            if (!_directorResolved)
            {
                _director = UsesDirector && NPC.TryEnable(out NPCDirector attached) ? attached : null;
                _directorResolved = true;
            }
            return _director;
        }
        set
        {
            _director = value;
            _directorResolved = true;
        }
    }

    protected virtual float AnchorReturnDistance => 200f;

    protected virtual bool AutoReturnToAnchor => true;

    /// <summary>
    /// The module the NPC falls into once it is back at its anchor.
    /// Override to customise (emotes, facing the player, a work loop).
    /// </summary>
    protected virtual IBehaviorModule CreateAnchorIdleModule()
    {
        return new Stay()
            .At(AnchorTile)
            .WithTolerance(2);
    }

    //  Appearance

    /// <summary>
    /// Returns the item currently held by the dummy.
    /// </summary>
    public Item ActiveWeapon => Puppet.HeldItem;

    /// <summary>
    /// Checks if the dummy is currently holding a valid, usable weapon.
    /// </summary>
    public bool HasValidWeapon => ActiveWeapon != null && !ActiveWeapon.IsAir && ActiveWeapon.type > ItemID.None;
    /// <summary>
    /// Sets the gender. True = Male, False = Female.
    /// </summary>
    public bool Male
    {
        get => DummyPlayer.Male;
        set => DummyPlayer.Male = value;
    }

    /// <summary>Skin variant index. 0–9 in vanilla. <see cref="Utilities.Extensions.PlayerClothingStyleID"/></summary>
    public int ClothingStyle
    {
        get => DummyPlayer.skinVariant;
        set => DummyPlayer.skinVariant = value;
    }

    /// <summary>Hair style index.</summary>
    public int HairType
    {
        get => DummyPlayer.hair;
        set => DummyPlayer.hair = value;
    }

    public Color HairColor
    {
        get => DummyPlayer.hairColor;
        set => DummyPlayer.hairColor = value;
    }

    public Color SkinColor
    {
        get => DummyPlayer.skinColor;
        set => DummyPlayer.skinColor = value;
    }

    public Color EyeColor
    {
        get => DummyPlayer.eyeColor;
        set => DummyPlayer.eyeColor = value;
    }

    public Color ShirtColor
    {
        get => DummyPlayer.shirtColor;
        set => DummyPlayer.shirtColor = value;
    }

    public Color UnderShirtColor
    {
        get => DummyPlayer.underShirtColor;
        set => DummyPlayer.underShirtColor = value;
    }

    public Color PantsColor
    {
        get => DummyPlayer.pantsColor;
        set => DummyPlayer.pantsColor = value;
    }

    public Color ShoeColor
    {
        get => DummyPlayer.shoeColor;
        set => DummyPlayer.shoeColor = value;
    }

    // Player.armor layout:
    //   [0]  Head armor      [10] Head vanity
    //   [1]  Body armor      [11] Body vanity
    //   [2]  Leg armor       [12] Leg vanity
    //   [3–9]  Accessories   [13–19] Vanity accessories

    /// <summary>Sets the head armor/vanity item by ID. Pass 0 to clear.</summary>
    public void SetHeadSlot(int itemId, bool vanity = false)
        => SetArmorSlot(vanity ? 10 : 0, itemId);

    /// <summary>Sets the body armor/vanity item by ID. Pass 0 to clear.</summary>
    public void SetBodySlot(int itemId, bool vanity = false)
        => SetArmorSlot(vanity ? 11 : 1, itemId);

    /// <summary>Sets the leg armor/vanity item by ID. Pass 0 to clear.</summary>
    public void SetLegSlot(int itemId, bool vanity = false)
        => SetArmorSlot(vanity ? 12 : 2, itemId);

    /// <summary>
    /// Sets an accessory by ID into the given accessory slot index (0–6).
    /// Pass vanity = true for the vanity accessory row.
    /// </summary>
    public void SetAccessory(int slotIndex, int itemId, bool vanity = false)
    {
        if (slotIndex < 0 || slotIndex > 6)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Accessory slot index must be 0–6.");

        SetArmorSlot(vanity ? 13 + slotIndex : 3 + slotIndex, itemId);
    }

    /// <summary>
    /// Sets a dye in the given dye slot index (mirrors armor slot indices).
    /// <para>
    /// Dye slots: 0 = head dye, 1 = body dye, 2 = leg dye, 3-9 = accessory dyes.
    /// </para>
    /// </summary>
    public void SetDye(int slotIndex, int dyeItemId)
    {
        if (slotIndex < 0 || slotIndex >= DummyPlayer.dye.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        DummyPlayer.dye[slotIndex].SetDefaults(dyeItemId);

        // DummyPlayer never runs Player.UpdateEquips(), so the c* shader cache
        // fields that PlayerDrawSet.BoringSetup reads are never populated.
        // Mirror item.dye into the appropriate field here.
        int shaderId = DummyPlayer.dye[slotIndex].dye;
        switch (slotIndex)
        {
            case 0: DummyPlayer.cHead = shaderId; break;
            case 1: DummyPlayer.cBody = shaderId; break;
            case 2: DummyPlayer.cLegs = shaderId; break;
        }
    }

    /// <summary>
    /// When true, world lighting does not affect the NPC's draw colors.
    /// Defaults to false.
    /// </summary>
    public bool IgnoreWorldLighting
    {
        get => Puppet.IgnoreLighting;
        set => Puppet.IgnoreLighting = value;
    }

    /// <summary>
    /// Override and set false for NPCs that render their own sprite instead of a player model.
    /// Defaults to true.
    /// </summary>
    protected virtual bool UsePlayerRenderer => true;

    /// <summary>
    /// map head icon drawn by <see cref="WorldNPCMapSystem"/>.
    /// Return null path to use a sprite instead of the DummyPlayer.
    /// </summary>
    public virtual string MapHeadTexture => null;

    /// <summary>
    /// When true, <see cref="WorldNPCPersistenceSystem"/> saves this NPC to the
    /// world file and respawns it on load.
    /// Defaults to false.
    /// </summary>
    public virtual bool Persistent => false;
    public virtual bool CanDespawn => true;

    //  Lifecycle
    public override void SetStaticDefaults()
    {
        NPCID.Sets.ActsLikeTownNPC[Type] = true;
        NPCID.Sets.NoTownNPCHappiness[Type] = true;

        NPCID.Sets.DoesntDespawnToInactivityAndCountsNPCSlots[Type] = true;
    }

    /// <summary>
    /// Do not override unless it's for an abstract class; this was intented to be sealed.
    /// Use <see cref="SetWorldNPCDefaults"/> and <see cref="SetAppearanceDefaults"/> instead.
    /// </summary>
    public override void SetDefaults()
    {
        Puppet = new PlayerPuppet(NPC, new Player());
        InitializeDummyPlayer();

        InitializeInventory();

        if (CanDespawn)
        {
            NPC.dontTakeDamageFromHostiles = false;
            NPC.immortal = false;
        }
        else
        {
            NPC.dontTakeDamageFromHostiles = true;
            NPC.immortal = true;
        }

        NPC.townNPC = true;
        TownNPCStayingHomeless = true;
        NPC.friendly = true;
        NPC.homeTileX = -1;
        NPC.homeTileY = -1;

        NPC.gfxOffY = -2f;

        NPC.width = 20;
        NPC.height = 42;

        NPC.damage = 0;
        NPC.defense = 0;
        NPC.lifeMax = 200;
        NPC.knockBackResist = 0.5f;
        NPC.aiStyle = NPCAIStyleID.Passive;
        NPC.npcSlots = 0f;

        SetWorldNPCDefaults();

        SetAppearanceDefaults();
    }

    /// <summary>
    /// Anchors the NPC where it actually spawned.
    ///
    /// <para>
    /// Deliberately not in <see cref="SetDefaults"/>: <see cref="NPC.NewNPC"/> calls
    /// SetDefaults <em>before</em> assigning position, so the NPC's centre is still
    /// (0,0) at that point. Anchoring there sends every world NPC walking toward the
    /// left edge of the world until it wedges itself on terrain and despawns.
    /// </para>
    ///
    /// <para>
    /// <see cref="LoadData"/> runs after this for persisted NPCs, so a saved anchor
    /// still wins.
    /// </para>
    /// </summary>
    public override void OnSpawn(IEntitySource source)
    {
        AnchorTile = NPC.Center.ToTileCoordinates();
    }

    /// <summary>
    /// Sets up the dummy player with safe defaults so it renders correctly
    /// without interfering with real game systems.
    /// </summary>
    /// <summary>
    /// Who this NPC's swings and projectiles may hurt. Harmless by default, so an
    /// actor that is only ever meant to gesture can't accidentally kill anyone.
    /// </summary>
    protected virtual PuppetAllegiance Allegiance => PuppetAllegiance.Harmless;

    private void InitializeDummyPlayer()
    {
        Puppet.Allegiance = Allegiance;

        DummyPlayer.direction = -1;
        DummyPlayer.gravDir = 1f;
        DummyPlayer.isDisplayDollOrInanimate = true;
        DummyPlayer.dead = false;
        DummyPlayer.ghost = false;
        DummyPlayer.active = true;

        // Prevent the engine from treating this as a real networked player.
        DummyPlayer.whoAmI = Main.maxPlayers;
    }

    /// <summary>
    /// Override this to configure the NPC.
    /// <see cref="SetAppearanceDefaults"/>.
    /// </summary>
    protected virtual void SetWorldNPCDefaults() { }

    /// <summary>
    /// Override this to configure the NPC's visual identity. 
    /// Called after <see cref="SetWorldNPCDefaults"/>.
    /// </summary>
    protected abstract void SetAppearanceDefaults();

    public override void ModifyHoverBoundingBox(ref Rectangle boundingBox)
    {
        boundingBox.Inflate(18, 44);
    }

    #region Persistence
    public override void SaveData(TagCompound tag)
    {
        tag["anchorX"] = AnchorTile.X;
        tag["anchorY"] = AnchorTile.Y;

        // Inventory (existing)
        var invTags = new List<TagCompound>();
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            var item = CarriedInventory[i];
            if (item.IsAir) continue;
            invTags.Add(new TagCompound { ["slot"] = i, ["item"] = ItemIO.Save(item) });
        }
        tag["inv"] = invTags;

        // Appearance
        tag["male"] = Male;
        tag["body"] = ClothingStyle;
        tag["hair"] = HairType;
        tag["hairColor"] = (int)HairColor.PackedValue;
        tag["skinColor"] = (int)SkinColor.PackedValue;
        tag["eyeColor"] = (int)EyeColor.PackedValue;
        tag["shirtColor"] = (int)ShirtColor.PackedValue;
        tag["underShirtColor"] = (int)UnderShirtColor.PackedValue;
        tag["pantsColor"] = (int)PantsColor.PackedValue;
        tag["shoeColor"] = (int)ShoeColor.PackedValue;

        // Equipment (catches randomly-rolled helmets, hoods, etc.)
        var armorTags = new List<TagCompound>();
        for (int i = 0; i < DummyPlayer.armor.Length; i++)
        {
            var item = DummyPlayer.armor[i];
            if (item.IsAir) continue;
            armorTags.Add(new TagCompound { ["slot"] = i, ["item"] = ItemIO.Save(item) });
        }
        tag["armor"] = armorTags;

        var dyeTags = new List<TagCompound>();
        for (int i = 0; i < DummyPlayer.dye.Length; i++)
        {
            var item = DummyPlayer.dye[i];
            if (item.IsAir) continue;
            dyeTags.Add(new TagCompound { ["slot"] = i, ["item"] = ItemIO.Save(item) });
        }
        tag["dyes"] = dyeTags;
    }

    public override void LoadData(TagCompound tag)
    {
        if (tag.ContainsKey("anchorX") && tag.ContainsKey("anchorY"))
            AnchorTile = new Point(tag.GetInt("anchorX"), tag.GetInt("anchorY"));

        if (tag.ContainsKey("inv"))
        {
            for (int i = 0; i < CarriedInventorySize; i++)
                CarriedInventory[i].TurnToAir();

            foreach (var entry in tag.GetList<TagCompound>("inv"))
            {
                int slot = entry.GetInt("slot");
                if (slot < 0 || slot >= CarriedInventorySize) continue;
                CarriedInventory[slot] = ItemIO.Load(entry.GetCompound("item"));
            }
        }

        if (tag.ContainsKey("male")) Male = tag.GetBool("male");
        if (tag.ContainsKey("body")) ClothingStyle = tag.GetInt("body");
        if (tag.ContainsKey("hair")) HairType = tag.GetInt("hair");
        if (tag.ContainsKey("hairColor")) HairColor = ColorFromPacked(tag.GetInt("hairColor"));
        if (tag.ContainsKey("skinColor")) SkinColor = ColorFromPacked(tag.GetInt("skinColor"));
        if (tag.ContainsKey("eyeColor")) EyeColor = ColorFromPacked(tag.GetInt("eyeColor"));
        if (tag.ContainsKey("shirtColor")) ShirtColor = ColorFromPacked(tag.GetInt("shirtColor"));
        if (tag.ContainsKey("underShirtColor")) UnderShirtColor = ColorFromPacked(tag.GetInt("underShirtColor"));
        if (tag.ContainsKey("pantsColor")) PantsColor = ColorFromPacked(tag.GetInt("pantsColor"));
        if (tag.ContainsKey("shoeColor")) ShoeColor = ColorFromPacked(tag.GetInt("shoeColor"));

        // Re-derive sound IDs from gender (set in SetAppearanceDefaults)
        NPC.HitSound = Male ? SoundID.PlayerHit : SoundID.FemaleHit;
        NPC.DeathSound = SoundID.PlayerKilled;

        if (tag.ContainsKey("armor"))
        {
            for (int i = 0; i < DummyPlayer.armor.Length; i++)
                DummyPlayer.armor[i].TurnToAir();

            foreach (var entry in tag.GetList<TagCompound>("armor"))
            {
                int slot = entry.GetInt("slot");
                if (slot < 0 || slot >= DummyPlayer.armor.Length) continue;
                DummyPlayer.armor[slot] = ItemIO.Load(entry.GetCompound("item"));
            }
        }

        if (tag.ContainsKey("dyes"))
        {
            foreach (var entry in tag.GetList<TagCompound>("dyes"))
            {
                int slot = entry.GetInt("slot");
                if (slot < 0 || slot >= DummyPlayer.dye.Length) continue;

                // Round-trip through SetDye so the c* shader cache fields stay in sync.
                var loaded = ItemIO.Load(entry.GetCompound("item"));
                SetDye(slot, loaded.type);
            }
        }
    }

    private static Color ColorFromPacked(int packed)
        => new() { PackedValue = (uint)packed };
    #endregion

    //  Housing Prevention
    public override bool CheckConditions(int left, int right, int top, int bottom)
        => false;

    public override bool CanGoToStatue(bool toKingStatue) 
        => false;

    /// <summary>Copies the NPC's physical state onto the dummy player.</summary>
    public void SyncDummyToNPC() => Puppet.SyncTransform();

    public override void SendExtraAI(BinaryWriter writer) => Puppet.WriteNetState(writer);

    public override void ReceiveExtraAI(BinaryReader reader) => Puppet.ReadNetState(reader);

    public int timeAlive;

    /// <summary>
    /// Despawn bookkeeping. Deliberately not in <see cref="PreAI"/>: an active
    /// director returns false from its own PreAI, and tModLoader skips
    /// <see cref="ModNPC.PreAI"/> entirely once a global has vetoed the tick - so a
    /// timer living there would freeze for as long as the NPC was busy.
    /// </summary>
    private void TickDespawn()
    {
        if (!CanDespawn)
            return;

        timeAlive++;

        if (timeAlive < TimeConversion.SetTime(mins: 3) || IsNpcOnscreen(NPC.Center))
            return;

        NPC.active = false;
        NPC.netSkip = -1;
        NPC.life = 0;
    }

    public override bool ModifyDeathMessage(ref NetworkText customText, ref Color color)
    {
        Main.NewText($"{NPC.GivenName} was slain...", Color.Red);

        return false;
    }

    private static bool IsNpcOnscreen(Vector2 center)
    {
        int w = NPC.sWidth + NPC.safeRangeX * 2;
        int h = NPC.sHeight + NPC.safeRangeY * 2;
        Rectangle npcScreenRect = new Rectangle((int)center.X - w / 2, (int)center.Y - h / 2, w, h);
        foreach (Player player in Main.ActivePlayers)
        {
            // If any player is close enough to the traveling merchant, it will prevent the npc from despawning
            if (player.getRect().Intersects(npcScreenRect))
            {
                return true;
            }
        }
        return false;
    }

    public override void PostAI()
    {
        NPC.homeless = true;
        NPC.homeTileX = -1;
        NPC.homeTileY = -1;

        TickDespawn();

        if (AutoReturnToAnchor)
            TryPushAnchorReturn();

        // Runs last so the pose reflects this tick's final position, and so a module
        // that started a swing this tick sees its first animation frame immediately.
        // The one exception is a module in Augment mode, which ticks in the
        // director's PostAI - and tModLoader runs that after this method. There, the
        // director updates the puppet once its module has had its say.
        if (Director is not { TicksInPostAI: true })
            Puppet.Update();
    }

    /// <summary>
    /// When the director has nothing to do, sends the NPC home: walk back if it has
    /// drifted, then settle into the idle module.
    /// </summary>
    private void TryPushAnchorReturn()
    {
        NPCDirector director = Director;

        if (director == null || director.HasProgram)
            return;

        if (DistanceFromAnchor <= AnchorReturnDistance)
        {
            director.Play(NPC, CreateAnchorIdleModule());
            return;
        }

        director.Play(NPC, Behavior.Seq(
            new TravelTo(AnchorTile.ToWorldCoordinates())
                .WithSpeed(BehaviorMovement.DefaultWalkSpeed)
                .WithArrival(32f),
            CreateAnchorIdleModule()));
    }

    //  Drawing
    public sealed override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        => false;

    public sealed override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        SyncDummyToNPC();

        if (UsePlayerRenderer)
        {
            spriteBatch.End();
            PreparePlayerRender();

            // Every layer runs, the held item included. PlayerDrawSet reads the
            // weapon from Player.lastVisualizedSelectedItem and its position from
            // Player.itemLocation - both of which PlayerPuppet.Update wrote this
            // tick, which is why no hand-rolled item draw is needed here.
            Main.PlayerRenderer.DrawPlayer(
                Main.Camera, DummyPlayer, DummyPlayer.position,
                DummyPlayer.fullRotation, DummyPlayer.fullRotationOrigin,
                shadow: 0f, scale: 1f);

            FinalizePlayerRender();
            spriteBatch.Begin(
                SpriteSortMode.Immediate, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None,
                Main.Rasterizer, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        PostDrawWorldNPC(spriteBatch, screenPos, drawColor);

        if (PuppetDebug.ShowHitboxes)
        {
            PuppetDebug.DrawHitbox(spriteBatch, NPC.Hitbox, Color.CornflowerBlue);

            if (Puppet.LastSwingHitbox is { } swing)
                PuppetDebug.DrawHitbox(spriteBatch, swing, Color.Red);
        }
    }

    /// <summary>
    /// Called after the SpriteBatch has been ended, before PlayerRenderer runs.
    /// The device render target is free to redirect here.
    /// </summary>
    protected virtual void PreparePlayerRender() { }

    /// <summary>
    /// Called after PlayerRenderer runs, before the SpriteBatch restarts.
    /// Restore the device render target to null here.
    /// </summary>
    protected virtual void FinalizePlayerRender() { }
    /// <summary>
    /// Optional override for derived classes that need to draw additional
    /// effects on top of the player model (shaders, player auras, items, etc.).
    /// The spriteBatch is active and in world-space when this runs.
    /// </summary>
    protected virtual void PostDrawWorldNPC(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { }

    /// <summary>
    /// Puts an item in the NPC's hand. Pass <see cref="ItemID.None"/> to empty it.
    /// </summary>
    public void EquipItem(int itemId) => Puppet.Hold(itemId);

    /// <summary>
    /// Equips an item and swings it, optionally aimed at a world position.
    /// Returns false if the hand is empty or a swing is already in progress.
    /// </summary>
    public bool StartItemUse(int itemId, Vector2? aimAt = null) => Puppet.BeginUse(itemId, aimAt);

    /// <summary>
    /// Spawns a WorldNPC of type T at the given world position and sets
    /// that position as the anchor tile.
    /// </summary>
    public static T Spawn<T>(Vector2 worldPosition) where T : WorldNPC
    {
        int type = ModContent.NPCType<T>();

        int idx = NPC.NewNPC(
            NPC.GetSource_None(),
            (int)worldPosition.X,
            (int)worldPosition.Y,
            type
        );

        if (idx >= Main.maxNPCs) return null;

        var worldNpc = Main.npc[idx].ModNPC as T;
        worldNpc?.SetAnchorTile(worldPosition);

        return worldNpc;
    }

    private void SetArmorSlot(int slot, int itemId)
    {
        if (slot < 0 || slot >= DummyPlayer.armor.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));

        DummyPlayer.armor[slot].SetDefaults(itemId);
    }

    #region Inventory

    public const int CarriedInventorySize = 10;

    /// <summary>
    /// Items this NPC carries. Dropped in full on death.
    /// Also serves as the pool for trading interactions later.
    /// </summary>
    public Item[] CarriedInventory { get; private set; }

    private void InitializeInventory()
    {
        CarriedInventory = new Item[CarriedInventorySize];
        for (int i = 0; i < CarriedInventorySize; i++)
            CarriedInventory[i] = new Item();
    }


    /// <summary>Items the NPC carries but won't trade away (drops on kill).</summary>
    protected virtual List<Action> SetWorldInventory() =>
[
    () => AddInvItem(ItemID.SilverCoin, Main.rand.Next(20, 80)),
    ];

    /// <summary>
    /// Adds an item to the first available slot, stacking into existing
    /// entries where possible. Returns false if the inventory is full.
    /// </summary>
    public bool AddInvItem(int itemId, int stack = 1)
    {
        // Try to stack into an existing entry first.
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            var slot = CarriedInventory[i];
            if (slot.type == itemId && slot.stack < slot.maxStack)
            {
                slot.stack = Math.Min(slot.stack + stack, slot.maxStack);
                return true;
            }
        }

        // Find an empty slot.
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            if (CarriedInventory[i].IsAir)
            {
                CarriedInventory[i].SetDefaults(itemId);
                CarriedInventory[i].stack = stack;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes one stack of the given item type. Returns the amount actually removed.
    /// </summary>
    public int TakeCarriedItem(int itemId, int amount = 1)
    {
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            var slot = CarriedInventory[i];
            if (slot.type != itemId) continue;

            int taken = Math.Min(slot.stack, amount);
            slot.stack -= taken;

            if (slot.stack <= 0)
                slot.TurnToAir();

            return taken;
        }

        return 0;
    }

    /// <summary>
    /// Returns whether this NPC is carrying at least the given amount of an item.
    /// </summary>
    public bool HasCarriedItem(int itemId, int amount = 1)
    {
        int total = 0;
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            if (CarriedInventory[i].type == itemId)
                total += CarriedInventory[i].stack;
        }
        return total >= amount;
    }

    /// <summary>
    /// Drops all carried items at the NPC's current position.
    /// Called automatically on death.
    /// </summary>
    public void DropInventory(int percentage = 100)
    {
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            var item = CarriedInventory[i];
            if (!item.IsAir)
            {
                int amountToDrop = (int)((item.stack * percentage) / 100f);

                if (amountToDrop > 0)
                {
                    Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), item.type, amountToDrop);
                    item.stack -= amountToDrop;

                    if (item.stack <= 0)
                        item.TurnToAir();
                }
            }
        }
    }

    public override void OnKill()
    {
        DropInventory();
    }

    #endregion
}

public abstract class JobNPC : ModNPC
{
    public override string Texture => Assets.Textures.NullTex.KEY;


    /// <summary>
    /// The tile this NPC considers home. Decoupled from vanilla's
    /// NPC.homeTileX/Y, which is kept at -1 to prevent housing assignment.
    /// </summary>
    public Point AnchorTile { get; private set; }

    public bool IsAtAnchor
    {
        get
        {
            var current = NPC.Center.ToTileCoordinates();
            return Math.Abs(current.X - AnchorTile.X) <= 2
                && Math.Abs(current.Y - AnchorTile.Y) <= 4;
        }
    }

    public float DistanceFromAnchor => Vector2.Distance(NPC.Center, AnchorTile.ToWorldCoordinates());

    public void SetAnchorTile(Point tile)
    {
        AnchorTile = tile;
    }

    public void SetAnchorTile(Vector2 worldPosition)
    {
        SetAnchorTile(worldPosition.ToTileCoordinates());
    }

    /// <summary>
    /// Cached director reference, enabled on first access.
    /// <para>
    /// A world NPC is not much use without one - every anchor, patrol and combat
    /// loop runs through it - so this attaches the component rather than waiting to
    /// be handed one. Override <see cref="UsesDirector"/> to opt out.
    /// </para>
    /// </summary>
    private NPCDirector _director;
    private bool _directorResolved;

    /// <summary>Set false for actors that drive themselves from <see cref="ModNPC.AI"/>.</summary>
    protected virtual bool UsesDirector => true;

    protected NPCDirector Director
    {
        get
        {
            if (!_directorResolved)
            {
                _director = UsesDirector && NPC.TryEnable(out NPCDirector attached) ? attached : null;
                _directorResolved = true;
            }
            return _director;
        }
        set
        {
            _director = value;
            _directorResolved = true;
        }
    }

    protected virtual float AnchorReturnDistance => 200f;

    protected virtual bool AutoReturnToAnchor => true;

    /// <summary>
    /// The module the NPC falls into once it is back at its anchor.
    /// Override to customise (emotes, facing the player, a work loop).
    /// </summary>
    protected virtual IBehaviorModule CreateAnchorIdleModule()
    {
        return new Stay()
            .At(AnchorTile)
            .WithTolerance(2);
    }

    /// <summary>
    /// map head icon drawn by <see cref="WorldNPCMapSystem"/>.
    /// Return null path to use a sprite instead of the DummyPlayer.
    /// </summary>
    public virtual string MapHeadTexture => null;

    /// <summary>
    /// When true, <see cref="WorldNPCPersistenceSystem"/> saves this NPC to the
    /// world file and respawns it on load.
    /// Defaults to true.
    /// </summary>
    public virtual bool Persistent => true;
    /// <summary>
    /// marks whether a job from this NPC has been accepted.
    /// <para>
    /// Used to prevent despawning this NPC while a job is active.
    /// </para>
    /// </summary>
    public bool AJobAccepted;

    //  Lifecycle
    public override void SetStaticDefaults()
    {
        NPCID.Sets.ActsLikeTownNPC[Type] = true;
        NPCID.Sets.NoTownNPCHappiness[Type] = true;

        NPCID.Sets.DoesntDespawnToInactivityAndCountsNPCSlots[Type] = true;
    }

    /// <summary>
    /// Do not override unless it's for an abstract class; this was intented to be sealed.
    /// Use <see cref="SetWorldNPCDefaults"/> and <see cref="SetAppearanceDefaults"/> instead.
    /// </summary>
    public override void SetDefaults()
    {
        if (AJobAccepted)
        {
            NPC.dontTakeDamageFromHostiles = false;
            NPC.immortal = false;
        }
        else
        {
            NPC.dontTakeDamageFromHostiles = true;
            NPC.immortal = true;
        }

        NPC.townNPC = true;
        TownNPCStayingHomeless = true;
        NPC.friendly = true;
        NPC.homeTileX = -1;
        NPC.homeTileY = -1;

        NPC.gfxOffY = -2f;

        NPC.width = 20;
        NPC.height = 42;

        NPC.damage = 0;
        NPC.defense = 0;
        NPC.lifeMax = 200;
        NPC.knockBackResist = 0.5f;
        NPC.aiStyle = NPCAIStyleID.Passive;
        NPC.npcSlots = 0f;
    }

    /// <inheritdoc cref="WorldNPC.OnSpawn"/>
    public override void OnSpawn(IEntitySource source)
    {
        AnchorTile = NPC.Center.ToTileCoordinates();
    }

    public override void ModifyHoverBoundingBox(ref Rectangle boundingBox)
    {
        boundingBox.Inflate(18, 44);
    }

    #region Persistence
    public override void SaveData(TagCompound tag)
    {
        tag["anchorX"] = AnchorTile.X;
        tag["anchorY"] = AnchorTile.Y;
        tag["aJobAccepted"] = AJobAccepted;
    }

    public override void LoadData(TagCompound tag)
    {
        if (tag.ContainsKey("anchorX") && tag.ContainsKey("anchorY"))
        {
            AnchorTile = new Point(
                tag.GetInt("anchorX"),
                tag.GetInt("anchorY"));
        }

        if (tag.ContainsKey("aJobAccepted"))
            AJobAccepted = tag.GetBool("aJobAccepted");
    }

    #endregion

    //  Housing Prevention
    public override bool CheckConditions(int left, int right, int top, int bottom)
        => false;

    public override bool CanGoToStatue(bool toKingStatue)
        => false;

    public int timeAlive;

    /// <summary>
    /// Despawn bookkeeping. Lives outside <see cref="PreAI"/> for the same reason as
    /// <see cref="WorldNPC"/>'s: a director that vetoes the tick would otherwise stop
    /// the timer along with the AI.
    /// </summary>
    private void TickDespawn()
    {
        if (!AJobAccepted)
            return;

        timeAlive++;

        if (timeAlive < TimeConversion.SetTime(mins: 3) || IsNpcOnscreen(NPC.Center))
            return;

        NPC.active = false;
        NPC.netSkip = -1;
        NPC.life = 0;
    }

    public override bool ModifyDeathMessage(ref NetworkText customText, ref Color color)
    {
        Main.NewText($"{NPC.GivenName} was slain...", Color.Red);

        return false;
    }

    protected static bool IsNpcOnscreen(Vector2 center)
    {
        int w = NPC.sWidth + NPC.safeRangeX * 2;
        int h = NPC.sHeight + NPC.safeRangeY * 2;
        Rectangle npcScreenRect = new Rectangle((int)center.X - w / 2, (int)center.Y - h / 2, w, h);
        foreach (Player player in Main.ActivePlayers)
        {
            // If any player is close enough to the traveling merchant, it will prevent the npc from despawning
            if (player.getRect().Intersects(npcScreenRect))
            {
                return true;
            }
        }
        return false;
    }

    public override void PostAI()
    {
        NPC.homeless = true;
        NPC.homeTileX = -1;
        NPC.homeTileY = -1;

        TickDespawn();

        if (AutoReturnToAnchor)
            TryPushAnchorReturn();
    }

    /// <summary>
    /// When the director has nothing to do, sends the NPC home: walk back if it has
    /// drifted, then settle into the idle module.
    /// </summary>
    private void TryPushAnchorReturn()
    {
        NPCDirector director = Director;

        if (director == null || director.HasProgram)
            return;

        if (DistanceFromAnchor <= AnchorReturnDistance)
        {
            director.Play(NPC, CreateAnchorIdleModule());
            return;
        }

        director.Play(NPC, Behavior.Seq(
            new TravelTo(AnchorTile.ToWorldCoordinates())
                .WithSpeed(BehaviorMovement.DefaultWalkSpeed)
                .WithArrival(32f),
            CreateAnchorIdleModule()));
    }

    /// <summary>
    /// Spawns a WorldNPC of type T at the given world position and sets
    /// that position as the anchor tile.
    /// </summary>
    public static T? Spawn<T>(Vector2 worldPosition) where T : WorldNPC
    {
        int type = ModContent.NPCType<T>();

        int idx = NPC.NewNPC(
            NPC.GetSource_None(),
            (int)worldPosition.X,
            (int)worldPosition.Y,
            type
        );

        if (idx >= Main.maxNPCs) return null;

        var worldNpc = Main.npc[idx].ModNPC as T;
        worldNpc?.SetAnchorTile(worldPosition);

        return worldNpc;
    }
}