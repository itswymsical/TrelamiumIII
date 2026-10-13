using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MonoMod.Cil;
using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Tiles.Conditions;

/// <summary>
///     Owns every <see cref="ITileCondition" />, the batch of tiles each one is laid on, and the
///     passes that draw them.
/// </summary>
/// <remarks>
///     <para>
///         Storage is one flat dictionary per condition rather than a chunk tree: contact testing is
///         an O(1) coordinate lookup, and drawing walks the batch once and culls, which a chunk index
///         would only help past the <see cref="ITileCondition.MaxTiles" /> ceiling.
///     </para>
/// </remarks>
public sealed class TileConditionSystem : ModSystem
{
    /// <summary>Pixels the hitbox is grown by before it is converted to tiles.</summary>
    private const int CONTACT_MARGIN = 2;

    /// <summary>One tick in eight, per tile.</summary>
    private const int LIQUID_PROBE_MASK = 7;

    /// <summary>Tiles the fallback terrain mask extends past the canvases: the kernel plus a lip.</summary>
    private const int MASK_PADDING = 3;

    private sealed class ConditionState(ITileCondition condition, int id)
    {
        public readonly ITileCondition Condition = condition;

        /// <summary>Index into <see cref="states" />, and the condition's identity on hte wire.</summary>
        public readonly int Id = id;

        public readonly Dictionary<Point, TileConditionInstance> Tiles = [];

        /// <summary>Insertion order</summary>
        public readonly Queue<(Point Position, int Serial)> Order = new();

        /// <summary>Scratch list of tiles that ended this tick. Reused and never read outside the tick.</summary>
        public readonly List<Point> Finished = [];

        /// <summary>Null on a server and for conditions that draw nothing.</summary>
        public TileCanvas? Canvas;

        public ConditionCanvasSource? CanvasSource;

        public int NextSerial;
    }

    /// <summary>Canvases drawn from other sources, composited over every condition's.</summary>
    private static readonly List<TileCanvas> overlays = [];

    private enum PendingKind : byte
    {
        Apply,
        Remove,
    }

    private readonly record struct PendingOp(PendingKind Kind, int Id, int X, int Y, float Intensity, float Duration, int Owner, bool Sync, byte Variant, ChemicalPayload? Payload);

    private static readonly List<ConditionState> states = [];

    private static readonly Dictionary<Type, ConditionState> byType = [];

    /// <summary>
    ///     Applications and removals asked for from inside a hook, held until the tick is over.
    /// </summary>
    private static readonly List<PendingOp> pending = [];

    private static bool dispatching;

    private static bool loaded;

    /// <summary>Whether the composite was spliced into the solid tile pass. See <see cref="SpliceComposite" />.</summary>
    private static bool compositeSpliced;

    /// <summary>The registered condition of type <typeparamref name="T" />, or null before load.</summary>
    public static T? Get<T>() where T : class, ITileCondition
        => byType.TryGetValue(typeof(T), out ConditionState? state) ? (T)state.Condition : null;

    /// <summary>
    ///     Lays <typeparamref name="T" /> on the tile at (<paramref name="i" />, <paramref name="j" />).
    /// </summary>
    /// <param name="intensity">How hard it took, 0..1. Clamped.</param>
    /// <param name="duration">Ticks to last, or a negative value to take the condition's own.</param>
    /// <param name="owner">
    ///     The player responsible for the mark, or <see cref="TileConditionInstance.NoOwner" />.
    ///     Conditions that treat their author differently from everyone else read it back off the
    ///     instance.
    /// </param>
    /// <param name="sync">
    ///     Whether to tell the other clients. Leave true for anything a player did; the packet
    ///     handler passes false so a received application is not bounced back out again.
    /// </param>
    /// <param name="variant">
    ///     Which flavour of the condition to apply, for one that has more than one set of rules.
    ///     Meaningless to a condition that does not read
    ///     <see cref="TileConditionInstance.Variant" />, which is most of them.
    /// </param>
    /// <param name="payload">The chemical release responsible, kept on the instance. Not synced.</param>
    /// <returns>
    ///     Whether the application was accepted. A deferred one returns true on the promise, since
    ///     the condition has not been asked yet.
    /// </returns>
    public static bool Apply<T>(int i, int j, float intensity = 1f, float duration = -1f, int owner = TileConditionInstance.NoOwner, bool sync = true, byte variant = 0, ChemicalPayload? payload = null) where T : class, ITileCondition
        => byType.TryGetValue(typeof(T), out ConditionState? state) && Apply(state, i, j, intensity, duration, owner, sync, variant, payload);

    /// <inheritdoc cref="Apply{T}" />
    public static bool Apply(ITileCondition condition, int i, int j, float intensity = 1f, float duration = -1f, int owner = TileConditionInstance.NoOwner, bool sync = true, byte variant = 0, ChemicalPayload? payload = null)
        => byType.TryGetValue(condition.GetType(), out ConditionState? state) && Apply(state, i, j, intensity, duration, owner, sync, variant, payload);

    public static bool Remove<T>(int i, int j) where T : class, ITileCondition
    {
        if (!byType.TryGetValue(typeof(T), out ConditionState? state))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Remove, state.Id, i, j, 0f, 0f, TileConditionInstance.NoOwner, false, 0, null));
            return true;
        }

        return Remove(state, new Point(i, j));
    }

    public static bool Has<T>(int i, int j) where T : class, ITileCondition
        => byType.TryGetValue(typeof(T), out ConditionState? state) && state.Tiles.ContainsKey(new Point(i, j));

    public static bool TryGet<T>(int i, int j, out TileConditionInstance instance) where T : class, ITileCondition
    {
        instance = default;

        return byType.TryGetValue(typeof(T), out ConditionState? state) && state.Tiles.TryGetValue(new Point(i, j), out instance);
    }

    /// <summary>Tiles currently carrying <typeparamref name="T" />.</summary>
    public static int Count<T>() where T : class, ITileCondition
        => byType.TryGetValue(typeof(T), out ConditionState? state) ? state.Tiles.Count : 0;

    public static void ClearAll()
    {
        foreach (ConditionState state in states)
        {
            state.Tiles.Clear();
            state.Order.Clear();
            state.Finished.Clear();
            state.Canvas?.ClearGhosts();
        }

        pending.Clear();
    }

    public override void OnModLoad()
    {
        IEnumerable<Type> discovered = Mod.Code.GetTypes()
            .Where(static type => !type.IsAbstract && !type.IsInterface && typeof(ITileCondition).IsAssignableFrom(type))
            // Ordered by name, not by reflection order: the index doubles as the identity on the
            // wire, and two clients have to agree on it.
            .OrderBy(static type => type.FullName, StringComparer.Ordinal);

        foreach (Type type in discovered)
        {
            var condition = (ITileCondition)Activator.CreateInstance(type)!;
            var state = new ConditionState(condition, states.Count);

            condition.Load();

            if (!Main.dedServ && condition.RenderMode == TileConditionRenderMode.Canvas)
            {
                state.CanvasSource = new ConditionCanvasSource(condition, state.Tiles);
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
            Mod.Logger.Warn("Could not find DrawTileEntities in Main.DoDraw_Tiles_Solid; tile conditions composite after the solid tile pass.");
            return;
        }

        // The call's arguments are already on the stack. This takes none of them and leaves them where they are.
        c.EmitDelegate(CompositeCanvases);
        compositeSpliced = true;
    }

    public override void OnModUnload()
    {
        loaded = false;

        foreach (ConditionState state in states)
        {
            state.Condition.Unload();
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

        foreach (ConditionState state in states)
        {
            state.Condition.UpdateGlobal();
            UpdateBatch(state);
        }

        SweepContacts();

        dispatching = false;

        DrainPending();

        // After the drain, so a tile marked again this tick takes over from its own fading sample.
        foreach (ConditionState state in states)
            state.Canvas?.Update();

        foreach (TileCanvas overlay in overlays)
            overlay.Update();
    }

    /// <summary>Builds and composites a canvas for <paramref name="source" /> alongside the conditions'. Client only.</summary>
    internal static TileCanvas? AddOverlay(ITileCanvasSource source)
    {
        if (Main.dedServ)
            return null;

        var canvas = new TileCanvas(source);
        overlays.Add(canvas);

        return canvas;
    }

    private static bool Apply(ConditionState state, int i, int j, float intensity, float duration, int owner, bool sync, byte variant, ChemicalPayload? payload)
    {
        if (!WorldGen.InWorld(i, j, 1))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Apply, state.Id, i, j, intensity, duration, owner, sync, variant, payload));
            return true;
        }

        if (!state.Condition.CanApplyTo(i, j))
            return false;

        float life = duration > 0f ? duration : state.Condition.Duration;

        if (life <= 0f)
            return false;

        var position = new Point(i, j);
        bool exists = state.Tiles.TryGetValue(position, out TileConditionInstance existing);

        var instance = new TileConditionInstance
        {
            X = i,
            Y = j,
            Life = life,
            MaxLife = life,
            Intensity = MathHelper.Clamp(intensity, 0f, 1f),
            Owner = owner,
            Payload = payload,
            Variant = variant,

            // Re-application refreshes the slot rather than replacing it, so the entry the eviction
            // queue is already holding stays the one that matches.
            Serial = exists ? existing.Serial : ++state.NextSerial,

            // Content state survives a refresh, so a condition can treat a second application as
            // adding to what is already there rather than as starting over.
            Level = exists ? existing.Level : 0f,
            Stage = exists ? existing.Stage : (byte)0,
            TotalAge = exists ? existing.TotalAge : 0,
        };

        state.Condition.OnApplied(ref instance);
        state.Tiles[position] = instance;

        if (!exists)
        {
            state.Order.Enqueue((position, instance.Serial));
            TrimToBudget(state);
        }

        if (sync && Main.netMode != NetmodeID.SinglePlayer)
            SendApply(state, i, j, instance.Intensity, life, instance.Owner, instance.Variant);

        return true;
    }

    private static bool Remove(ConditionState state, Point position)
    {
        if (!state.Tiles.Remove(position, out TileConditionInstance instance))
            return false;

        if (state.Canvas is { } canvas)
            state.CanvasSource?.OnRemoved(canvas, in instance);
        state.Condition.OnExpired(in instance);

        return true;
    }

    /// <summary>Drops the oldest tiles until the batch is back inside its ceiling.</summary>
    private static void TrimToBudget(ConditionState state)
    {
        int max = Math.Max(state.Condition.MaxTiles, 1);

        while (state.Tiles.Count > max && state.Order.Count > 0)
        {
            (Point position, int serial) = state.Order.Dequeue();

            // A stale entry: the tile it named has since expired, and anything sitting there now is
            // a different application that has its own place in the queue.
            if (state.Tiles.TryGetValue(position, out TileConditionInstance instance) && instance.Serial == serial)
                Remove(state, position);
        }
    }

    private static void UpdateBatch(ConditionState state)
    {
        if (state.Tiles.Count == 0)
            return;

        state.Finished.Clear();

        foreach (Point position in state.Tiles.Keys)
        {
            // By reference: the hooks below mutate the instance, and a copy-then-store round trip
            // for every tile every tick is the cost this system is most exposed to. Safe against the
            // hooks because they cannot structurally modify the batch: see the pending queue.
            ref TileConditionInstance instance = ref CollectionsMarshal.GetValueRefOrNullRef(state.Tiles, position);

            instance.Age++;
            instance.TotalAge++;
            instance.Life--;

            if (((instance.Age + position.X * 7 + position.Y * 13) & LIQUID_PROBE_MASK) == 0)
                ProbeLiquid(state.Condition, ref instance);

            state.Condition.UpdateTile(ref instance);

            if (instance.Life <= 0f || !state.Condition.CanRemain(in instance))
                state.Finished.Add(position);
        }

        foreach (Point position in state.Finished)
            Remove(state, position);
    }

    /// <summary>
    ///     Reports the wettest of the tile and its four neighbours, if any of them holds liquid.
    /// </summary>
    private static void ProbeLiquid(ITileCondition condition, ref TileConditionInstance instance)
    {
        int bestAmount = 0;
        int bestType = LiquidID.Water;

        Consider(instance.X, instance.Y);
        Consider(instance.X - 1, instance.Y);
        Consider(instance.X + 1, instance.Y);
        Consider(instance.X, instance.Y - 1);
        Consider(instance.X, instance.Y + 1);

        if (bestAmount > 0)
            condition.OnLiquidContact(ref instance, bestType, bestAmount);

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

        foreach (ConditionState state in states)
            total += state.Tiles.Count;

        // Nothing is marked, so nothing can be touched. Worth checking first: without it this walks
        // every player, NPC and projectile in the world on every tick of a game where no condition
        // has ever been applied.
        if (total == 0)
            return;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];

            if (player.active && !player.dead)
                SweepEntity(player);
        }

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

                foreach (ConditionState state in states)
                {
                    if (state.Tiles.Count == 0)
                        continue;

                    ref TileConditionInstance instance = ref CollectionsMarshal.GetValueRefOrNullRef(state.Tiles, position);

                    if (Unsafe.IsNullRef(ref instance))
                        continue;

                    switch (entity)
                    {
                        case Player player:
                            state.Condition.OnPlayerContact(ref instance, player);
                            break;

                        case NPC npc:
                            state.Condition.OnNPCContact(ref instance, npc);
                            break;

                        case Projectile projectile:
                            state.Condition.OnProjectileContact(ref instance, projectile);
                            break;
                    }
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

            ConditionState state = states[op.Id];

            if (op.Kind == PendingKind.Apply)
                Apply(state, op.X, op.Y, op.Intensity, op.Duration, op.Owner, op.Sync, op.Variant, op.Payload);
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
    private static void ReportFailure(Exception exception)
    {
        if (reportedFailure)
            return;

        reportedFailure = true;
        ModContent.GetInstance<TrelamiumIII>().Logger.Error("Tile condition canvas failed", exception);
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
        foreach (ConditionState state in states)
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

    private static void SendApply(ConditionState state, int i, int j, float intensity, float duration, int owner, byte variant, int ignoreClient = -1)
    {
        ModPacket packet = ModContent.GetInstance<TrelamiumIII>().GetPacket();

        packet.Write((byte)TrelamiumIII.MessageType.TileCondition);
        packet.Write((byte)state.Id);
        packet.Write((short)i);
        packet.Write((short)j);
        packet.Write((byte)(MathHelper.Clamp(intensity, 0f, 1f) * 255f));
        packet.Write((ushort)Math.Clamp(duration, 0f, ushort.MaxValue));
        packet.Write(owner is >= 0 and < NO_OWNER_BYTE ? (byte)owner : NO_OWNER_BYTE);
        packet.Write(variant);
        packet.Send(-1, ignoreClient);
    }

    internal static void ReceiveApply(BinaryReader reader, int whoAmI)
    {
        int id = reader.ReadByte();
        int i = reader.ReadInt16();
        int j = reader.ReadInt16();
        float intensity = reader.ReadByte() / 255f;
        float duration = reader.ReadUInt16();
        byte ownerByte = reader.ReadByte();
        byte variant = reader.ReadByte();

        if (id < 0 || id >= states.Count)
            return;

        int owner = ownerByte == NO_OWNER_BYTE ? TileConditionInstance.NoOwner : ownerByte;

        ConditionState state = states[id];

        Apply(state, i, j, intensity, duration <= 0f ? -1f : duration, owner, sync: false, variant, payload: null);

        // The server is the only one that has heard this, so it is the only one that can tell the
        // rest, minus the client it came from, which already has it.
        if (Main.netMode == NetmodeID.Server)
            SendApply(state, i, j, intensity, duration, owner, variant, ignoreClient: whoAmI);
    }
}
