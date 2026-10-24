namespace TrelamiumIII.Content.Buffs;

public sealed class DebuffPlayer : ModPlayer
{
    public bool nitricAcid;
    public bool acid;

    public override void ResetEffects()
    {
        nitricAcid = false;
        acid = false;
    }

    public override void UpdateBadLifeRegen()
    {
        if (nitricAcid)
        {
            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= 16;
        }

        if (acid)
        {
            if (Player.lifeRegen > 0)
                Player.lifeRegen = 0;

            Player.lifeRegenTime = 0;
            Player.lifeRegen -= 14;
        }
    }
}

public sealed class DebuffNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;
    public bool nitricAcid;
    public bool acid;
    public override void ResetEffects(NPC npc)
    {
        nitricAcid = false;
        acid = false;
    }

    public override void UpdateLifeRegen(NPC npc, ref int damage)
    {
        if (nitricAcid)
        {
            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= 16;
        }

        if (acid)
        {
            if (npc.lifeRegen > 0)
                npc.lifeRegen = 0;

            npc.lifeRegen -= 14;
        }
    }
}