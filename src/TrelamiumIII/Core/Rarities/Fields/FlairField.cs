using System.Collections.Generic;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Core.Rarities.Fields;

/// <summary>Where a <see cref="Flairs.FieldFlair" /> reports emitters from.</summary>
[Flags]
public enum FieldSurfaces
{
    None = 0,

    /// <summary>The tooltip name, or any text drawn through <see cref="RarityText" />.</summary>
    Text = 1,

    /// <summary>Items drawn in inventory slots.</summary>
    Slot = 2,

    /// <summary>Items lying in the world.</summary>
    World = 4,
}

/// <summary>The part of the text ink or item sprite an emitter covers.</summary>
public enum EmitArea
{
    /// <summary>The text baseline, or the sprite's bottom edge, across the full width.</summary>
    Baseline,

    /// <summary>The ink rectangle, or the sprite rectangle.</summary>
    Bounds,

    /// <summary>The top edge of the ink or sprite.</summary>
    Top,

    /// <summary>The centre point.</summary>
    Center,
}

/// <summary>A spawn area reported by one draw, in the reporting batch's space.</summary>
public readonly struct FieldEmitter(
    object key,
    FieldSurfaces surface,
    Vector2 position,
    Vector2 size,
    Vector2 anchor,
    float scale,
    Matrix transform,
    GlyphLayout? layout = null,
    Vector2 textPosition = default,
    Vector2 textScale = default)
{
    /// <summary>Owner of per-emitter content: the text key, or the item in a slot.</summary>
    public readonly object Key = key;

    public readonly FieldSurfaces Surface = surface;

    /// <summary>Top-left of the spawn area.</summary>
    public readonly Vector2 Position = position;

    public readonly Vector2 Size = size;

    /// <summary>Centre of the ink or sprite. Emitter-local content is positioned from it.</summary>
    public readonly Vector2 Anchor = anchor;

    /// <summary>Draw scale of the text or sprite.</summary>
    public readonly float Scale = scale;

    /// <summary>Transform of the reporting batch.</summary>
    public readonly Matrix Transform = transform;

    /// <summary>The reporting text's layout, or null for a sprite.</summary>
    public readonly GlyphLayout? Layout = layout;

    /// <summary>Top-left of the reporting text's line box.</summary>
    public readonly Vector2 TextPosition = textPosition;

    public readonly Vector2 TextScale = textScale;

    /// <summary>A uniformly random point in the spawn area.</summary>
    public Vector2 RandomPoint() => Position + new Vector2(Main.rand.NextFloat(Size.X), Main.rand.NextFloat(Size.Y));

    /// <summary>Draws the reporting text's glyphs, unrotated and without outline. Does nothing for a sprite.</summary>
    public void DrawText(SpriteBatch spriteBatch, Color color)
    {
        if (Layout is null)
            return;

        foreach (TextGlyph glyph in Layout.Glyphs)
        {
            spriteBatch.Draw(glyph.Texture, TextPosition + glyph.Position * TextScale, glyph.Source, color, 0f, Vector2.Zero, TextScale,
                SpriteEffects.None, 0f);
        }
    }
}

/// <summary>
///     Content a <see cref="Flairs.FieldFlair" /> places. Draws report emitters; content spawns and moves on UI update ticks, which
///     also run while the game is paused; <see cref="Draw" /> runs inside the reporting hook.
/// </summary>
public abstract class FlairField
{
    /// <summary>Ticks an emitter keeps spawning after its last report, so draw rates below the tick rate still spawn every tick.</summary>
    private const ulong EMITTER_GRACE_TICKS = 2;

    /// <summary>Ticks after an emitter key's last report before its spawn carry is dropped.</summary>
    private const ulong KEY_EXPIRY_TICKS = 300;

    private sealed class KeyState
    {
        public float Carry;
        public ulong LastTick;
    }

    private readonly List<FieldEmitter> emitters = [];
    private readonly Dictionary<object, KeyState> keys = [];

    private ulong emittersFrame = ulong.MaxValue;
    private ulong lastReportTick;
    private ulong drawnFrame = ulong.MaxValue;

    protected FlairField()
    {
        if (!Main.dedServ)
            FlairFieldSystem.Register(this);
    }

    /// <summary>Spawns per tick for each text or slot emitter. Fractions carry over between ticks.</summary>
    public float Rate { get; init; } = 1f;

    /// <summary>Spawns per tick for each item lying in the world. The fraction is a chance.</summary>
    public float WorldRate { get; init; } = 0.5f;

    /// <summary>The surfaces this field can spawn on or draw.</summary>
    public abstract FieldSurfaces Supported { get; }

    /// <summary>Emitters reported during the previous draw frame, or none once they are older than that.</summary>
    protected IReadOnlyList<FieldEmitter> PreviousFrameEmitters => emittersFrame + 1 == FlairFieldSystem.DrawFrame ? emitters : [];

    internal void Report(in FieldEmitter emitter)
    {
        if (emittersFrame != FlairFieldSystem.DrawFrame)
        {
            emitters.Clear();
            emittersFrame = FlairFieldSystem.DrawFrame;
        }

        emitters.Add(emitter);
        lastReportTick = FlairFieldSystem.Ticks;
    }

    internal void Tick()
    {
        ulong now = FlairFieldSystem.Ticks;

        if (now - lastReportTick <= EMITTER_GRACE_TICKS)
        {
            foreach (FieldEmitter emitter in emitters)
            {
                if (!keys.TryGetValue(emitter.Key, out KeyState? state))
                    keys[emitter.Key] = state = new KeyState();

                state.LastTick = now;
                state.Carry += Rate;

                for (; state.Carry >= 1f; state.Carry--)
                    Spawn(emitter);
            }
        }

        foreach ((object key, KeyState state) in keys)
        {
            if (now - state.LastTick > KEY_EXPIRY_TICKS)
                keys.Remove(key);
        }

        Update();
    }

    /// <summary>True for the first call in each draw frame, for fields that draw all their content in one pass.</summary>
    protected bool FirstDrawThisFrame()
    {
        if (drawnFrame == FlairFieldSystem.DrawFrame)
            return false;

        drawnFrame = FlairFieldSystem.DrawFrame;

        return true;
    }

    /// <summary>The whole part of <paramref name="rate" />, plus one more with the fractional part as its chance.</summary>
    protected static int RollCount(float rate)
    {
        int count = (int)rate;

        if (Main.rand.NextFloat() < rate - count)
            count++;

        return count;
    }

    /// <summary>Adds one piece of content for <paramref name="emitter" />.</summary>
    protected abstract void Spawn(in FieldEmitter emitter);

    /// <summary>Advances content by one tick.</summary>
    protected abstract void Update();

    /// <summary>Builds render targets. Called at <c>DoDraw_UpdateCameraPosition</c>, the one point in a frame where binding one is safe.</summary>
    protected internal virtual void Accumulate() { }

    /// <summary>Draws content for <paramref name="emitter" /> into the active batch, which must be left in the state it was found.</summary>
    protected internal abstract void Draw(SpriteBatch spriteBatch, in FieldEmitter emitter);

    /// <summary>Spawns into a world system for an item lying in the world, at <see cref="WorldRate" />.</summary>
    protected internal virtual void SpawnWorld(WorldItem item) { }

    /// <summary>Drops content and frees render targets.</summary>
    protected internal virtual void Release() { }
}
