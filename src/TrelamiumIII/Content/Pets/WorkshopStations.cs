using System.Collections.Generic;

namespace TrelamiumIII.Content.Pets;

/// <summary>
///     Crafting stations, derived in <see cref="ModSystem.PostAddRecipes"/>: a tile is one exactly when some recipe requires it.
/// </summary>
public sealed class CraftingStationRegistry : ModSystem
{
    private static readonly HashSet<int> stations = [];

    /// <summary>
    ///     Vanilla stations that stand in for lesser ones. Vanilla applies these only when scanning nearby tiles, so the workshop does it itself.
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
            int tile = Main.recipe[i].requiredTile;

            if (tile >= 0)
                stations.Add(tile);
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

    /// <summary>Marks <paramref name="player" /> as next to <paramref name="tileType" /> and everything it counts as.</summary>
    /// <param name="record">Collects every tile switched on, so the caller can switch exactly those back off.</param>
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

/// <summary>Tracks the player's workshop and hands its contents to the crafting system.</summary>
public sealed class WorkshopPlayer : ModPlayer
{
    /// <summary>The workshop driving the pet and panel, or null. Found each frame, so dropping it applies immediately.</summary>
    public Item? ActiveWorkshop { get; private set; }

    public override void ResetEffects() => ActiveWorkshop = FindWorkshop();

    public override void PostUpdate() => KeepStationPets();

    /// <summary>Spawns a pet for every station; pets retire themselves when swapped out or orphaned.</summary>
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
    ///     Cursor tell and right-click to open the panel. Handled here rather than in the pet's AI, because <see cref="Player.mouseInterface" />
    ///     and <see cref="Player.noThrow" /> must be set before this update's item check. Local only.
    /// </summary>
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

    /// <summary>Switches off exactly the stations the workshop switched on.</summary>
    public void ClearStations()
    {
        foreach (int tile in provided)
        {
            if (tile < TileLoader.TileCount)
                Player.adjTile[tile] = false;
        }
    }

    /// <summary>Marks the player as next to every station whose own pet has caught up with them.</summary>
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

            // Sticky range: a single hard edge would flicker and reset the selected recipe.
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
///     Lifts the workshop's stations out before vanilla's adjacency pass and restores them after it. 1.4.5 rebuilds the recipe
///     list every inventory frame, so a station change needs no refresh here.
/// </summary>
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
    }
}
