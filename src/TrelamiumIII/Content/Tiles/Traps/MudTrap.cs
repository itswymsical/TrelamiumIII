using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Tiles.Traps;

public class MudTrap : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = true;
        DustType = DustID.Mud;
        MineResist = 1f;
        HitSound = SoundID.Dig;
        AddMapEntry(new Color(113, 87, 70));
    }

    public override void NearbyEffects(int i, int j, bool closer)
    {
        base.NearbyEffects(i, j, closer);

        if (closer)
        {
            for (int h = 0; h < 2; h++)
            {
                FieldSystem.GetLayer<MudField>()?.SpawnEmitter(EmitterProfile.Default with
                {
                    Interval = 2,
                    NodeRadius = Main.rand.NextFloat(3f, 18f)
                }, new Vector2(i * 16 + 8, j * 16), Vector2.Zero);
            }
        }
    }
}