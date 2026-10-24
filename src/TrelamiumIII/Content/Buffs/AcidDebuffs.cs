using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Buffs;

public sealed class AcidDebuff : ModBuff
{
    public override string Texture => AssetReferences.Content.Buffs.AcidDebuffs.KEY;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = false;
        Main.buffNoSave[Type] = true;

        BuffID.Sets.LongerExpertDebuff[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<DebuffPlayer>().acid = true;

        Dust.NewDust(player.position, player.width, player.height, DustID.CursedTorch);
        Dust.NewDustDirect(player.position, player.width, 16, DustID.SilverFlame, newColor: new Color(202, 253, 49));
    }
}

public sealed class NitricAcidDebuff : ModBuff
{
    public override string Texture => AssetReferences.Content.Buffs.AcidDebuffs.KEY;

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.pvpBuff[Type] = false;
        Main.buffNoSave[Type] = true;

        BuffID.Sets.LongerExpertDebuff[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        player.GetModPlayer<DebuffPlayer>().nitricAcid = true;

        Dust.NewDust(player.position, player.width, player.height / 2, DustID.ToxicBubble, Scale: 0.4f);
        Dust.NewDustDirect(player.position, player.width, player.height, DustID.SilverFlame, newColor: new Color(255, 171, 87));
    }
}