using MonoMod.Cil;

namespace TrelamiumIII.Common.DamageMeter;

public enum DamageMeterSource : byte
{
    Hit,

    /// <summary>Buff damage over time, dealt through <see cref="NPC.lifeRegen" />.</summary>
    Debuff,

    /// <summary>Chemical affliction ticks.</summary>
    Affliction,
}

/// <summary>
///     Feeds the DPS meter the damage vanilla never passes to <see cref="Player.addDPS" />: debuff ticks, and strikes
///     dealt through <see cref="NPC.SimpleStrikeNPC" />. <see cref="DamageMeterPlayer" /> keeps each source's share.
/// </summary>
public class DamageMeterSystem : ModSystem
{
    /// <summary>How long after the local player's last hit or exposure an NPC's debuff ticks are credited to them.</summary>
    private const uint DebuffCreditTicks = 600;

    /// <summary>Source of the <see cref="Player.addDPS" /> call in progress. Vanilla's own calls are hits.</summary>
    private static DamageMeterSource crediting;

    public override void Load()
    {
        IL_NPC.UpdateNPC_BuffApplyDOTs += CreditDebuffTicks;
        On_Player.addDPS += RecordShare;
    }

    public override void Unload()
    {
        IL_NPC.UpdateNPC_BuffApplyDOTs -= CreditDebuffTicks;
        On_Player.addDPS -= RecordShare;
    }

    /// <summary>
    ///     Adds <paramref name="damage" /> to <paramref name="owner" />'s DPS meter, on that player's machine only. A hit
    ///     also attributes the NPC's debuffs to the owner.
    /// </summary>
    public static void Credit(int owner, NPC npc, int damage, DamageMeterSource source)
    {
        if (source == DamageMeterSource.Hit)
            AttributeDebuffs(owner, npc);

        if (Main.dedServ || owner != Main.myPlayer || damage <= 0 || npc.HideStrikeDamage || !Main.LocalPlayer.accDreamCatcher)
            return;

        crediting = source;
        Main.LocalPlayer.addDPS(damage);
        crediting = DamageMeterSource.Hit;
    }

    /// <summary>
    ///     Credits <paramref name="npc" />'s debuff ticks to <paramref name="owner" /> for <see cref="DebuffCreditTicks" />.
    ///     Vanilla doesn't record who applied a debuff, so everyone recently hitting an NPC is credited with all of it.
    /// </summary>
    public static void AttributeDebuffs(int owner, NPC npc)
    {
        if (!Main.dedServ && owner == Main.myPlayer)
            BodyOf(npc).GetGlobalNPC<DamageMeterNPC>().DebuffCreditUntil = Main.GameUpdateCount + DebuffCreditTicks;
    }

    /// <summary>Worm segments share the attribution of the segment holding their life.</summary>
    private static NPC BodyOf(NPC npc) => npc.realLife >= 0 ? Main.npc[npc.realLife] : npc;

    /// <summary>Both damage-over-time loops load the tick's damage right after <see cref="CombatText.LifeRegenNegative" />.</summary>
    private void CreditDebuffTicks(ILContext il)
    {
        ILCursor c = new(il);
        int patched = 0;

        while (c.TryGotoNext(MoveType.After,
            i => i.MatchLdsfld<CombatText>(nameof(CombatText.LifeRegenNegative)),
            i => i.MatchLdloc(out _) || i.MatchLdcI4(out _)))
        {
            c.EmitDup();
            c.EmitLdarg0();
            c.EmitDelegate<Action<int, NPC>>(CreditDebuffTick);
            patched++;
        }

        if (patched == 0)
        {
            Mod.Logger.Warn("Could not patch NPC.UpdateNPC_BuffApplyDOTs, failed to match the damage-over-time combat text.");

            throw new ILPatchFailureException(Mod, il, null);
        }
    }

    /// <remarks>Counted on immortal NPCs, as vanilla counts hits on them, so target dummies register.</remarks>
    private static void CreditDebuffTick(int damage, NPC npc)
    {
        if (!Main.dedServ && Main.GameUpdateCount < BodyOf(npc).GetGlobalNPC<DamageMeterNPC>().DebuffCreditUntil)
            Credit(Main.myPlayer, npc, damage, DamageMeterSource.Debuff);
    }

    private static void RecordShare(On_Player.orig_addDPS orig, Player self, int dmg)
    {
        int before = self.dpsStarted ? self.dpsDamage : 0;

        orig(self, dmg);
        self.GetModPlayer<DamageMeterPlayer>().Record(crediting, before, dmg);
    }
}

public class DamageMeterNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    /// <summary><see cref="Main.GameUpdateCount" /> until which this NPC's debuff ticks are credited to the local player.</summary>
    public uint DebuffCreditUntil;
}
