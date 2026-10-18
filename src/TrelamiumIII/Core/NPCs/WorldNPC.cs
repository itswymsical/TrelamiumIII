using System.Collections.Generic;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Behaviors;
using TrelamiumIII.Core.NPCs.Housing;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.NPCs;

/// <summary>
/// An NPC the world puts somewhere on purpose: narrative speakers, job providers,
/// cutscene actors, and the occasional one that turns on you.
///
/// <para>
/// What it provides is placement and AI. It holds an anchor it belongs to and returns
/// to, carries an inventory, saves itself into the world when asked, and runs a town
/// AI modelled on vanilla's aiStyle 7 (see <see cref="TownState"/>). An
/// <see cref="NPCDirector"/> behaviour pauses that AI while it plays. It says nothing
/// about how the NPC looks - <see cref="Actors.TerrarianNPC"/> renders as a player.
/// </para>
///
/// <para>
/// Override <see cref="SetWorldNPCDefaults"/> for stats, and the town AI properties
/// (<see cref="WanderRadius"/>, <see cref="CombatWeapon"/>...) for what it does.
/// </para>
/// </summary>
public abstract partial class WorldNPC : ModNPC
{
    private NPCDirector director;
    private bool directorResolved;

    // Placement

    /// <summary>
    /// The tile this NPC belongs to. Kept separate from vanilla's
    /// <see cref="NPC.homeTileX"/>, which stays -1 so housing never claims it.
    /// </summary>
    public Point AnchorTile { get; private set; }

    public float DistanceFromAnchor => Vector2.Distance(NPC.Center, AnchorTile.ToWorldCoordinates());

    public bool IsAtAnchor
    {
        get
        {
            Point current = NPC.Center.ToTileCoordinates();
            return Math.Abs(current.X - AnchorTile.X) <= 2
                && Math.Abs(current.Y - AnchorTile.Y) <= 4;
        }
    }

    public void SetAnchorTile(Point tile) => AnchorTile = tile;

    public void SetAnchorTile(Vector2 worldPosition) => SetAnchorTile(worldPosition.ToTileCoordinates());

    // Housing

    /// <summary>
    /// The place reserved for this NPC's type, if the world has one. Reserved
    /// through <see cref="WorldNPCHousing"/>; nothing else may be housed there.
    /// A homed NPC anchors to <see cref="NPCHome.Anchor"/> when it spawns and
    /// never despawns.
    /// </summary>
    public NPCHome Home => WorldNPCHousing.TryGetHome(Type, out NPCHome home) ? home : null;

    public bool HasHome => Home != null;

    // Script

    /// <summary>
    /// Runs this NPC's behaviours. Attached on first use; null only if
    /// <see cref="UsesDirector"/> is false.
    /// </summary>
    public NPCDirector Director
    {
        get
        {
            if (!directorResolved)
            {
                director = UsesDirector && NPC.TryEnable(out NPCDirector attached) ? attached : null;
                directorResolved = true;
            }

            return director;
        }
    }

    protected virtual bool UsesDirector => true;

    /// <summary>
    /// A behaviour the director plays in place of the town AI for as long as it runs, rebuilt whenever it finishes.
    /// Null, the default, leaves the NPC to the town AI. For test rigs and set pieces.
    /// </summary>
    protected virtual IBehavior? BuildScript() => null;

    /// <summary>How far it may drift before walking home rather than resuming in place.</summary>
    protected virtual float AnchorReturnDistance => 200f;

    /// <summary>Set false to leave the director alone once its script ends.</summary>
    protected virtual bool AutoResumeScript => true;

    /// <summary>Interrupts the script with <paramref name="behavior"/>, resuming after.</summary>
    public void Interrupt(IBehavior behavior) => Director?.Interrupt(NPC, behavior);

    /// <summary>Replaces the running script outright.</summary>
    public void Play(IBehavior behavior) => Director?.Play(NPC, behavior);

    // Identity

    /// <summary>
    /// Map icon. Null falls back to the NPC's own head sprite, or to the player-model
    /// head for a <see cref="Actors.TerrarianNPC"/>.
    /// </summary>
    public virtual string MapHeadTexture => null;

    /// <summary>
    /// Leaves the map icon off until the local player has been near this NPC once in this world
    /// (<see cref="WorldNPCDiscovery"/>), so the map can't give away where a story NPC lives.
    /// </summary>
    public virtual bool HiddenOnMapUntilMet => false;

    /// <summary>
    /// Kept across world reloads. Otherwise <see cref="WorldNPCPersistenceSystem"/> removes it after load, unless it
    /// has an open job or a home. Defaults to true for fixtures (<see cref="CanDespawn"/> false).
    /// </summary>
    public virtual bool Persistent => !CanDespawn;

    /// <summary>
    /// Wanders off when nobody is looking. False for fixtures - a narrative speaker
    /// standing where the story needs it - which also makes them invulnerable.
    /// </summary>
    public virtual bool CanDespawn => true;

    /// <summary>
    /// Set while this NPC has given out work. Keeps a provider around for as long as
    /// the job it handed you is still open.
    /// </summary>
    public bool HasActiveJob { get; set; }

    // Lifecycle

    public override void SetStaticDefaults()
    {
        NPCID.Sets.ActsLikeTownNPC[Type] = true;
        NPCID.Sets.NoTownNPCHappiness[Type] = true;
        NPCID.Sets.DoesntDespawnToInactivityAndCountsNPCSlots[Type] = true;
    }

    /// <summary>
    /// Sealed in spirit - override <see cref="SetWorldNPCDefaults"/> instead.
    /// Subclasses that extend the base setup (like <see cref="Actors.TerrarianNPC"/>)
    /// call through.
    /// </summary>
    public override void SetDefaults()
    {
        InitializeInventory();

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
        // CanTalk and CanBeTalkedTo require aiStyle 7; PreAI runs the town AI in its place.
        NPC.aiStyle = NPCAIStyleID.Passive;
        NPC.npcSlots = 0f;

        // A fixture cannot be killed by a passing zombie; a wanderer can.
        NPC.immortal = !CanDespawn;
        NPC.dontTakeDamageFromHostiles = !CanDespawn;

        SetWorldNPCDefaults();
    }

    /// <summary>Stats, sounds and flags. Called at the end of <see cref="SetDefaults"/>.</summary>
    protected virtual void SetWorldNPCDefaults() { }

    /// <summary>
    /// Anchors the NPC where it actually spawned.
    ///
    /// <para>
    /// Deliberately not in <see cref="SetDefaults"/>: <see cref="NPC.NewNPC"/> calls
    /// that <em>before</em> assigning position, so the NPC's centre is still (0,0)
    /// there. Anchoring at (0,0) sends every world NPC walking toward the left edge of
    /// the world until it wedges on terrain. <see cref="LoadData"/> runs after this,
    /// so a saved anchor still wins.
    /// </para>
    /// </summary>
    public override void OnSpawn(IEntitySource source) => AnchorTile = Home?.Anchor ?? NPC.Center.ToTileCoordinates();

    public override void PostAI()
    {
        NPC.homeless = true;
        NPC.homeTileX = -1;
        NPC.homeTileY = -1;

        TickDespawn();

        if (AutoResumeScript)
            ResumeScript();

        SettleTownState();
    }

    /// <summary>True while any player has this NPC's chat window open.</summary>
    public bool IsChatting => TownChat.Talker(NPC) != null;

    /// <summary>
    /// Restarts <see cref="BuildScript"/> when the director falls idle, walking home first if the NPC has wandered too far
    /// to pick up where it is. An open chat window plays <see cref="Listen"/> instead, and the script restarts after.
    /// </summary>
    private void ResumeScript()
    {
        if (Director is not { IsBusy: false })
            return;

        IBehavior? script = BuildScript();
        if (script == null)
            return;

        if (IsChatting)
        {
            Director.Play(NPC, Behavior.Listen());
            return;
        }

        if (DistanceFromAnchor > AnchorReturnDistance)
            script = Behavior.WalkTo(AnchorTile).WithArrival(32f).Then(script);

        Director.Play(NPC, script.While(_ => !IsChatting));
    }

    /// <summary>
    /// Despawn bookkeeping. Deliberately not in <see cref="PreAI"/>: an active director
    /// vetoes the tick from its own PreAI, and tModLoader skips <see cref="ModNPC.PreAI"/>
    /// entirely once a global has done so - a timer there would freeze for as long as
    /// the NPC was busy.
    /// </summary>
    private void TickDespawn()
    {
        if (!CanDespawn || HasActiveJob || HasHome || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        timeAlive++;

        if (timeAlive < TimeConversion.SetTime(mins: 3) || IsAnyoneWatching())
            return;

        Despawn();
    }

    /// <summary>Removes the NPC without killing it (no loot, no death message), syncing the removal from a server.</summary>
    public void Despawn()
    {
        NPC.active = false;
        NPC.netSkip = -1;
        NPC.life = 0;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
    }

    public int timeAlive;

    private bool IsAnyoneWatching()
    {
        int width = NPC.sWidth + NPC.safeRangeX * 2;
        int height = NPC.sHeight + NPC.safeRangeY * 2;

        var screen = new Rectangle(
            (int)NPC.Center.X - width / 2,
            (int)NPC.Center.Y - height / 2,
            width, height);

        foreach (Player player in Main.ActivePlayers)
        {
            if (player.getRect().Intersects(screen))
                return true;
        }

        return false;
    }

    public override void ModifyHoverBoundingBox(ref Rectangle boundingBox) => boundingBox.Inflate(18, 44);

    // Housing prevention. A world NPC never takes vanilla housing, even its own
    // reserved home - the home is expressed through the anchor instead.

    public override bool CheckConditions(int left, int right, int top, int bottom) => false;

    public override bool CanGoToStatue(bool toKingStatue) => false;

    public override bool ModifyDeathMessage(ref NetworkText customText, ref Color color)
    {
        string message = $"{NPC.GivenName} was slain...";

        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), Color.Red);
        else if (Main.netMode == NetmodeID.SinglePlayer)
            Main.NewText(message, Color.Red);

        return false;
    }

    // Persistence

    public override void SaveData(TagCompound tag)
    {
        tag["anchorX"] = AnchorTile.X;
        tag["anchorY"] = AnchorTile.Y;
        tag["job"] = HasActiveJob;

        var inventory = new List<TagCompound>();
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            if (CarriedInventory[i].IsAir)
                continue;

            inventory.Add(new TagCompound { ["slot"] = i, ["item"] = ItemIO.Save(CarriedInventory[i]) });
        }

        tag["inv"] = inventory;
    }

    public override void LoadData(TagCompound tag)
    {
        if (tag.ContainsKey("anchorX") && tag.ContainsKey("anchorY"))
            AnchorTile = new Point(tag.GetInt("anchorX"), tag.GetInt("anchorY"));

        if (tag.ContainsKey("job"))
            HasActiveJob = tag.GetBool("job");

        if (!tag.ContainsKey("inv"))
            return;

        for (int i = 0; i < CarriedInventorySize; i++)
            CarriedInventory[i].TurnToAir();

        foreach (TagCompound entry in tag.GetList<TagCompound>("inv"))
        {
            int slot = entry.GetInt("slot");
            if (slot >= 0 && slot < CarriedInventorySize)
                CarriedInventory[slot] = ItemIO.Load(entry.GetCompound("item"));
        }
    }

    // Inventory

    public const int CarriedInventorySize = 10;

    /// <summary>
    /// What this NPC carries. Dropped on death, and the pool trading draws from.
    /// </summary>
    public Item[] CarriedInventory { get; private set; }

    private void InitializeInventory()
    {
        CarriedInventory = new Item[CarriedInventorySize];

        for (int i = 0; i < CarriedInventorySize; i++)
            CarriedInventory[i] = new Item();
    }

    /// <summary>Items the NPC carries but won't trade away.</summary>
    protected virtual List<Action> SetWorldInventory() =>
    [
        () => AddInvItem(ItemID.SilverCoin, Main.rand.Next(20, 80)),
    ];

    /// <summary>
    /// Adds an item, stacking into an existing entry where possible.
    /// Returns false when the inventory is full.
    /// </summary>
    public bool AddInvItem(int itemId, int stack = 1)
    {
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            Item slot = CarriedInventory[i];
            if (slot.type == itemId && slot.stack < slot.maxStack)
            {
                slot.stack = Math.Min(slot.stack + stack, slot.maxStack);
                return true;
            }
        }

        for (int i = 0; i < CarriedInventorySize; i++)
        {
            if (!CarriedInventory[i].IsAir)
                continue;

            CarriedInventory[i].SetDefaults(itemId);
            CarriedInventory[i].stack = stack;
            return true;
        }

        return false;
    }

    /// <summary>Removes up to <paramref name="amount"/> of an item. Returns how many went.</summary>
    public int TakeCarriedItem(int itemId, int amount = 1)
    {
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            Item slot = CarriedInventory[i];
            if (slot.type != itemId)
                continue;

            int taken = Math.Min(slot.stack, amount);
            slot.stack -= taken;

            if (slot.stack <= 0)
                slot.TurnToAir();

            return taken;
        }

        return 0;
    }

    public bool HasCarriedItem(int itemId, int amount = 1) => GetCarriedItemCount(itemId) >= amount;

    /// <summary>Total stack of an item across every slot.</summary>
    public int GetCarriedItemCount(int itemId)
    {
        int total = 0;

        for (int i = 0; i < CarriedInventorySize; i++)
        {
            if (CarriedInventory[i].type == itemId)
                total += CarriedInventory[i].stack;
        }

        return total;
    }

    /// <summary>Drops a percentage of everything carried, at the NPC's feet.</summary>
    public void DropInventory(int percentage = 100)
    {
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            Item item = CarriedInventory[i];
            if (item.IsAir)
                continue;

            int amount = item.stack * percentage / 100;
            if (amount <= 0)
                continue;

            Item.NewItem(NPC.GetSource_Loot(), NPC.getRect(), item.type, amount);
            item.stack -= amount;

            if (item.stack <= 0)
                item.TurnToAir();
        }
    }

    public override void OnKill() => DropInventory();

    /// <summary>Spawns one at a world position and anchors it there.</summary>
    public static T Spawn<T>(Vector2 worldPosition) where T : WorldNPC
    {
        int index = NPC.NewNPC(
            NPC.GetSource_None(),
            (int)worldPosition.X,
            (int)worldPosition.Y,
            ModContent.NPCType<T>());

        if (index >= Main.maxNPCs)
            return null;

        var spawned = Main.npc[index].ModNPC as T;
        spawned?.SetAnchorTile(worldPosition);

        return spawned;
    }
}
