using System.Collections.Generic;
using Terraria.Utilities;

namespace TrelamiumIII.Core.Graphics.FbmScreen;

/// <summary>
/// Drawn over an <see cref="FbmScreenRenderer"/>'s mist before the pixel pass, so it is pixelated with it. Updated
/// at <see cref="FbmScreenRenderer.StepsPerSecond"/> and removed once <see cref="Active"/> is false.
/// </summary>
internal abstract class ScreenObject
{
    public bool Active { get; protected set; } = true;

    /// <summary>Use <see cref="FbmScreenRenderer.Random"/>, not <c>Main.rand</c>: loading screens run beside world generation.</summary>
    public abstract void Update(FbmScreenRenderer screen, in ScreenFrame frame);

    /// <summary>Premultiplied <see cref="BlendState.AlphaBlend"/>.</summary>
    public virtual void DrawAlpha(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
    }

    /// <summary><see cref="Blends.AdditiveColor"/>: adds light without raising alpha, so it stays additive over the world.</summary>
    public virtual void DrawAdditive(SpriteBatch spriteBatch, in ScreenFrame frame)
    {
    }
}

/// <summary>
/// Spawns <paramref name="initial"/> objects scattered over the frame on its first step, then
/// <paramref name="perSecond"/> more while fewer than <paramref name="limit"/> of its own are alive.
/// The factory's bool is true for the scattered batch.
/// </summary>
internal sealed class ScreenEmitter(float perSecond, int initial, int limit, Func<UnifiedRandom, ScreenFrame, bool, ScreenObject> create)
    : ScreenObject
{
    private readonly List<ScreenObject> alive = [];

    private float accumulated;
    private bool seeded;

    public override void Update(FbmScreenRenderer screen, in ScreenFrame frame)
    {
        alive.RemoveAll(static item => !item.Active);

        if (!seeded)
        {
            seeded = true;

            for (int i = 0; i < initial && alive.Count < limit; i++)
                Emit(screen, frame, scattered: true);
        }

        accumulated += perSecond / FbmScreenRenderer.StepsPerSecond;

        while (accumulated >= 1f)
        {
            accumulated -= 1f;

            if (alive.Count < limit)
                Emit(screen, frame, scattered: false);
        }
    }

    private void Emit(FbmScreenRenderer screen, ScreenFrame frame, bool scattered)
    {
        ScreenObject item = create(screen.Random, frame, scattered);

        alive.Add(item);
        screen.Spawn(item);
    }
}
