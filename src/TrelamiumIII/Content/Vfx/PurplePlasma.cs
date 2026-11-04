namespace TrelamiumIII.Content.Vfx;

/// <summary>
///     The hand-written half of this field. The VFX Lab writes this file once and never touches it again, so anything the lab can't
///     express goes here: overrides, what it does to the world, drawing on top.
/// </summary>
/// <remarks>
///     The lab released it like this:
///     <code>
///     float angle = MathHelper.ToRadians(-93.34884f + Main.rand.NextFloat(-35.88372f, 35.88372f));
///     FieldSystem.GetLayer&lt;PurplePlasma&gt;()?.SpawnNode(position, angle.ToRotationVector2() * 2.4250693f, 82.40579f, 79.6186f);
///     </code>
/// </remarks>
internal sealed partial class PurplePlasma
{
}
