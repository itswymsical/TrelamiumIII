using System.Collections.Generic;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>
/// Per-node animation for the skill tree's glyphs: unlock and lock bursts, a refused-purchase shake, hover
/// easing and an idle sheen across held nodes. Every layer is a white mask built from the socket's own alpha.
/// </summary>
internal sealed class SkillGlyphFlash
{
    private const float UNLOCK_SECONDS = 0.85f;
    private const float LOCK_SECONDS = 0.50f;
    private const float DENY_SECONDS = 0.35f;

    /// <summary>Seconds between idle sheens, and how long one takes to cross a glyph.</summary>
    private const float SHEEN_PERIOD = 5f;
    private const float SHEEN_SECONDS = 0.6f;

    /// <summary>Screen pixels per second of sheen stagger, so a row lights up left to right.</summary>
    private const float SHEEN_SPEED = 170f;

    /// <summary>Hover weight gained or lost per tick.</summary>
    private const float HOVER_RATE = 0.16f;

    private const int GLOW_PADDING = 6;
    private const int GLOW_BLUR_RADIUS = 3;

    private static Texture2D? _mask;
    private static Texture2D? _glowMask;

    private sealed class Node
    {
        public bool Held;
        public bool Seen;
        public float Hover;
        public float UnlockAt = float.NaN;
        public float LockAt = float.NaN;
        public float DenyAt = float.NaN;
    }

    private readonly Dictionary<string, Node> _nodes = [];

    /// <summary>Forgets every node's state, so reopening the tree does not replay the bursts.</summary>
    public void Reset() => _nodes.Clear();

    /// <summary>
    /// Advances <paramref name="id"/>'s hover weight and starts a burst when it changes hands. The first
    /// call for a node only records whether it is held.
    /// </summary>
    public void Observe(string id, bool held, bool hovered)
    {
        Node node = NodeFor(id);

        node.Hover = Math.Clamp(node.Hover + (hovered ? HOVER_RATE : -HOVER_RATE), 0f, 1f);

        Expire(node);

        if (!node.Seen)
        {
            node.Seen = true;
            node.Held = held;

            return;
        }

        if (held != node.Held)
        {
            if (held)
                node.UnlockAt = Main.GlobalTimeWrappedHourly;
            else
                node.LockAt = Main.GlobalTimeWrappedHourly;

            node.Held = held;
        }
    }

    /// <summary>Starts the shake and red flash for a purchase the player could not make.</summary>
    public void Deny(string id) => NodeFor(id).DenyAt = Main.GlobalTimeWrappedHourly;

    /// <summary>Draw scale for <paramref name="id"/>: hover lift, unlock pop, lock dip.</summary>
    public float ScaleOf(string id)
    {
        if (!_nodes.TryGetValue(id, out Node? node))
            return 1f;

        float scale = 1f + 0.14f * EaseFunction.EaseQuadOut.Ease(node.Hover);
        float unlock = Progress(node.UnlockAt, UNLOCK_SECONDS);
        float locked = Progress(node.LockAt, LOCK_SECONDS);

        if (unlock >= 0f)
        {
            scale += 0.34f * EaseFunction.EaseCircularOut.Ease(Math.Min(1f, unlock / 0.16f))
                * EaseFunction.EaseQuadIn.Ease(1f - unlock);
        }

        if (locked >= 0f)
            scale -= 0.20f * MathF.Sin(MathF.PI * EaseFunction.EaseQuadOut.Ease(locked));

        return scale;
    }

    /// <summary>Horizontal shake for a refused purchase.</summary>
    public Vector2 OffsetOf(string id)
    {
        if (!_nodes.TryGetValue(id, out Node? node))
            return Vector2.Zero;

        float deny = Progress(node.DenyAt, DENY_SECONDS);

        return deny < 0f ? Vector2.Zero : new Vector2(MathF.Sin(deny * MathF.PI * 6f) * 3.5f * (1f - deny), 0f);
    }

    /// <summary>Burst glow behind the glyph. <paramref name="centre"/> is the socket's centre.</summary>
    public void DrawUnder(SpriteBatch spriteBatch, Texture2D socket, string id, Vector2 centre, float scale, Color tone, float alpha)
    {
        if (!_nodes.TryGetValue(id, out Node? node))
            return;

        Texture2D glow = Glow(socket);
        Vector2 origin = glow.Size() * 0.5f;

        float unlock = Progress(node.UnlockAt, UNLOCK_SECONDS);
        float locked = Progress(node.LockAt, LOCK_SECONDS);

        if (unlock >= 0f)
        {
            float fade = 1f - unlock;

            spriteBatch.Draw(glow, centre, null, Additive(tone, 1.2f * fade * fade * alpha), 0f, origin,
                scale * (1f + 0.5f * unlock), SpriteEffects.None, 0f);
        }

        if (locked >= 0f)
        {
            spriteBatch.Draw(glow, centre, null, Additive(new Color(150, 150, 190), 0.8f * MathF.Sin(locked * MathF.PI) * alpha),
                0f, origin, scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>Flash, sheen and ring over the glyph. <paramref name="held"/> adds the idle sheen.</summary>
    public void DrawOver(SpriteBatch spriteBatch, Texture2D socket, string id, Vector2 centre, float scale, Color tone, float alpha, bool held)
    {
        if (!_nodes.TryGetValue(id, out Node? node))
            return;

        Texture2D mask = Mask(socket);
        Vector2 origin = mask.Size() * 0.5f;
        Vector2 topLeft = centre - origin * scale;

        float unlock = Progress(node.UnlockAt, UNLOCK_SECONDS);
        float locked = Progress(node.LockAt, LOCK_SECONDS);
        float deny = Progress(node.DenyAt, DENY_SECONDS);
        float sweep = held ? Sweep(centre.X) : -1f;

        if (unlock >= 0f)
            DrawUnlock(spriteBatch, mask, centre, topLeft, origin, scale, tone, alpha, unlock);

        if (locked >= 0f)
            DrawLock(spriteBatch, mask, centre, origin, scale, alpha, locked);

        if (deny >= 0f)
        {
            spriteBatch.Draw(mask, centre, null, new Color(255, 90, 90) * (0.75f * (1f - deny) * alpha), 0f, origin,
                scale, SpriteEffects.None, 0f);
        }

        if (sweep >= 0f)
        {
            SpriteSheen.Draw(spriteBatch, mask, topLeft, scale, sweep, Color.White * (0.55f * alpha));

            spriteBatch.Draw(mask, centre, null, Color.White * (0.10f * MathF.Sin(sweep * MathF.PI) * alpha), 0f, origin,
                scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>White rise fading back to the socket, a sheen crossing it, and a ring expanding off its rim.</summary>
    private static void DrawUnlock(SpriteBatch spriteBatch, Texture2D mask, Vector2 centre, Vector2 topLeft, Vector2 origin,
        float scale, Color tone, float alpha, float progress)
    {
        float white = progress < 0.10f
            ? progress / 0.10f
            : EaseFunction.EaseQuadIn.Ease(Math.Max(0f, 1f - (progress - 0.10f) / 0.45f));

        if (white > 0f)
            spriteBatch.Draw(mask, centre, null, Color.White * (white * alpha), 0f, origin, scale, SpriteEffects.None, 0f);

        SpriteSheen.Draw(spriteBatch, mask, topLeft, scale, (progress - 0.25f) / 0.75f, Color.White * (0.85f * alpha));

        float ring = EaseFunction.EaseQuadOut.Ease(progress);
        float fade = 1f - progress;

        spriteBatch.Draw(mask, centre, null, Additive(tone, 0.75f * fade * fade * alpha), 0f, origin,
            scale * (1f + 1.9f * ring), SpriteEffects.None, 0f);
    }

    /// <summary>A ring collapsing onto the socket, with the white spike landing just before it closes.</summary>
    private static void DrawLock(SpriteBatch spriteBatch, Texture2D mask, Vector2 centre, Vector2 origin,
        float scale, float alpha, float progress)
    {
        float ring = 1f + 1.4f * (1f - EaseFunction.EaseCircularOut.Ease(progress));

        spriteBatch.Draw(mask, centre, null, Additive(new Color(190, 190, 215), 0.7f * MathF.Sin(progress * MathF.PI) * alpha),
            0f, origin, scale * ring, SpriteEffects.None, 0f);

        float white = Math.Max(0f, 1f - MathF.Abs(progress - 0.78f) / 0.16f);

        if (white > 0f)
            spriteBatch.Draw(mask, centre, null, new Color(220, 225, 255) * (0.9f * white * alpha), 0f, origin, scale, SpriteEffects.None, 0f);
    }

    /// <summary>The socket's white mask, and a padded blurred copy for glows. Built on first use.</summary>
    public static Texture2D Mask(Texture2D socket) => _mask ??= SpriteMask.Build(socket);

    public static Texture2D Glow(Texture2D socket) => _glowMask ??= SpriteMask.Build(socket, GLOW_PADDING, GLOW_BLUR_RADIUS);

    /// <summary>Disposes the generated masks on the main thread.</summary>
    public static void Unload()
    {
        Texture2D? mask = _mask;
        Texture2D? glowMask = _glowMask;
        _mask = _glowMask = null;

        if (mask == null && glowMask == null)
            return;

        Main.QueueMainThreadAction(() =>
        {
            mask?.Dispose();
            glowMask?.Dispose();
        });
    }

    private Node NodeFor(string id)
    {
        if (!_nodes.TryGetValue(id, out Node? node))
            _nodes[id] = node = new Node();

        return node;
    }

    private static void Expire(Node node)
    {
        if (Progress(node.UnlockAt, UNLOCK_SECONDS) < 0f) node.UnlockAt = float.NaN;
        if (Progress(node.LockAt, LOCK_SECONDS) < 0f) node.LockAt = float.NaN;
        if (Progress(node.DenyAt, DENY_SECONDS) < 0f) node.DenyAt = float.NaN;
    }

    /// <summary>0..1 through a burst started at <paramref name="startTime"/>; -1 when idle or finished.</summary>
    private static float Progress(float startTime, float duration)
    {
        if (float.IsNaN(startTime))
            return -1f;

        float elapsed = Main.GlobalTimeWrappedHourly - startTime;

        // GlobalTimeWrappedHourly wraps to 0 every 3600 seconds.
        if (elapsed < -1800f)
            elapsed += 3600f;

        return elapsed >= 0f && elapsed <= duration ? elapsed / duration : -1f;
    }

    /// <summary>0..1 through the idle sheen at screen x <paramref name="x"/>; -1 between sheens.</summary>
    private static float Sweep(float x)
    {
        float phase = (Main.GlobalTimeWrappedHourly - x / SHEEN_SPEED) % SHEEN_PERIOD;

        if (phase < 0f)
            phase += SHEEN_PERIOD;

        return phase < SHEEN_SECONDS ? phase / SHEEN_SECONDS : -1f;
    }

    /// <summary>Zero-alpha colours add in the premultiplied AlphaBlend UI batch.</summary>
    private static Color Additive(Color color, float opacity) => (color * opacity) with { A = 0 };
}
