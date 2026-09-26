using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;

namespace TrelamiumIII.Core.Gas;

/// <summary>A cling that throws a splash of its gas up off the host as it attaches, then rides it like the default.</summary>
public sealed class SplashCling : ClingBehaviour
{
    private readonly int minCount;
    private readonly int maxCount;
    private readonly float radius;
    private readonly float life;
    private readonly SoundStyle sound;
    private readonly int shake;

    /// <param name="maxCount">Exclusive.</param>
    public SplashCling(int minCount, int maxCount, float radius, float life, SoundStyle sound, int shake = 0)
    {
        this.minCount = minCount;
        this.maxCount = maxCount;
        this.radius = radius;
        this.life = life;
        this.sound = sound;
        this.shake = shake;
    }

    public override void OnAttach(NPC host, ref ClingState cling)
    {
        if (cling.Medium is not { } medium)
            return;

        Vector2 impact = host.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splash = Vector2.Lerp(impact, -Vector2.UnitY, 0.5f).SafeNormalize(-Vector2.UnitY);

        if (shake > 0)
            CameraSystem.shake = shake;

        SoundEngine.PlaySound(sound, host.Center);

        int count = Main.rand.Next(minCount, maxCount);

        for (int i = 0; i < count; i++)
        {
            Vector2 velocity = splash.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(4f, 7f);

            medium.SpawnNode(host.Center, velocity, radius, life, owner: cling.Owner, payload: cling.Payload);
        }
    }
}
