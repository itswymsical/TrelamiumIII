using TrelamiumIII.Core.Tiles.Seals;

namespace TrelamiumIII.Content.Seals;

/// <summary>Laid over realm structures, such as the Ligneous Fortress and the whole of Reverie.</summary>
public sealed class RealmSeal : SealType
{
    public override Vector3 PrimaryColor => new(1f, 0.72f, 0.3f);

    public override Vector3 SecondaryColor => new(0.5f, 0.9f, 0.45f);

    public override Vector3 CoreColor => new(1f, 0.96f, 0.82f);
}
