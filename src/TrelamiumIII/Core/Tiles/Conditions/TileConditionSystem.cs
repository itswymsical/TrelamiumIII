using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TrelamiumIII.Core.Graphics;

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

    private const int CULL_PADDING = 4;

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

        public WorldFieldTarget? Field;

        public int NextSerial;
    }

    private enum PendingKind : byte
    {
        Apply,
        Remove,
    }

    private readonly record struct PendingOp(PendingKind Kind, int Id, int X, int Y, float Intensity, float Duration, int Owner, bool Sync, byte Variant);

    private static readonly List<ConditionState> states = [];

    private static readonly Dictionary<Type, ConditionState> byType = [];

    /// <summary>
    ///     Applications and removals asked for from inside a hook, held until the tick is over.
    /// </summary>
    private static readonly List<PendingOp> pending = [];

    private static bool dispatching;

    private static bool loaded;

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
    /// <returns>
    ///     Whether the application was accepted. A deferred one returns true on the promise, since
    ///     the condition has not been asked yet.
    /// </returns>
    public static bool Apply<T>(int i, int j, float intensity = 1f, float duration = -1f, int owner = TileConditionInstance.NoOwner, bool sync = true, byte variant = 0) where T : class, ITileCondition
        => byType.TryGetValue(typeof(T), out ConditionState? state) && Apply(state, i, j, intensity, duration, owner, sync, variant);

    /// <inheritdoc cref="Apply{T}" />
    public static bool Apply(ITileCondition condition, int i, int j, float intensity = 1f, float duration = -1f, int owner = TileConditionInstance.NoOwner, bool sync = true, byte variant = 0)
        => byType.TryGetValue(condition.GetType(), out ConditionState? state) && Apply(state, i, j, intensity, duration, owner, sync, variant);

    public static bool Remove<T>(int i, int j) where T : class, ITileCondition
    {
        if (!byType.TryGetValue(typeof(T), out ConditionState? state))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Remove, state.Id, i, j, 0f, 0f, TileConditionInstance.NoOwner, false, 0));
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
            states.Add(state);
            byType[type] = state;
        }

        loaded = true;

        if (Main.dedServ)
            return;

        On_Main.CheckMonoliths += static orig =>
        {
            orig();
            BuildFields();
        };
    }

    public override void OnModUnload()
    {
        loaded = false;

        foreach (ConditionState state in states)
        {
            state.Condition.Unload();
            state.Field?.Reset();
            state.Field = null;
        }

        states.Clear();
        byType.Clear();
        pending.Clear();
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
    }

    public override void PostDrawTiles()
    {
        if (!loaded || Main.dedServ)
            return;

        Rectangle area = VisibleTileArea();

        foreach (ConditionState state in states)
        {
            if (state.Tiles.Count == 0)
                continue;

            TileConditionRenderMode mode = state.Condition.RenderMode;

            // Field first, then overlay, and a condition may ask for both.
            if (mode.HasFlag(TileConditionRenderMode.Field))
                CompositeField(state);

            if (mode.HasFlag(TileConditionRenderMode.Overlay))
                DrawOverlay(state, area);
        }
    }

    private static bool Apply(ConditionState state, int i, int j, float intensity, float duration, int owner, bool sync, byte variant)
    {
        if (!WorldGen.InWorld(i, j, 1))
            return false;

        if (dispatching)
        {
            pending.Add(new PendingOp(PendingKind.Apply, state.Id, i, j, intensity, duration, owner, sync, variant));
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
            Variant = variant,

            // Re-application refreshes the slot rather than replacing it, so the entry the eviction
            // queue is already holding stays the one that matches.
            Serial = exists ? existing.Serial : ++state.NextSerial,

            // Content state survives a refresh, so a condition can treat a second application as
            // adding to what is already there rather than as starting over.
            Level = exists ? existing.Level : 0f,
            Stage = exists ? existing.Stage : (byte)0,
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
                Apply(state, op.X, op.Y, op.Intensity, op.Duration, op.Owner, op.Sync, op.Variant);
            else
                Remove(state, new Point(op.X, op.Y));
        }

        pending.Clear();
    }

    private static Rectangle VisibleTileArea()
    {
        // Built from the unzoomed viewport, which is a superset of the zoomed one for every zoom
        // level Terraria allows. Cheaper than the exact rectangle and it can only ever over-include.
        int left = Math.Max((int)(Main.screenPosition.X / 16f) - CULL_PADDING, 0);
        int top = Math.Max((int)(Main.screenPosition.Y / 16f) - CULL_PADDING, 0);
        int right = Math.Min((int)((Main.screenPosition.X + Main.screenWidth) / 16f) + CULL_PADDING, Main.maxTilesX - 1);
        int bottom = Math.Min((int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + CULL_PADDING, Main.maxTilesY - 1);

        return new Rectangle(left, top, right - left + 1, bottom - top + 1);
    }

    private static void DrawOverlay(ConditionState state, Rectangle area)
    {
        ITileCondition condition = state.Condition;
        Effect? effect = condition.OverlayShader?.Value;

        if (effect is not null)
            condition.ApplyShaderParameters(effect);

        condition.BindAdditionalTextures(Main.graphics.GraphicsDevice);

        SpriteBatch spriteBatch = Main.spriteBatch;

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            condition.BlendState,
            Main.DefaultSamplerState,
            DepthStencilState.None,
            Main.Rasterizer,
            effect,
            Main.GameViewMatrix.TransformationMatrix
        );

        DrawBatch(state, area, spriteBatch, overlay: true);

        spriteBatch.End();
    }

    private static void BuildFields()
    {
        if (!loaded || Main.dedServ || Main.gameMenu)
            return;

        foreach (ConditionState state in states)
        {
            if (!state.Condition.RenderMode.HasFlag(TileConditionRenderMode.Field) || state.Tiles.Count == 0)
                continue;

            state.Field ??= new WorldFieldTarget(spriteBatch => DrawBatch(state, VisibleTileArea(), spriteBatch, overlay: false))
            {
                PixelScale = Math.Max(state.Condition.FieldPixelScale, 1),
                Blend = state.Condition.FieldBlend,
                Sampler = SamplerState.PointClamp,
            };

            // Refreshed per frame rather than set once at construction: the asset is lazily loaded,
            // so the first few frames of a session can legitimately hand back a shader that was not
            // ready when the target was built.
            state.Field.StampEffect = state.Condition.StampShader?.Value;

            state.Field.Build();
        }
    }

    private static void CompositeField(ConditionState state)
    {
        if (state.Field is not { Field: { } field })
            return;

        ITileCondition condition = state.Condition;
        Effect? effect = condition.Shader?.Value;
        SpriteBatch spriteBatch = Main.spriteBatch;

        // No view matrix: the field was built with the zoom already baked into its own transform, so
        // it composites as a plain full-screen quad.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            condition.BlendState,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone
        );

        condition.BindAdditionalTextures(Main.graphics.GraphicsDevice);

        if (effect is not null)
        {
            condition.ApplyShaderParameters(effect);
            effect.CurrentTechnique.Passes[0].Apply();
        }

        spriteBatch.Draw(field, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, state.Field.PixelScale, SpriteEffects.None, 0f);

        spriteBatch.End();
    }

    /// <param name="overlay">
    ///     Which pass this is. A condition running both draws different geometry into each, so the
    ///     two cannot share an entry point: see <see cref="ITileCondition.DrawTileOverlay" />.
    /// </param>
    private static void DrawBatch(ConditionState state, Rectangle area, SpriteBatch spriteBatch, bool overlay)
    {
        foreach (KeyValuePair<Point, TileConditionInstance> pair in state.Tiles)
        {
            if (!area.Contains(pair.Key))
                continue;

            TileConditionInstance instance = pair.Value;
            Vector2 position = instance.WorldPosition - Main.screenPosition;

            if (overlay)
                state.Condition.DrawTileOverlay(spriteBatch, in instance, position);
            else
                state.Condition.DrawTile(spriteBatch, in instance, position);
        }
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

        Apply(state, i, j, intensity, duration <= 0f ? -1f : duration, owner, sync: false, variant);

        // The server is the only one that has heard this, so it is the only one that can tell the
        // rest, minus the client it came from, which already has it.
        if (Main.netMode == NetmodeID.Server)
            SendApply(state, i, j, intensity, duration, owner, variant, ignoreClient: whoAmI);
    }
}
