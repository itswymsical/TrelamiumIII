namespace TrelamiumIII.Core.Graphics.Interfaces;

/// <summary>
/// Interface for projectiles and NPCs that draw vertex primitives outside the sprite batch.
/// <para>
/// <c>PrimitiveRenderer</c> calls <see cref="DrawPrimitives"/> once per frame for every active
/// entity implementing this, after dust and before the sprite batch resumes.
/// </para>
/// </summary>
public interface IDrawPrimitive
{
    void DrawPrimitives();
}
