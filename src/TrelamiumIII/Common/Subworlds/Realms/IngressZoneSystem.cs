using SubworldLibrary;

namespace TrelamiumIII.Common.Subworlds.Realms;

/// <summary>
/// No ocean in Ingress: <see cref="WorldGen.oceanDepths"/> is false there, which removes the beach zone near the world
/// edges along with its music, background and spawns. Hell is kept out by generation; see <c>IngressRealm.ContentBottom</c>.
/// </summary>
public sealed class IngressZoneSystem : ModSystem
{
    public override void Load() => On_WorldGen.oceanDepths += NoOceanInIngress;

    public override void Unload() => On_WorldGen.oceanDepths -= NoOceanInIngress;

    private static bool NoOceanInIngress(On_WorldGen.orig_oceanDepths orig, int x, int y) => !SubworldSystem.IsActive<IngressRealm>() && orig(x, y);
}
