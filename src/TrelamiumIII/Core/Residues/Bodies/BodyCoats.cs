using System.Collections.Generic;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Buffs;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>One residue on one body, as <see cref="BodyCanvas" /> draws it.</summary>
/// <param name="Fill">How far up the body it reaches, 0..1.</param>
/// <param name="Age">Ticks it has been on, for looks that grow.</param>
/// <param name="Arrival">When it landed, in one order shared by every body. The oldest draws first.</param>
internal readonly record struct BodyCoat(IResidue Residue, float Fill, int Age, long Arrival);

/// <summary>
///     Which residue each chemical Type wears on a body, and which debuffs count as which Type.
/// </summary>
/// <remarks>
///     This is where an affliction or a debuff gets its look: the Type's own canvas, shared with what that Type leaves on the
///     ground. Fields stay their own system and only share the Type's palette, so a burning zombie, the scorch under it and
///     the thermite cloud around it read as one fire without knowing about each other.
/// </remarks>
internal sealed class BodyResidues : ModSystem
{
    private static readonly Dictionary<int, ChemicalDamageType> debuffs = [];

    /// <summary>The residue <paramref name="type" /> draws with on a body, or null for a Type with no canvas yet.</summary>
    public static IResidue? Of(ChemicalDamageType type) => type switch
    {
        ChemicalDamageType.Thermal => ResidueSystem.Get<BurnedResidue>(),
        ChemicalDamageType.Cryonic => ResidueSystem.Get<CryonicResidue>(),
        ChemicalDamageType.Caustic => ResidueSystem.Get<AcidResidue>(),
        ChemicalDamageType.Necrotic => ResidueSystem.Get<NecroticResidue>(),
        ChemicalDamageType.Solute => ResidueSystem.Get<SlimedResidue>(),
        _ => null,
    };

    /// <summary>Has <paramref name="buffType" /> show as <paramref name="type" /> on whatever has it.</summary>
    public static void RegisterDebuff(int buffType, ChemicalDamageType type) => debuffs[buffType] = type;

    public static bool TryGetDebuff(int buffType, out ChemicalDamageType type) => debuffs.TryGetValue(buffType, out type);

    public override void PostSetupContent()
    {
        foreach (int buff in (ReadOnlySpan<int>)[BuffID.OnFire, BuffID.OnFire3, BuffID.Burning])
            RegisterDebuff(buff, ChemicalDamageType.Thermal);

        foreach (int buff in (ReadOnlySpan<int>)[BuffID.Frostburn, BuffID.Frostburn2, BuffID.Chilled, BuffID.Frozen])
            RegisterDebuff(buff, ChemicalDamageType.Cryonic);

        foreach (int buff in (ReadOnlySpan<int>)[BuffID.Poisoned, BuffID.Venom, ModContent.BuffType<AcidDebuff>(), ModContent.BuffType<NitricAcidDebuff>()])
            RegisterDebuff(buff, ChemicalDamageType.Caustic);

        RegisterDebuff(BuffID.Bleeding, ChemicalDamageType.Necrotic);
        RegisterDebuff(BuffID.Slimed, ChemicalDamageType.Solute);
    }

    public override void Unload() => debuffs.Clear();
}

/// <summary>
///     The residues on one body. Nothing here decides anything: each tick its owner says how far each Type should reach, and
///     this eases toward that, so a coat climbs when it lands and sinks back to the feet as it wears off.
/// </summary>
internal sealed class BodyCoats
{
    private static readonly int TypeCount = Enum.GetValues<ChemicalDamageType>().Length;

    private const float RISE_PER_TICK = 0.04f;

    private const float FALL_PER_TICK = 0.012f;

    /// <summary>How far up a debuff reaches. Afflictions go by their concentration, see <see cref="ChemicalGlobalNPC.CoverageOf" />.</summary>
    private const float DEBUFF_FILL = 0.5f;

    /// <summary>Ticks of a debuff left when its coat starts to sink.</summary>
    private const float DEBUFF_FADE_TICKS = 45f;

    private static long lastArrival;

    private readonly float[] fill = new float[TypeCount];

    private readonly float[] wanted = new float[TypeCount];

    private readonly int[] age = new int[TypeCount];

    private readonly long[] arrival = new long[TypeCount];

    private readonly int[] heldTicks = new int[TypeCount];

    private readonly float[] heldFill = new float[TypeCount];

    /// <summary>False lets the canvas skip a body with nothing on it.</summary>
    public bool IsCoated { get; private set; }

    public float FillOf(ChemicalDamageType type) => fill[(int)type];

    /// <summary>Says <paramref name="type" /> should reach this far up the body this tick.</summary>
    public void Want(ChemicalDamageType type, float amount)
        => wanted[(int)type] = MathF.Max(wanted[(int)type], amount);

    /// <summary>Holds <paramref name="type" /> on the body by itself for <paramref name="ticks" />, with no affliction or debuff behind it.</summary>
    public void Lay(ChemicalDamageType type, float amount, int ticks)
    {
        heldFill[(int)type] = amount;
        heldTicks[(int)type] = ticks;
    }

    public void WantDebuffs(int[] buffTypes, int[] buffTimes)
    {
        for (int i = 0; i < buffTypes.Length; i++)
        {
            if (buffTimes[i] > 0 && BodyResidues.TryGetDebuff(buffTypes[i], out ChemicalDamageType type))
                Want(type, DEBUFF_FILL * MathHelper.Clamp(buffTimes[i] / DEBUFF_FADE_TICKS, 0f, 1f));
        }
    }

    /// <summary>Takes this tick's wants and moves every coat toward them. Call once per tick, after they're all in.</summary>
    public void Step(Entity body)
    {
        bool any = false;

        for (int i = 0; i < TypeCount; i++)
        {
            if (heldTicks[i] > 0)
            {
                heldTicks[i]--;
                wanted[i] = MathF.Max(wanted[i], heldFill[i]);
            }

            float target = wanted[i];
            float current = fill[i];

            wanted[i] = 0f;

            if (current <= 0f && target <= 0f)
                continue;

            if (current <= 0f)
                arrival[i] = ++lastArrival;

            current = current < target ? MathF.Min(current + RISE_PER_TICK, target) : MathF.Max(current - FALL_PER_TICK, target);
            fill[i] = current;

            if (current <= 0f)
            {
                age[i] = 0;
                continue;
            }

            age[i]++;
            any = true;

            BodyResidues.Of((ChemicalDamageType)i)?.UpdateBody(body, current);
        }

        IsCoated = any;
    }

    /// <summary>Adds every coat with something to draw to <paramref name="into" />, oldest first.</summary>
    public void Collect(List<BodyCoat> into)
    {
        for (int i = 0; i < TypeCount; i++)
        {
            if (fill[i] > 0f && BodyResidues.Of((ChemicalDamageType)i) is { } residue)
                into.Add(new BodyCoat(residue, fill[i], age[i], arrival[i]));
        }

        into.Sort(static (a, b) => a.Arrival.CompareTo(b.Arrival));
    }
}

/// <summary>An NPC's coats: its debuffs, its afflictions and its clings, each in its Type's canvas.</summary>
internal sealed class BodyCoatNPC : GlobalNPC
{
    private static readonly ChemicalDamageType[] Types = Enum.GetValues<ChemicalDamageType>();

    public override bool InstancePerEntity => true;

    public BodyCoats Coats { get; } = new();

    public override void Load()
    {
        if (!Main.dedServ)
            On_NPC.UpdateNPC_BuffApplyVFX += WithoutDebuffDust;
    }

    /// <summary>
    ///     A debuff that shows as a coat doesn't throw vanilla's dust as well, which is heavy enough to hide it. The flags are
    ///     only put away for this call: damage has already read them, and nothing else here does.
    /// </summary>
    private static void WithoutDebuffDust(On_NPC.orig_UpdateNPC_BuffApplyVFX orig, NPC self)
    {
        (bool onFire, bool onFire3, bool onFrostBurn, bool onFrostBurn2, bool poisoned, bool venom) = (self.onFire, self.onFire3, self.onFrostBurn, self.onFrostBurn2, self.poisoned, self.venom);

        self.onFire = self.onFire3 = self.onFrostBurn = self.onFrostBurn2 = self.poisoned = self.venom = false;

        orig(self);

        (self.onFire, self.onFire3, self.onFrostBurn, self.onFrostBurn2, self.poisoned, self.venom) = (onFire, onFire3, onFrostBurn, onFrostBurn2, poisoned, venom);
    }

    public override void PostAI(NPC npc)
    {
        if (Main.dedServ)
            return;

        Coats.WantDebuffs(npc.buffType, npc.buffTime);

        ChemicalGlobalNPC chemistry = npc.GetGlobalNPC<ChemicalGlobalNPC>();

        if (chemistry.HasChemistry)
        {
            foreach (ChemicalDamageType type in Types)
                Coats.Want(type, chemistry.CoverageOf(type));
        }

        Coats.Step(npc);
    }
}

/// <summary>A player's coats. Players have no afflictions, so only debuffs show for now.</summary>
internal sealed class BodyCoatPlayer : ModPlayer
{
    public BodyCoats Coats { get; } = new();

    public override void Load()
    {
        if (!Main.dedServ)
            On_LegacyPlayerRenderer.DrawPlayerFull += WithoutDebuffDust;
    }

    /// <summary>As <see cref="BodyCoatNPC" /> does for NPCs. A player's dust comes from setting up its draw, and only the draw is hooked here.</summary>
    private static void WithoutDebuffDust(On_LegacyPlayerRenderer.orig_DrawPlayerFull orig, LegacyPlayerRenderer self, Camera camera, Player player)
    {
        (bool onFire, bool onFire3, bool onFrostBurn, bool onFrostBurn2, bool poisoned, bool venom, bool bleed) = (player.onFire, player.onFire3, player.onFrostBurn, player.onFrostBurn2, player.poisoned, player.venom, player.bleed);

        player.onFire = player.onFire3 = player.onFrostBurn = player.onFrostBurn2 = player.poisoned = player.venom = player.bleed = false;

        orig(self, camera, player);

        (player.onFire, player.onFire3, player.onFrostBurn, player.onFrostBurn2, player.poisoned, player.venom, player.bleed) = (onFire, onFire3, onFrostBurn, onFrostBurn2, poisoned, venom, bleed);
    }

    public override void PostUpdate()
    {
        if (Main.dedServ)
            return;

        Coats.WantDebuffs(Player.buffType, Player.buffTime);
        Coats.Step(Player);
    }
}
