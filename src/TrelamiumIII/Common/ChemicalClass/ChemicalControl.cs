namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>Moves slower by <see cref="Slow" />; bosses by half as much. Applied by <see cref="ChemicalControlGlobalNPC" />.</summary>
public sealed class Chilled : ModBuff
{
    public const float Slow = 0.35f;

    public override string Texture => $"Terraria/Images/Buff_{BuffID.Chilled}";

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.buffNoSave[Type] = true;
    }
}

/// <summary>Frozen solid: no AI and no horizontal movement. See <see cref="CanFreeze" /> for who is immune.</summary>
public sealed class Frozen : ModBuff
{
    public override string Texture => $"Terraria/Images/Buff_{BuffID.Frozen}";

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.buffNoSave[Type] = true;

        // A shatter removes it from the releasing player's client.
        BuffID.Sets.CanBeRemovedByNetMessage[Type] = true;
    }

    /// <summary>False for bosses and for NPCs made of several (worms), whose AI can't be paused safely.</summary>
    public static bool CanFreeze(NPC npc) => !ChemicalControlGlobalNPC.IsBossLike(npc) && npc.realLife < 0;
}

/// <summary>Deals <see cref="DamageScale" /> of its damage; bosses lose half as much.</summary>
public sealed class Enfeebled : ModBuff
{
    public const float DamageScale = 0.8f;

    public override string Texture => $"Terraria/Images/Buff_{BuffID.Weak}";

    public override void SetStaticDefaults()
    {
        Main.debuff[Type] = true;
        Main.buffNoSave[Type] = true;
    }
}

/// <summary>
///     What the chemical debuffs do to an NPC, on every machine from the synced buffs. Movement is scaled after the AI and
///     restored before the next, so the AI keeps working from its own velocity; a frozen NPC skips its AI.
/// </summary>
public sealed class ChemicalControlGlobalNPC : GlobalNPC
{
    private static readonly Color FrozenTint = new(150, 215, 255);

    private static readonly Color ChilledTint = new(185, 228, 255);

    private static readonly Color EnfeebledTint = new(95, 70, 120);

    /// <summary>The scale applied after the last AI tick, undone before the next. 1 when nothing was applied.</summary>
    private float appliedScale = 1f;

    private bool scaledVertical;

    public override bool InstancePerEntity => true;

    public static bool IsBossLike(NPC npc) => npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type];

    public static bool IsFrozen(NPC npc) => npc.HasBuff<Frozen>() && Frozen.CanFreeze(npc);

    /// <summary>The fraction of its speed <paramref name="npc" /> keeps while chilled; 1 when it isn't.</summary>
    public static float SpeedScale(NPC npc)
    {
        if (!npc.HasBuff<Chilled>() || npc.realLife >= 0)
            return 1f;

        return 1f - (IsBossLike(npc) ? Chilled.Slow * 0.5f : Chilled.Slow);
    }

    public override bool PreAI(NPC npc)
    {
        if (appliedScale < 1f)
        {
            npc.velocity.X /= appliedScale;

            if (scaledVertical)
                npc.velocity.Y /= appliedScale;

            appliedScale = 1f;
        }

        return !IsFrozen(npc);
    }

    public override void PostAI(NPC npc)
    {
        if (IsFrozen(npc))
        {
            npc.velocity.X = 0f;

            if (npc.noGravity)
                npc.velocity.Y = 0f;

            return;
        }

        float scale = SpeedScale(npc);

        if (scale >= 1f)
            return;

        // Gravity is added after this, so a falling NPC keeps its fall and only walks slower.
        scaledVertical = npc.noGravity;
        npc.velocity.X *= scale;

        if (scaledVertical)
            npc.velocity.Y *= scale;

        appliedScale = scale;
    }

    public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
    {
        if (npc.HasBuff<Enfeebled>())
            modifiers.SourceDamage *= EnfeebledScale(npc);
    }

    public override void ModifyHitNPC(NPC npc, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (npc.HasBuff<Enfeebled>())
            modifiers.SourceDamage *= EnfeebledScale(npc);
    }

    public override void DrawEffects(NPC npc, ref Color drawColor)
    {
        if (IsFrozen(npc))
        {
            drawColor = Color.Lerp(drawColor, FrozenTint, 0.6f);
            Speck(npc, DustID.Frost, 8);
        }
        else if (npc.HasBuff<Chilled>())
        {
            drawColor = Color.Lerp(drawColor, ChilledTint, 0.25f);
            Speck(npc, DustID.Frost, 16);
        }

        if (npc.HasBuff<Enfeebled>())
        {
            drawColor = Color.Lerp(drawColor, EnfeebledTint, 0.2f);
            Speck(npc, DustID.Shadowflame, 18);
        }
    }

    private static float EnfeebledScale(NPC npc)
        => IsBossLike(npc) ? 1f - (1f - Enfeebled.DamageScale) * 0.5f : Enfeebled.DamageScale;

    /// <summary>One dust on the NPC, once in <paramref name="oneIn" /> frames.</summary>
    private static void Speck(NPC npc, int dustType, int oneIn)
    {
        if (!Main.rand.NextBool(oneIn))
            return;

        Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, dustType, 0f, -0.5f, 100, default, 0.9f);
        dust.noGravity = true;
        dust.velocity *= 0.4f;
    }
}

/// <summary>Defense from <see cref="HitGuardModifier" />. The amount lives on <see cref="GallantryPlayer" /> and resets when this ends.</summary>
public sealed class Gallantry : ModBuff
{
    public override string Texture => $"Terraria/Images/Buff_{BuffID.Ironskin}";

    public override void SetStaticDefaults() => Main.buffNoSave[Type] = true;

    public override void Update(Player player, ref int buffIndex) => player.statDefense += player.GetModPlayer<GallantryPlayer>().Defense;

    public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
        => tip = Description.Format(Main.LocalPlayer.GetModPlayer<GallantryPlayer>().Defense);
}

public sealed class GallantryPlayer : ModPlayer
{
    public int Defense { get; private set; }

    /// <summary>Adds <paramref name="amount" /> defense, up to <paramref name="cap" />, and restarts the buff at <paramref name="ticks" />.</summary>
    public void Raise(int amount, int cap, int ticks)
    {
        Defense = Math.Min(cap, Defense + amount);
        Player.AddBuff(ModContent.BuffType<Gallantry>(), ticks);
    }

    public override void PostUpdateBuffs()
    {
        if (!Player.HasBuff<Gallantry>())
            Defense = 0;
    }
}
