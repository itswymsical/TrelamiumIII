namespace TrelamiumIII.Content.Vfx;

/// <summary>
///     The hand-written half of this field. The VFX Lab writes this file once and never touches it again, so anything the lab can't
///     express goes here: overrides, what it does to the world, drawing on top.
/// </summary>
/// <remarks>
///     The lab released it like this:
///     <code>
///     float angle = MathHelper.ToRadians(-123.48838f + Main.rand.NextFloat(-73.558136f, 73.558136f));
///     FieldSystem.GetLayer&lt;Field2&gt;()?.SpawnNode(position, angle.ToRotationVector2() * 4.1046515f, 76.29767f, 553.64185f);
///     </code>
/// </remarks>
internal sealed partial class Field2
{
}
