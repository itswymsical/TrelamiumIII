using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MonoMod.Cil;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Residues.Bodies;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues;

/// <summary>Where a call to lay a mark comes from, which decides who is in charge of the mark.</summary>
/// <remarks>
///     Marks have stats and collision and affect anything that touches them, so a server is in charge of all but
///     <see cref="Local" /> ones: it runs their reactions and spreading, and clients take its word for what's on the ground.
/// </remarks>
public enum ResidueOrigin : byte
{
    /// <summary>
    ///     Only this machine knows it happened: a player's wand, a hit only its owner's client runs, gas whose owner decides
    ///     where it pools. A client lays it at once and tells the server, which takes it from there.
    /// </summary>
    Owned,

    /// <summary>
    ///     Every machine runs the code that asked: a projectile landing, an NPC's AI, a reaction. Only a server's call counts,
    ///     and a client's does nothing, because it's about to hear the result.
    /// </summary>
    Shared,

    /// <summary>
    ///     Each machine lays its own and nothing is sent: a rule deciding from world state, which lands the same everywhere,
    ///     or a weather particle that only exists on one client.
    /// </summary>
    Local,
}

/// <summary>
///     Owns every <see cref="IResidue" />, the batch of tiles each one is laid on, and the
///     passes that draw them.
/// </summary>
/// <remarks>
///     <para>
///         Storage is one flat dictionary per residue rather than a chunk tree: contact testing is
///         an O(1) coordinate lookup, and drawing walks the batch once and culls, which a chunk index
///         would only help past the <see cref="IResidue.MaxTiles" /> ceiling.
///     </para>
///     <para>
///         In multiplayer every machine still ticks the marks it has, which keeps them moving between packets and costs
///         nothing to send, but only a server decides what's new or gone. Changes go out in one batch per residue at the end
///         of the tick. See <see cref="ResidueOrigin" />.
///     </para>
/// </remarks>
public sealed class ResidueSystem : ModSystem
{
    /// <summary>Pixels the hitbox is grown by before it is converted to tiles.</summary>
    private const int CONTACT_MARGIN = 2;

    /// <summary>One tick in eight, per tile.</summary>
    private const int LIQUID_PROBE_MASK = 7;

    /// <summary>Tiles the fallback terrain mask extends past the canvases: the kernel plus a lip.</summary>
    private const int MASK_PADDING = 3;

    private sealed class ResidueState(IResidue residue, int id)
    {
        public readonly IResidue Residue = residue;

        /// <summary>Index into <see cref="states" />, and the residue's identity on hte wire.</summary>
        public readonly int Id = id;

        public readonly Dictionary<Point, ResidueInstance> Tiles = [];

        /// <summary>Insertion order</summary>
        public readonly Queue<(Point Position, int Serial)> Order = new();

        /// <summary>Scratch list of tiles that ended this tick. Reused and never read outside the tick.</summary>
        public readonly List<Point> Finished = [];

        /// <summary>Null on a server and for residues that draw nothing.</summary>
        public TileCanvas? Canvas;

        public ResidueCanvasSource? CanvasSource;

        public int NextSerial;

        /// <summary>Marks to tell the other machines about at the end of the tick.</summary>
        public readonly HashSet<Point> Changed = [];

        /// <summary>Marks a server has to tell the clients are gone at the end of the tick.</summary>
        public readonly HashSet<Point> Gone = [];
    }

    /// <summary>Canvases drawn from other sources, composited over every residue's.</summary>
    private static readonly List<TileCanvas> overlays = [];

    private enum PendingKind : byte
    {
        Apply,
        Remove,
    }

    private readonly record struct PendingOp(PendingKind Kind, int Id, int X, int Y, float Intensity, float Duration, int Owner, ResidueOrigin Origin, byte Variant, ChemicalPayload? Payload, Color? Tint);

    private static readonly List<ResidueState> states = [];

    private static readonly Dictionary<Type, ResidueState> byType = [];

    /// <summary>
    ///     Applications and removals asked for from inside a hook, held until the tick is over.
    /// </summary>
    private static readonly List<PendingOp> pending = [];

    private static bool dispatching;

    private static bool loaded;

    /// <summary>The last <see cref="ResidueInstance.Arrival" /> handed out.</summary>
    private static long lastArrival;

    /// <summary>Whether the composite was spliced into the solid tile pass. See <see cref="SpliceComposite" />.</summary>
    private static bool compositeSpliced;

    /// <summary>The registered residue of type <typeparamref name="T" />, or null before load.</summary>
    public static T? Get<T>() where T : class, IResidue
        => byType.TryGetValue(typeof(T), out ResidueState? state) ? (T)state.Residue : null;

    /// <summary>The registered residue of <paramref name="type" />, or null before load or for a type that is not one.</summary>
    public static IResidue? Get(Type type)
        => byType.TryGetValue(type, out ResidueState? state) ? state.Residue : null;

    /// <summary>Where <paramref name="residue" /> falls in wire order, the same on every machine.</summary>
    internal static int IdOf(IResidue residue) => byType[residue.GetType()].Id;

    /// <summary>The residue at <paramref name="id" /> in wire order, or null for one that isn't there.</summary>
    internal static IResidue? ById(int id) => id >= 0 && id < states.Count ? states[id].Residue : null;

    /// <summary>Every registered residue, in wire order. For tooling that enumerates them.</summary>
    public static IEnumerable<IResidue> All => states.Select(static state => state.Residue);

    /// <summary>
    ///     Lays <typeparamref name="T" /> on the tile at (<paramref name="i" />, <paramref name="j" />).
    /// </summary>
    /// <param name="intensity">How hard it took, 0..1. Clamped.</param>
    /// <param name="duration">Ticks to last, or a negative value to take the residue's own.</param>
    /// <param name="owner">
    ///     The player responsible for the mark, or <see cref="ResidueInstance.NoOwner" />.
    ///     Residues that treat their author differently from everyone else read it back off the
    ///     instance.
    /// </param>
    /// <param name="origin">Where this call comes from, which decides who is in charge of the mark. See <see cref="ResidueOrigin" />.</param>
    /// <param name="variant">
    ///     Which flavour of the residue to apply, for one that has more than one set of rules.
    ///     Meaningless to a residue that does not read
    ///     <see cref="ResidueInstance.Variant" />, which is most of them.
    /// </param>
    /// <param name="payload">The chemical release responsible, kept on the instance. Not synced.</param>
    /// <param name="tint">
    ///     This tile's own colour, or null for the residue's palette. See
    ///     <see cref="ResidueInstance.Tint" />.
    /// </param>
    /// <returns>
    ///     Whether the application was accepted. A deferred one returns true on the promise, since
    ///     the residue has not been asked yet.
    /// </returns>
    public static bool Apply<T>(int i, int j, float intensity = 1f, float duration = -1f, int owner = ResidueInstance.NoOwner, ResidueOrigin origin = ResidueOrigin.Owned, byte variant = 0, ChemicalPayload? payload = null, Color? tint = null) where T : class, IResidue
        => byType.TryGetValue(typeof(T), out ResidueState? state) && Apply(state, i, j, intensity, duration, owner, origin, variant, payload, tint);

    /// <inheritdoc cref="Apply{T}" />
    public static bool Apply(IResidue residue, int i, int j, float intensity = 1f, float duration = -1f, int owner = ResidueInstance.NoOwner, ResidueOrigin origin = ResidueOrigin.Owned, byte variant = 0, ChemicalPayload? payload = null, Color? tint = null)
        => byType.TryGetValue(residue.GetType(), out ResidueState? state) && Apply(state, i, j, intensity, duration, owner, origin, variant, payload, tint);

    /// <summary>
    ///     Lays <typeparamref name="T" /> on every tile within <paramref name="radius" /> pixels of
    ///     <paramref name="center" />, thinning from <paramref name="intensity" /> at the middle to
    ///     <paramref name="edge" /> times that at the rim.
    /// </summary>
    /// <remarks>
    ///     <see cref="ResidueOrigin.Shared" /> unless asked, as an area is nearly always laid by something every machine
    ///     simulates: a projectile bursting, an NPC landing.
    /// </remarks>
    /// <param name="edge">Intensity at the rim as a fraction of the centre's, 0..1.</param>
    /// <returns>Tiles that took it.</returns>
    public static int ApplyCircle<T>(Vector2 center, float radius, float intensity = 1f, float edge = 0.45f, float duration = -1f, int owner = ResidueInstance.NoOwner, ResidueOrigin origin = ResidueOrigin.Shared, byte variant = 0, Color? tint = null) where T : class, IResidue
        => byType.TryGetValue(typeof(T), out ResidueState? state) ? ApplyCircle(state, center, radius, intensity, edge, duration, owner, origin, variant, tint) : 0;

    /// <inheritdoc cref="ApplyCircle{T}" />
    public static int ApplyCircle(IResidue residue, Vector2 center, float radius, float intensity = 1f, float edge = 0.45f, float duration = -1f, int owner = ResidueInstance.NoOwner, ResidueOrigin origin = ResidueOrigin.Shared, byte variant = 0, Color? tint = null)
        => byType.TryGetValue(residue.GetType(), out ResidueState? state) ? ApplyCircle(state, center, radius, intensity, edge, duration, owner, origin, variant, tint) : 0;

    private static int ApplyCircle(ResidueState state, Vector2 center, float radius, float intensity, float edge, float duration, int owner, ResidueOrigin origin, byte variant, Color? tint)
    {
        int reach = (int)MathF.Ceiling(radius / 16f);
        int centerX = (int)(center.X / 16f);
        int centerY = (int)(center.Y / 16f);
        int applied = 0;

        for (int x = centerX - reach; x <= centerX + reach; x++)
        {
            for (int y = centerY - reach; y <= centerY + reach; y++)
            {
                float distance = Vector2.Distance(new Vector2(x * 16f + 8f, y * 16f + 8f), center);

                if (distance > radius)
                    continue;

                float falloff = 1f - distance / MathF.Max(radius, 1f);

                if (Apply(state, x, y, intensity * MathHelper.Lerp(edge, 1f, falloff), duration, owner, origin, variant, null, tint))
                    applied++;
            }
        }

        return applied;
    }

    public static bool Remove<T>(int i, int j) where T : class, IResidue
    {
        if (!byType.TryGetValue(typeof(T), out ResidueState? state))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Remove, state.Id, i, j, 0f, 0f, ResidueInstance.NoOwner, ResidueOrigin.Shared, 0, null, null));
            return true;
        }

        return Remove(state, new Point(i, j));
    }

    /// <inheritdoc cref="Remove{T}" />
    public static bool Remove(IResidue residue, int i, int j)
    {
        if (!byType.TryGetValue(residue.GetType(), out ResidueState? state))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Remove, state.Id, i, j, 0f, 0f, ResidueInstance.NoOwner, ResidueOrigin.Shared, 0, null, null));
            return true;
        }

        return Remove(state, new Point(i, j));
    }

    public static bool Has<T>(int i, int j) where T : class, IResidue
        => byType.TryGetValue(typeof(T), out ResidueState? state) && state.Tiles.ContainsKey(new Point(i, j));

    /// <summary>Whether any mark on the tile counts as <paramref name="element" /> right now.</summary>
    public static bool HasElement(int i, int j, ResidueElement element)
    {
        var position = new Point(i, j);

        foreach (ResidueState state in states)
        {
            if (state.Tiles.TryGetValue(position, out ResidueInstance instance) && (state.Residue.ElementsOf(in instance) & element) != 0)
                return true;
        }

        return false;
    }

    /// <summary>Takes <paramref name="share" /> of what a mark has left. See <see cref="IResidue.Diminish" />. Not for use from inside a residue's hook.</summary>
    public static void Diminish(IResidue residue, int i, int j, float share)
    {
        if (!byType.TryGetValue(residue.GetType(), out ResidueState? state))
            return;

        ref ResidueInstance instance = ref CollectionsMarshal.GetValueRefOrNullRef(state.Tiles, new Point(i, j));

        if (Unsafe.IsNullRef(ref instance))
            return;

        residue.Diminish(ref instance, MathHelper.Clamp(share, 0f, 1f));

        if (Main.netMode == NetmodeID.Server)
        {
            instance.SharedAt = Main.GameUpdateCount;
            state.Changed.Add(new Point(i, j));
        }
    }

    /// <summary>
    ///     Moves a mark up to <paramref name="arrival" /> in the tile's order, for one that is the same stuff in a new form:
    ///     water that was frost a moment ago was there as long as the frost was. Never moves a mark later than it is.
    /// </summary>
    public static void TakePlace<T>(int i, int j, long arrival) where T : class, IResidue
    {
        if (!byType.TryGetValue(typeof(T), out ResidueState? state))
            return;

        ref ResidueInstance instance = ref CollectionsMarshal.GetValueRefOrNullRef(state.Tiles, new Point(i, j));

        if (!Unsafe.IsNullRef(ref instance))
            instance.Arrival = Math.Min(instance.Arrival, arrival);
    }

    /// <summary>Fills <paramref name="into" /> with the marks on the tile that have an element and any of it left, oldest first.</summary>
    internal static void CollectReactants(Point tile, List<ResidueReactant> into)
    {
        into.Clear();

        foreach (ResidueState state in states)
        {
            if (!state.Tiles.TryGetValue(tile, out ResidueInstance instance))
                continue;

            ResidueElement elements = state.Residue.ElementsOf(in instance);
            float potency = state.Residue.PotencyOf(in instance);

            if (elements != ResidueElement.None && potency > 0f)
                into.Add(new ResidueReactant(state.Residue, instance, elements, potency));
        }

        into.Sort(static (a, b) => a.Instance.Arrival.CompareTo(b.Instance.Arrival));
    }

    public static bool TryGet<T>(int i, int j, out ResidueInstance instance) where T : class, IResidue
    {
        instance = default;

        return byType.TryGetValue(typeof(T), out ResidueState? state) && state.Tiles.TryGetValue(new Point(i, j), out instance);
    }

    /// <summary>Tiles currently carrying <typeparamref name="T" />.</summary>
    public static int Count<T>() where T : class, IResidue
        => byType.TryGetValue(typeof(T), out ResidueState? state) ? state.Tiles.Count : 0;

    public static void ClearAll()
    {
        foreach (ResidueState state in states)
        {
            state.Tiles.Clear();
            state.Order.Clear();
            state.Finished.Clear();
            state.Canvas?.ClearGhosts();
        }

        pending.Clear();
        ResidueReactionSystem.Clear();
        Fuses.Clear();
    }

    public override void OnModLoad()
    {
        IEnumerable<Type> discovered = Mod.Code.GetTypes()
            .Where(static type => !type.IsAbstract && !type.IsInterface && typeof(IResidue).IsAssignableFrom(type))
            // Ordered by name, not by reflection order: the index doubles as the identity on the
            // wire, and two clients have to agree on it.
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);

        foreach (Type type in discovered)
        {
            var residue = (IResidue)Activator.CreateInstance(type)!;
            var state = new ResidueState(residue, states.Count);

            residue.Load();

            if (!Main.dedServ && residue.RenderMode == ResidueRenderMode.Canvas)
            {
                state.CanvasSource = new ResidueCanvasSource(residue, state.Tiles);
                state.Canvas = new TileCanvas(state.CanvasSource);
            }

            states.Add(state);
            byType[type] = state;
        }

        loaded = true;

        if (Main.dedServ)
            return;

        On_Main.CheckMonoliths += static orig =>
        {
            orig();
            BuildCanvases();
        };

        IL_Main.DoDraw_Tiles_Solid += SpliceComposite;

        // Without the splice, draw once the pass is over: above the solid layer's tile entities, still under NPCs.
        if (!compositeSpliced)
        {
            On_Main.DoDraw_Tiles_Solid += static (orig, self) =>
            {
                orig(self);
                CompositeCanvases();
            };
        }
    }

    /// <summary>
    ///     Calls <see cref="CompositeCanvases" /> between the solid tile blit and the tile entities drawn over it.
    /// </summary>
    /// <remarks>
    ///     Spliced into the IL rather than hooked on <c>DrawTileEntities</c>: that is a one-line forwarder, and the copy
    ///     of this method compiled for its other hooks inlines it, so a hook on it never runs from here.
    /// </remarks>
    private void SpliceComposite(ILContext il)
    {
        ILCursor c = new(il);

        if (!c.TryGotoNext(MoveType.Before, i => i.MatchCall<Main>("DrawTileEntities")))
        {
            Mod.Logger.Warn("Could not find DrawTileEntities in Main.DoDraw_Tiles_Solid; residues composite after the solid tile pass.");
            return;
        }

        // The call's arguments are already on the stack. This takes none of them and leaves them where they are.
        c.EmitDelegate(CompositeCanvases);
        compositeSpliced = true;
    }

    public override void OnModUnload()
    {
        loaded = false;

        foreach (ResidueState state in states)
        {
            state.Residue.Unload();
            state.Canvas?.Free();
            state.Canvas = null;
        }

        foreach (TileCanvas overlay in overlays)
            overlay.Free();

        states.Clear();
        byType.Clear();
        pending.Clear();
        overlays.Clear();

        compositeSpliced = false;
        reportedFailure = false;

        TerrainMask.Unload();
    }

    public override void ClearWorld() => ClearAll();

    public override void PostUpdateEverything()
    {
        if (!loaded || Main.gameMenu)
            return;

        dispatching = true;

        foreach (ResidueState state in states)
        {
            (state.Residue as Residue)?.ResetContacts();
            state.Residue.UpdateGlobal();
            UpdateBatch(state);
        }

        SweepContacts();

        dispatching = false;

        DrainPending();

        // Reactions change the world for everyone, so the server runs them and says what came of it.
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            ResidueReactionSystem.Resolve();
            Fuses.Update();
        }

        FlushNetwork();

        // After the drain, so a tile marked again this tick takes over from its own fading sample.
        foreach (ResidueState state in states)
            state.Canvas?.Update();

        foreach (TileCanvas overlay in overlays)
            overlay.Update();
    }

    /// <summary>Builds and composites a canvas for <paramref name="source" /> alongside the residues'. Client only.</summary>
    internal static TileCanvas? AddOverlay(ITileCanvasSource source)
    {
        if (Main.dedServ)
            return null;

        var canvas = new TileCanvas(source);
        overlays.Add(canvas);

        return canvas;
    }

    private static bool Apply(ResidueState state, int i, int j, float intensity, float duration, int owner, ResidueOrigin origin, byte variant, ChemicalPayload? payload, Color? tint)
    {
        // The server is running this same code, and will say what came of it.
        if (origin == ResidueOrigin.Shared && Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        if (!WorldGen.InWorld(i, j, 1))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Apply, state.Id, i, j, intensity, duration, owner, origin, variant, payload, tint));
            return true;
        }

        if (!state.Residue.CanApplyTo(i, j))
            return false;

        float life = duration > 0f ? duration : state.Residue.Duration;

        if (life <= 0f)
            return false;

        var position = new Point(i, j);
        bool exists = state.Tiles.TryGetValue(position, out ResidueInstance existing);

        var instance = new ResidueInstance
        {
            X = i,
            Y = j,
            Life = life,
            MaxLife = life,
            Intensity = MathHelper.Clamp(intensity, 0f, 1f),
            Owner = owner,
            Payload = payload,
            Variant = variant,
            Tint = tint ?? default,

            // Re-application refreshes the slot rather than replacing it, so the entry the eviction
            // queue is already holding stays the one that matches.
            Serial = exists ? existing.Serial : ++state.NextSerial,

            // Content state survives a refresh, so a residue can treat a second application as
            // adding to what is already there rather than as starting over.
            Level = exists ? existing.Level : 0f,
            Stage = exists ? existing.Stage : (byte)0,
            TotalAge = exists ? existing.TotalAge : 0,
            Arrival = exists ? existing.Arrival : ++lastArrival,

            IsShared = origin != ResidueOrigin.Local || (exists && existing.IsShared),
            SharedAt = exists ? existing.SharedAt : 0,
        };

        state.Residue.OnApplied(ref instance);

        // A tile refreshed over and over only says so now and then, and a mark that's new or has changed kind goes out right away.
        if (origin != ResidueOrigin.Local && Main.netMode != NetmodeID.SinglePlayer && (!exists || existing.Variant != variant || Main.GameUpdateCount - instance.SharedAt >= RESHARE_TICKS))
        {
            instance.SharedAt = Main.GameUpdateCount;
            state.Changed.Add(position);
            state.Gone.Remove(position);
        }

        state.Tiles[position] = instance;

        if (Main.netMode != NetmodeID.MultiplayerClient && state.Residue.ElementsOf(in instance) != ResidueElement.None)
            ResidueReactionSystem.Touch(position);

        if (!exists)
        {
            state.Order.Enqueue((position, instance.Serial));
            TrimToBudget(state);
        }

        return true;
    }

    /// <param name="announce">Whether a server tells the clients even about a mark each machine lays for itself.</param>
    private static bool Remove(ResidueState state, Point position, bool announce = true)
    {
        if (!state.Tiles.Remove(position, out ResidueInstance instance))
            return false;

        if (state.Canvas is { } canvas)
            state.CanvasSource?.OnRemoved(canvas, in instance);
        state.Residue.OnExpired(in instance);

        if (Main.netMode == NetmodeID.Server && (announce || instance.IsShared))
        {
            state.Gone.Add(position);
            state.Changed.Remove(position);
        }

        return true;
    }

    /// <summary>Drops the oldest tiles until the batch is back inside its ceiling.</summary>
    private static void TrimToBudget(ResidueState state)
    {
        int max = Math.Max(state.Residue.MaxTiles, 1);

        while (state.Tiles.Count > max && state.Order.Count > 0)
        {
            (Point position, int serial) = state.Order.Dequeue();

            // A stale entry: the tile it named has since expired, and anything sitting there now is
            // a different application that has its own place in the queue.
            if (state.Tiles.TryGetValue(position, out ResidueInstance instance) && instance.Serial == serial)
                Remove(state, position, announce: false);
        }
    }

    private static void UpdateBatch(ResidueState state)
    {
        if (state.Tiles.Count == 0)
            return;

        state.Finished.Clear();

        foreach (Point position in state.Tiles.Keys)
        {
            // By reference: the hooks below mutate the instance, and a copy-then-store round trip
            // for every tile every tick is the cost this system is most exposed to. Safe against the
            // hooks because they cannot structurally modify the batch: see the pending queue.
            ref ResidueInstance instance = ref CollectionsMarshal.GetValueRefOrNullRef(state.Tiles, position);

            instance.Age++;
            instance.TotalAge++;
            instance.Life--;

            if (((instance.Age + position.X * 7 + position.Y * 13) & LIQUID_PROBE_MASK) == 0)
                ProbeLiquid(state.Residue, ref instance);

            state.Residue.UpdateTile(ref instance);

            if (instance.Life <= 0f || !state.Residue.CanRemain(in instance))
                state.Finished.Add(position);
        }

        foreach (Point position in state.Finished)
            Remove(state, position, announce: false);
    }

    /// <summary>
    ///     Reports the wettest of the tile and its four neighbours, if any of them holds liquid.
    /// </summary>
    private static void ProbeLiquid(IResidue residue, ref ResidueInstance instance)
    {
        int bestAmount = 0;
        int bestType = LiquidID.Water;

        Consider(instance.X, instance.Y);
        Consider(instance.X - 1, instance.Y);
        Consider(instance.X + 1, instance.Y);
        Consider(instance.X, instance.Y - 1);
        Consider(instance.X, instance.Y + 1);

        if (bestAmount > 0)
            residue.OnLiquidContact(ref instance, bestType, bestAmount);

        void Consider(int x, int y)
        {
            if (!WorldGen.InWorld(x, y, 1))
                return;

            Tile tile = Framing.GetTileSafely(x, y);

            if (tile.LiquidAmount <= bestAmount)
                return;

            bestAmount = tile.LiquidAmount;
            bestType = tile.LiquidType;
        }
    }

    private static void SweepContacts()
    {
        int total = 0;

        foreach (ResidueState state in states)
            total += state.Tiles.Count;

        // Nothing is marked, so nothing can be touched. Worth checking first: without it this walks
        // every player, NPC and projectile in the world on every tick of a game where no residue
        // has ever been applied.
        if (total == 0)
            return;

        // Only its own machine says what touches a player. Debuffs and movement are its client's to decide, and the rest
        // hear about them from vanilla. A server has no player of its own, and this is an empty slot there.
        Player local = Main.LocalPlayer;

        if (local.active && !local.dead)
            SweepEntity(local);

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc.active)
                SweepEntity(npc);
        }

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (projectile.active)
                SweepEntity(projectile);
        }
    }

    private static void SweepEntity(Entity entity)
    {
        Rectangle box = entity.Hitbox;
        box.Inflate(CONTACT_MARGIN, CONTACT_MARGIN);

        int left = Math.Max(box.Left / 16, 0);
        int top = Math.Max(box.Top / 16, 0);
        int right = Math.Min(box.Right / 16, Main.maxTilesX - 1);
        int bottom = Math.Min(box.Bottom / 16, Main.maxTilesY - 1);

        for (int x = left; x <= right; x++)
        {
            for (int y = top; y <= bottom; y++)
            {
                var position = new Point(x, y);

                foreach (ResidueState state in states)
                {
                    if (state.Tiles.Count == 0)
                        continue;

                    ref ResidueInstance instance = ref CollectionsMarshal.GetValueRefOrNullRef(state.Tiles, position);

                    if (Unsafe.IsNullRef(ref instance))
                        continue;

                    switch (entity)
                    {
                        case Player player:
                            state.Residue.OnPlayerContact(ref instance, player);
                            break;

                        case NPC npc:
                            state.Residue.OnNPCContact(ref instance, npc);
                            break;

                        case Projectile projectile:
                            state.Residue.OnProjectileContact(ref instance, projectile);
                            break;
                    }

                    if (!Main.dedServ && entity is not Projectile && state.Residue.BodyPickup(in instance, out float amount) is { } picked)
                        BodyCoatNet.TouchTile(entity, picked, position, amount, instance.Tint);
                }
            }
        }
    }

    private static void DrainPending()
    {
        // Indexed rather than foreach: nothing added here can queue more work, because the flag that
        // defers is already down, but an index is honest about that where an enumerator would throw.
        for (int i = 0; i < pending.Count; i++)
        {
            PendingOp op = pending[i];

            if (op.Id < 0 || op.Id >= states.Count)
                continue;

            ResidueState state = states[op.Id];

            if (op.Kind == PendingKind.Apply)
                Apply(state, op.X, op.Y, op.Intensity, op.Duration, op.Owner, op.Origin, op.Variant, op.Payload, op.Tint);
            else
                Remove(state, new Point(op.X, op.Y));
        }

        pending.Clear();
    }

    /// <summary>Set once a canvas pass has thrown, so the log carries one report rather than one per frame.</summary>
    private static bool reportedFailure;

    /// <summary>
    ///     Logs a canvas failure once. Vanilla routes exceptions from this part of the frame to
    ///     <c>TimeLogger.DrawException</c>, which drops them, and skips the NPCs drawn after it.
    /// </summary>
    internal static void ReportFailure(Exception exception)
    {
        if (reportedFailure)
            return;

        reportedFailure = true;
        ModContent.GetInstance<TrelamiumIII>().Logger.Error("Residue canvas failed", exception);
    }

    private static void BuildCanvases()
    {
        if (!loaded || Main.gameMenu)
            return;

        try
        {
            BuildCanvasesUnchecked();
        }
        catch (Exception exception)
        {
            Main.graphics.GraphicsDevice.SetRenderTarget(null);
            ReportFailure(exception);
        }
    }

    private static void BuildCanvasesUnchecked()
    {
        TerrainMask.BeginFrame();

        Rectangle covered = Rectangle.Empty;

        foreach (TileCanvas canvas in AllCanvases())
        {
            canvas.Build();

            Rectangle bounds = canvas.TileBounds;

            if (!bounds.IsEmpty)
                covered = covered.IsEmpty ? bounds : Rectangle.Union(covered, bounds);
        }

        if (Main.drawToScreen && !covered.IsEmpty)
        {
            covered.Inflate(MASK_PADDING, MASK_PADDING);
            TerrainMask.BuildFallback(covered);
        }
    }

    private static void CompositeCanvases()
    {
        if (!loaded || !TerrainMask.TryGet(out Texture2D? mask, out Vector2 origin))
            return;

        try
        {
            foreach (TileCanvas canvas in AllCanvases())
                canvas.Composite(mask, origin);
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
        }
    }

    private static IEnumerable<TileCanvas> AllCanvases()
    {
        foreach (ResidueState state in states)
        {
            if (state.Canvas is { } canvas)
                yield return canvas;
        }

        foreach (TileCanvas overlay in overlays)
            yield return overlay;
    }

    /// <summary>
    ///     255 on the wire: the byte the owner rides in has no room for a signed sentinel, and no
    ///     player ever occupies that slot.
    /// </summary>
    private const byte NO_OWNER_BYTE = 255;

    /// <summary>Ticks before refreshing a mark is worth saying again. Anything else that changes it goes out at once.</summary>
    private const uint RESHARE_TICKS = 45;

    /// <summary>Keeps a packet well under the size a ModPacket can be.</summary>
    private const int MAX_PER_PACKET = 2000;

    private enum ResiduePacket : byte
    {
        Marks,
        Removals,
        Effects,
        Snapshot,
    }

    private static readonly List<ResidueInstance> outgoing = [];

    private static readonly List<(ReactionEffect Effect, Point Tile)> outgoingEffects = [];

    private static ModPacket BeginPacket(ResiduePacket kind)
    {
        ModPacket packet = ModContent.GetInstance<TrelamiumIII>().GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.Residue);
        packet.Write((byte)kind);

        return packet;
    }

    /// <summary>Sends this tick's changes: a client's to the server, and a server's to every client.</summary>
    private static void FlushNetwork()
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        foreach (ResidueState state in states)
        {
            if (state.Changed.Count > 0)
            {
                SendMarks(state, state.Changed, -1);
                state.Changed.Clear();
            }

            if (state.Gone.Count > 0)
            {
                SendRemovals(state);
                state.Gone.Clear();
            }
        }

        if (outgoingEffects.Count == 0)
            return;

        ModPacket packet = BeginPacket(ResiduePacket.Effects);

        packet.Write((ushort)outgoingEffects.Count);

        foreach ((ReactionEffect effect, Point tile) in outgoingEffects)
        {
            packet.Write((byte)effect);
            packet.Write((short)tile.X);
            packet.Write((short)tile.Y);
        }

        packet.Send();
        outgoingEffects.Clear();
    }

    private static void SendMarks(ResidueState state, IEnumerable<Point> positions, int toClient)
    {
        outgoing.Clear();

        foreach (Point position in positions)
        {
            if (state.Tiles.TryGetValue(position, out ResidueInstance instance))
                outgoing.Add(instance);
        }

        for (int start = 0; start < outgoing.Count; start += MAX_PER_PACKET)
        {
            int count = Math.Min(MAX_PER_PACKET, outgoing.Count - start);
            ModPacket packet = BeginPacket(ResiduePacket.Marks);

            packet.Write((byte)state.Id);
            packet.Write((ushort)count);

            for (int n = start; n < start + count; n++)
            {
                ResidueInstance instance = outgoing[n];

                packet.Write((short)instance.X);
                packet.Write((short)instance.Y);
                packet.Write((byte)(MathHelper.Clamp(instance.Intensity, 0f, 1f) * 255f));
                packet.Write((ushort)Math.Clamp(instance.Life, 0f, ushort.MaxValue));
                packet.Write((ushort)Math.Clamp(instance.MaxLife, 0f, ushort.MaxValue));
                packet.Write(instance.Owner is >= 0 and < NO_OWNER_BYTE ? (byte)instance.Owner : NO_OWNER_BYTE);
                packet.Write(instance.Variant);
                packet.Write((byte)(MathHelper.Clamp(instance.Level, 0f, 1f) * 255f));
                packet.Write(instance.Stage);
                packet.Write(instance.Tint.PackedValue);
            }

            packet.Send(toClient);
        }
    }

    private static void SendRemovals(ResidueState state)
    {
        int left = state.Gone.Count;

        using HashSet<Point>.Enumerator gone = state.Gone.GetEnumerator();

        while (left > 0)
        {
            int count = Math.Min(MAX_PER_PACKET, left);
            ModPacket packet = BeginPacket(ResiduePacket.Removals);

            packet.Write((byte)state.Id);
            packet.Write((ushort)count);

            for (int n = 0; n < count && gone.MoveNext(); n++)
            {
                packet.Write((short)gone.Current.X);
                packet.Write((short)gone.Current.Y);
            }

            packet.Send();
            left -= count;
        }
    }

    /// <summary>Tells the clients a reaction just happened, on a server, and returns true. Elsewhere the caller plays it.</summary>
    internal static bool ShareEffect(ReactionEffect effect, Point tile)
    {
        if (Main.netMode != NetmodeID.Server)
            return false;

        outgoingEffects.Add((effect, tile));

        return true;
    }

    /// <summary>A client just entering a world asks for what's already on the ground.</summary>
    internal static void RequestSnapshot()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            BeginPacket(ResiduePacket.Snapshot).Send();
    }

    internal static void Receive(BinaryReader reader, int whoAmI)
    {
        var kind = (ResiduePacket)reader.ReadByte();

        switch (kind)
        {
            case ResiduePacket.Snapshot:
                foreach (ResidueState snapshot in states)
                    SendMarks(snapshot, snapshot.Tiles.Where(static pair => pair.Value.IsShared).Select(static pair => pair.Key), whoAmI);

                return;

            case ResiduePacket.Effects:
                for (int n = reader.ReadUInt16(); n > 0; n--)
                {
                    var effect = (ReactionEffect)reader.ReadByte();
                    int x = reader.ReadInt16();
                    int y = reader.ReadInt16();

                    ReactionEffects.Play(effect, new Point(x, y));
                }

                return;
        }

        int id = reader.ReadByte();
        int count = reader.ReadUInt16();
        ResidueState? state = id < states.Count ? states[id] : null;

        for (int n = 0; n < count; n++)
        {
            int i = reader.ReadInt16();
            int j = reader.ReadInt16();

            if (kind == ResiduePacket.Removals)
            {
                if (state is not null)
                    Remove(state, new Point(i, j), announce: false);

                continue;
            }

            float intensity = reader.ReadByte() / 255f;
            float life = reader.ReadUInt16();
            float maxLife = reader.ReadUInt16();
            byte ownerByte = reader.ReadByte();
            byte variant = reader.ReadByte();
            float level = reader.ReadByte() / 255f;
            byte stage = reader.ReadByte();
            var tint = new Color { PackedValue = reader.ReadUInt32() };

            if (state is null)
                continue;

            if (Main.netMode == NetmodeID.Server)
            {
                // A client only ever asks for its own marks, and the server takes it from here. What the mark does on
                // the way in is the server's to work out, so only what the client can't know comes across.
                Apply(state, i, j, intensity, life <= 0f ? -1f : life, ownerByte == NO_OWNER_BYTE ? ResidueInstance.NoOwner : whoAmI, ResidueOrigin.Owned, variant, null, tint.A > 0 ? tint : null);
            }
            else
            {
                ReceiveMark(state, new Point(i, j), intensity, life, maxLife, ownerByte == NO_OWNER_BYTE ? ResidueInstance.NoOwner : ownerByte, variant, level, stage, tint);
            }
        }
    }

    /// <summary>
    ///     Takes the server's word for a mark. Its hooks already ran on the server, so <see cref="IResidue.OnApplied" /> doesn't
    ///     run again here. What only this machine has stays as it was: the payload behind a mark its owner laid.
    /// </summary>
    private static void ReceiveMark(ResidueState state, Point position, float intensity, float life, float maxLife, int owner, byte variant, float level, byte stage, Color tint)
    {
        if (!WorldGen.InWorld(position.X, position.Y, 1))
            return;

        ref ResidueInstance instance = ref CollectionsMarshal.GetValueRefOrAddDefault(state.Tiles, position, out bool exists);

        if (!exists)
        {
            instance.X = position.X;
            instance.Y = position.Y;
            instance.Serial = ++state.NextSerial;
            instance.Arrival = ++lastArrival;
        }

        instance.Intensity = intensity;
        instance.Life = life;
        instance.MaxLife = maxLife;
        instance.Owner = owner;
        instance.Variant = variant;
        instance.Level = level;
        instance.Stage = stage;
        instance.Tint = tint;
        instance.IsShared = true;

        int serial = instance.Serial;

        if (exists)
            return;

        state.Order.Enqueue((position, serial));
        TrimToBudget(state);
    }
}

/// <summary>Asks for the marks already on the ground when a client joins a world.</summary>
internal sealed class ResidueSnapshotPlayer : ModPlayer
{
    public override void OnEnterWorld() => ResidueSystem.RequestSnapshot();
}
