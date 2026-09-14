using TrelamiumIII.Core.Rarities.Fields;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>
///     Places a <see cref="FlairField" /> (metaballs, particles, a render target) on the item's text, its slot sprite and its world
///     drops. The flair decides where content spawns and which side it draws on; the field owns what the content is.
/// </summary>
public sealed class FieldFlair : RarityFlair
{
    public required FlairField Field { get; init; }

    /// <summary>Where to emit. Surfaces the field does not support are ignored.</summary>
    public FieldSurfaces Surfaces { get; init; } = FieldSurfaces.Text | FieldSurfaces.World;

    /// <summary>The part of the text ink or sprite that spawns content.</summary>
    public EmitArea Area { get; init; } = EmitArea.Baseline;

    /// <summary>Moves the spawn area, in the batch's pixels.</summary>
    public Vector2 Offset { get; init; }

    /// <summary>Grows the spawn area on each side, in the batch's pixels.</summary>
    public Vector2 Padding { get; init; }

    /// <summary>Draws in front of the text or sprite instead of behind it.</summary>
    public bool InFront { get; init; }

    public override bool ManagesOwnBatch => true;

    public override void UnderName(in RarityNameContext ctx)
    {
        if (!InFront)
            DrawOnText(ctx);
    }

    public override void OverName(in RarityNameContext ctx)
    {
        if (InFront)
            DrawOnText(ctx);
    }

    public override void UnderSprite(in RaritySpriteContext ctx)
    {
        if (!InFront)
            DrawOnSlot(ctx);
    }

    public override void OverSprite(in RaritySpriteContext ctx)
    {
        if (InFront)
            DrawOnSlot(ctx);
    }

    public override void UpdateWorldItem(Item item)
    {
        if (Enabled(FieldSurfaces.World))
            Field.SpawnWorld(item);
    }

    public override void Unload() => Field.Release();

    private bool Enabled(FieldSurfaces surface) => (Surfaces & Field.Supported & surface) != 0;

    private void DrawOnText(in RarityNameContext ctx)
    {
        if (Main.dedServ || !Enabled(FieldSurfaces.Text))
            return;

        Vector2 min = ctx.InkMin;
        Vector2 max = ctx.InkMax;
        (Vector2 position, Vector2 size) = AreaOf(min, max, ctx.BaselineY);

        var emitter = new FieldEmitter(ctx.Key, FieldSurfaces.Text, position, size, (min + max) * 0.5f, ctx.Scale.X,
            ctx.SpriteBatch.transformMatrix, ctx.Layout, ctx.TopLeft, ctx.Scale);

        Field.Report(emitter);
        Field.Draw(ctx.SpriteBatch, emitter);
    }

    private void DrawOnSlot(in RaritySpriteContext ctx)
    {
        if (Main.dedServ || ctx.InWorld || !Enabled(FieldSurfaces.Slot))
            return;

        Vector2 half = ctx.SpriteSize * 0.5f;
        (Vector2 position, Vector2 size) = AreaOf(ctx.Center - half, ctx.Center + half, ctx.Center.Y + half.Y);

        var emitter = new FieldEmitter(ctx.Item, FieldSurfaces.Slot, position, size, ctx.Center, ctx.Scale, ctx.SpriteBatch.transformMatrix);

        Field.Report(emitter);
        Field.Draw(ctx.SpriteBatch, emitter);
    }

    /// <summary>The spawn area within the box <paramref name="min" />..<paramref name="max" />, whose baseline is at <paramref name="baseline" />.</summary>
    private (Vector2 Position, Vector2 Size) AreaOf(Vector2 min, Vector2 max, float baseline)
    {
        (Vector2 position, Vector2 size) = Area switch
        {
            EmitArea.Bounds => (min, max - min),
            EmitArea.Top => (min, new Vector2(max.X - min.X, 0f)),
            EmitArea.Center => ((min + max) * 0.5f, Vector2.Zero),
            _ => (new Vector2(min.X, baseline), new Vector2(max.X - min.X, 0f)),
        };

        return (position + Offset - Padding, size + Padding * 2f);
    }
}
