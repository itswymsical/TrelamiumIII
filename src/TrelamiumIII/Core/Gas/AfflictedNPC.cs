using System.Collections.Generic;

namespace TrelamiumIII.Core.Gas;

public class AfflictedNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    public Dictionary<ChemicalGasField, int> ClingingGases = new();

    public void AddCling(ChemicalGasField layer, int duration)
    {
        if (layer == null) return;

        if (ClingingGases.ContainsKey(layer))
            ClingingGases[layer] = Math.Max(ClingingGases[layer], duration);
        else
            ClingingGases[layer] = duration;
    }

    public override void PostAI(NPC npc)
    {
        if (ClingingGases.Count == 0) return;

        var keys = new List<ChemicalGasField>(ClingingGases.Keys);

        foreach (var layer in keys)
        {
            ClingingGases[layer]--;

            float intensity = Math.Min(1f, ClingingGases[layer] / 60f);

            float spreadX = npc.width * MathHelper.Lerp(0.25f, 0.55f, intensity);
            float spreadY = npc.height * MathHelper.Lerp(0.25f, 0.55f, intensity);
            Vector2 position = npc.Center + Main.rand.NextVector2Circular(spreadX, spreadY);
            Vector2 velocity = npc.velocity * 0.85f + new Vector2(0f, -0.2f) + Main.rand.NextVector2Circular(0.4f, 0.4f);

            layer.SpawnClingNode(
                position: position,
                velocity: velocity,
                radius: MathHelper.Lerp(2f, 4f, intensity) * Main.rand.NextFloat(0.75f, 1.25f),
                life: MathHelper.Lerp(10f, 20f, intensity)
            );

            if (ClingingGases[layer] <= 0)
                ClingingGases.Remove(layer);
        }
    }
}