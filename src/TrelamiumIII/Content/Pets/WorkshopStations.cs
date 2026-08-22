using System.Collections.Generic;

namespace TrelamiumIII.Content.Pets;

/// <summary>
///     What counts as a crafting station, worked out from scanning <see cref="ModSystem.PostAddRecipes"/>.
/// </summary>
/// <remarks>
///     Derived rather than listed: a tile is a station exactly when some recipe asks you to stand
///     next to it. That covers every modded station for free, and keeps anything that would sit in a
///     slot doing nothing out of one.
/// </remarks>
public sealed class CraftingStationRegistry : ModSystem
{
    private static readonly HashSet<int> stations = [];

    /// <summary>
    ///     Vanilla stations that stand in for a lesser one. Terraria applies these inside the pass
    ///     that scans the tiles around the player, which is a pass a carried station never goes
    ///     through, so the workshop has to apply them itself.
    /// </summary>
    private static readonly Dictionary<int, int[]> StandsInFor = new()
    {
        [TileID.MythrilAnvil] = [TileID.Anvils],
        [TileID.AdamantiteForge] = [TileID.Furnaces, TileID.Hellforge],
        [TileID.Hellforge] = [TileID.Furnaces],
        [TileID.GlassKiln] = [TileID.Furnaces],
        [TileID.AlchemyTable] = [TileID.Bottles],
    };

    public override void PostAddRecipes()
    {
        stations.Clear();

        for (int i = 0; i < Recipe.numRecipes; i++)
        {
            foreach (int tile in Main.recipe[i].requiredTile)
            {
                if (tile >= 0)
                    stations.Add(tile);
            }
        }

        // A station that stands in for another is worth carrying even if nothing asks for it
        // directly, since what it stands in for is the part that gets used.
        foreach (int tile in StandsInFor.Keys)
            stations.Add(tile);
    }

    public override void Unload() => stations.Clear();

    /// <summary>An item that places nothing. Not a tile id, so it is compared as itself.</summary>
    private const int NO_TILE = -1;

    /// <summary>Whether this item places something a recipe would want you standing next to.</summary>
    public static bool IsStation(Item item) => !item.IsAir && item.createTile > NO_TILE && stations.Contains(item.createTile);

    /// <summary>
    ///     Marks <paramref name="player" /> as standing next to <paramref name="tileType" />, and
    ///     next to everything that tile counts as.
    /// </summary>
    /// <param name="record">
    ///     Collects every tile actually switched on. The caller has to be able to switch exactly
    ///     these back off again, and the equivalences mean that is not just the tile passed in.
    /// </param>
    public static void Provide(Player player, int tileType, HashSet<int> record)
    {
        if (tileType <= NO_TILE || tileType >= TileLoader.TileCount)
            return;

        Set(player, tileType, record);

        if (StandsInFor.TryGetValue(tileType, out int[]? vanillaEquivalents))
        {
            foreach (int equivalent in vanillaEquivalents)
                Set(player, equivalent, record);
        }

        // A modded station only stands in for a vanilla one through its own AdjTiles list, and
        // nothing walks that list for a tile the player is not actually stood next to.
        if (TileLoader.GetTile(tileType) is { AdjTiles: { Length: > 0 } moddedEquivalents })
        {
            foreach (int equivalent in moddedEquivalents)
                Set(player, equivalent, record);
        }

        // Carries the duplication chance, which is a separate flag from the crafting adjacency.
        if (tileType == TileID.AlchemyTable)
            player.alchemyTable = true;
    }

    private static void Set(Player player, int tileType, HashSet<int> record)
    {
        if (tileType <= NO_TILE || tileType >= TileLoader.TileCount)
            return;

        player.adjTile[tileType] = true;
        record.Add(tileType);
    }
}

/// <summary>
///     Tracks the player's workshop and hands its contents to the crafting system.
/// </summary>
public sealed class WorkshopPlayer : ModPlayer
{
    /// <summary>
    ///     The workshop driving the pet and the panel, or null. Found once a frame rather than
    ///     stored, so dropping it or handing it over takes effect immediately.
    /// </summary>
    public Item? ActiveWorkshop { get; private set; }

    public override void ResetEffects() => ActiveWorkshop = FindWorkshop();

    public override void PostUpdate() => KeepStationPets();

    /// <summary>
    ///     Puts a pet on the ground for every station in the workshop.
    /// </summary>
    /// <remarks>
    ///     Only spawns. Each pet works out for itself when it has been swapped out of its slot or
    ///     lost its apparition and goes quietly, which means a slot changing needs no bookkeeping
    ///     here and a pet that somehow outlives its cause still cleans itself up.
    /// </remarks>
    private void KeepStationPets()
    {
        if (Main.myPlayer != Player.whoAmI)
            return;

        if (ActiveWorkshop?.ModItem is not PossessedWorkshop workshop)
            return;

        if (!PossessedWorkshopPet.TryGetPet(Player, out _))
            return;

        int type = ModContent.ProjectileType<WorkshopStationPet>();

        // Swept once into a slot map rather than searched per slot: five stations asking the same
        // question every tick is five full passes over the projectile array for one answer.
        Span<bool> covered = stackalloc bool[PossessedWorkshop.SLOT_COUNT];

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (!projectile.active || projectile.type != type || projectile.owner != Player.whoAmI)
                continue;

            int slot = (int)projectile.ai[1];

            // A pet whose slot now holds something else is stale, and stale pets retire themselves.
            if (slot >= 0 && slot < covered.Length && (int)projectile.ai[0] == workshop.Stations[slot].type)
                covered[slot] = true;
        }

        for (int slot = 0; slot < workshop.Stations.Length && slot < covered.Length; slot++)
        {
            Item station = workshop.Stations[slot];

            if (station.IsAir || covered[slot])
                continue;

            Projectile.NewProjectile(
                Player.GetSource_Misc("PossessedWorkshop"),
                Player.Center,
                Vector2.Zero,
                type,
                0,
                0f,
                Player.whoAmI,
                ai0: station.type,
                ai1: slot
            );
        }
    }

    /// <summary>
    ///     The cursor tell and the right-click that opens the panel.
    /// </summary>
    /// <remarks>
    ///     Handled from the player rather than from the pet's own AI, because
    ///     <see cref="Player.mouseInterface" /> and <see cref="Player.noThrow" /> have to be set
    ///     before the same update reaches the item check that reads them. Projectiles update after
    ///     every player has already been through it, so a pet setting these for itself would suppress
    ///     the swing one frame too late and let the first right-click through.
    ///     <para>Local only: a panel belongs to whoever is looking at it.</para>
    /// </remarks>
    public override void PreUpdate()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;

        if (ActiveWorkshop?.ModItem is not PossessedWorkshop || !PossessedWorkshopPet.TryGetPet(Player, out Projectile? pet))
            return;

        // An open panel is drawn over its own pet, so without this every click on a slot would land
        // on the workshop underneath as well and shut the thing being clicked.
        if (PossessedWorkshopUI.Open && PossessedWorkshopUI.MouseOverPanel)
            return;

        bool hovering = pet.Hitbox.Contains(Main.MouseWorld.ToPoint());

        if (hovering)
        {
            Player.noThrow = 2;
            Player.cursorItemIconEnabled = true;
            Player.cursorItemIconID = ModContent.ItemType<PossessedWorkshop>();
            //Player.mouseInterface = true;
        }

        if (!Main.mouseRight || !Main.mouseRightRelease)
            return;

        if (hovering)
            PossessedWorkshopUI.Toggle(pet);
        else if (PossessedWorkshopUI.Open)
            PossessedWorkshopUI.Close();
    }

    private Item? FindWorkshop()
    {
        Item[] inventory = Player.inventory;

        for (int i = 0; i < inventory.Length; i++)
        {
            if (inventory[i].ModItem is PossessedWorkshop)
                return inventory[i];
        }

        return null;
    }

    /// <summary>Tiles the workshop switched on the last time it was asked.</summary>
    private readonly HashSet<int> provided = [];

    private readonly HashSet<int> previouslyProvided = [];

    /// <summary>Slots close enough to count, kept so range can be sticky rather than a hard edge.</summary>
    private readonly HashSet<int> inReach = [];

    /// <summary>
    ///     Switches the workshop's stations back off, exactly the ones it switched on.
    /// </summary>
    public void ClearStations()
    {
        foreach (int tile in provided)
        {
            if (tile < TileLoader.TileCount)
                Player.adjTile[tile] = false;
        }
    }

    /// <summary>
    ///     Marks the player as standing next to every station whose pet has caught up with them.
    /// </summary>
    /// <remarks>
    ///     Measured against each station's own pet rather than against the apparition, so being able
    ///     to use a furnace means the furnace is actually stood next to you. A station whose pet is
    ///     off hopping over a hill does not count until it arrives.
    /// </remarks>
    public void ApplyStations()
    {
        previouslyProvided.Clear();

        foreach (int tile in provided)
            previouslyProvided.Add(tile);

        provided.Clear();

        if (ActiveWorkshop?.ModItem is not PossessedWorkshop workshop)
        {
            inReach.Clear();

            return;
        }

        int type = ModContent.ProjectileType<WorkshopStationPet>();

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile pet = Main.projectile[i];

            if (!pet.active || pet.type != type || pet.owner != Player.whoAmI)
                continue;

            int slot = (int)pet.ai[1];

            if (slot < 0 || slot >= workshop.Stations.Length)
                continue;

            Item station = workshop.Stations[slot];

            // A pet whose slot has already been swapped is on its way out and does not count.
            if (station.IsAir || station.type != (int)pet.ai[0])
                continue;

            // Sticky: harder to lose than to gain. A station bouncing along beside a running player
            // would otherwise cross a single hard edge over and over, and every crossing changes the
            // recipe list and throws away whatever the player had selected.
            float limit = inReach.Contains(slot)
                ? WorkshopStationPet.STATION_RANGE_KEEP
                : WorkshopStationPet.STATION_RANGE;

            if (Player.Distance(pet.Center) > limit)
            {
                inReach.Remove(slot);

                continue;
            }
            inReach.Add(slot);

            CraftingStationRegistry.Provide(Player, station.createTile, provided);
        }
    }

    /// <summary>Whether the set of stations on offer is different from the last time it was built.</summary>
    public bool StationsChanged() => !provided.SetEquals(previouslyProvided);
}

/// <summary>
///     Splices the workshop's stations into the pass that decides what the player is stood next to.
/// </summary>
/// <remarks>
///     <para>
///         That pass snapshots last frame's adjacency, clears it, rescans the tiles physically
///         around the player, and rebuilds the recipe list if the two differ. A carried station is
///         not a tile it can find, so simply writing the stations afterwards makes them look, to
///         that comparison, like they vanish and reappear on every single frame. It rebuilds the
///         recipe list constantly, from a version that does not contain them.
///     </para>
///     <para>
///         Which is worse than it sounds, because rebuilding resets the player's selected recipe to
///         the top of the list and can only put it back if the recipe they had picked is still in
///         it. Scrolling onto a station's recipe would immediately throw the selection back to the
///         last one the surrounding tiles could make.
///     </para>
///     <para>
///         So the stations are lifted out before the pass and put back after it. The pass then sees
///         a world without them on both sides of its comparison, stays quiet, and the recipe list is
///         rebuilt here instead, only when the set of stations genuinely changed.
///     </para>
/// </remarks>
public sealed class WorkshopAdjacencySystem : ModSystem
{
    public override void Load() => On_Player.AdjTiles += SpliceStations;

    public override void Unload() => On_Player.AdjTiles -= SpliceStations;

    private static void SpliceStations(On_Player.orig_AdjTiles orig, Player self)
    {
        WorkshopPlayer workshop = self.GetModPlayer<WorkshopPlayer>();

        workshop.ClearStations();

        orig(self);

        workshop.ApplyStations();

        if (workshop.StationsChanged() && self.whoAmI == Main.myPlayer)
            Recipe.FindRecipes();
    }
}
