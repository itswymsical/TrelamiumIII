using System.Collections.Generic;
using Terraria.Graphics;
using Terraria.Graphics.Renderers;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Buffs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Core.Residues.Bodies;

/// <summary>Where a body is and what a <see cref="BodyGrid" /> stretches over.</summary>
/// <param name="Middle">The middle of the sprite in the world.</param>
/// <param name="Width">The sprite's width in pixels, as near as can be told without drawing it.</param>
/// <param name="Height">Pixels from the top of the sprite to its feet.</param>
/// <param name="Slot">Pixels square that the whole of it should fit in, however it's turned.</param>
/// <param name="GravDir">-1 for a body with its feet on the ceiling.</param>
/// <param name="Direction">Which way it's facing, which mirrors its grid so a coat stays on the same side.</param>
internal readonly record struct BodyBox(Vector2 Middle, int Width, int Height, int Slot, float GravDir, int Direction)
{
    private const int MIN_SLOT = 96;

    /// <summary>Anything bigger is cut off at this.</summary>
    private const int MAX_SLOT = 512;

    private const int PLAYER_SLOT = 192;

    private const int MOUNTED_SLOT = 320;

    /// <summary>A standing player's sprite runs past its hitbox.</summary>
    private const int PLAYER_WIDTH = 32;

    private const int PLAYER_HEIGHT = 52;

    private const int MOUNTED_WIDTH = 96;

    public static BodyBox Of(Entity body)
    {
        if (body is Player player)
        {
            return new BodyBox(
                player.Center + new Vector2(0f, player.gfxOffY),
                player.mount.Active ? MOUNTED_WIDTH : PLAYER_WIDTH,
                PLAYER_HEIGHT,
                player.mount.Active ? MOUNTED_SLOT : PLAYER_SLOT,
                player.gravDir,
                player.direction);
        }

        var npc = (NPC)body;
        Vector2 middle = npc.Center + npc.netOffset + new Vector2(0f, npc.gfxOffY);
        int direction = npc.spriteDirection != 0 ? npc.spriteDirection : npc.direction;

        if (TerrarianModel.Of(npc) is not null)
            return new BodyBox(middle, PLAYER_WIDTH, PLAYER_HEIGHT, PLAYER_SLOT, 1f, direction);

        // The frame is what's really drawn. The hitbox covers NPCs with a custom draw and no frame set.
        float width = MathF.Max(npc.width, npc.frame.Width * npc.scale);
        float height = MathF.Max(npc.height, npc.frame.Height * npc.scale);

        // Half as much again, as the widest it can be once rotated, plus room for a sprite hung off-centre.
        int slot = Math.Clamp((int)(MathF.Max(width, height) * 1.5f) + 32, MIN_SLOT, MAX_SLOT);

        return new BodyBox(middle, Math.Max((int)width, 8), Math.Max((int)height, 8), slot, 1f, direction);
    }

    /// <summary>Where a point in the world falls on the body's grid, clamped to it.</summary>
    public (float U, float V) ToGrid(Vector2 world)
    {
        float u = MathHelper.Clamp((world.X - (Middle.X - Width * 0.5f)) / Width, 0f, 1f);
        float v = MathHelper.Clamp((world.Y - (Middle.Y - Height * 0.5f)) / Height, 0f, 1f);

        return (Direction < 0 ? 1f - u : u, GravDir < 0f ? 1f - v : v);
    }
}

/// <summary>One residue on one body.</summary>
internal sealed class BodyCoat(IResidue residue, long arrival)
{
    public IResidue Residue { get; } = residue;

    public BodyGrid Grid { get; } = new();

    /// <summary>When it landed, in one order shared by every body. The oldest draws first.</summary>
    public long Arrival { get; } = arrival;

    /// <summary>Ticks it has been on, for looks that grow.</summary>
    public int Age;

    /// <summary>This coat's own colour, or transparent for the residue's palette as authored.</summary>
    public Color Tint;

    /// <summary>How much of the body this tick's asking says it should cover. Spent each tick.</summary>
    public float Wanted;

    public float HeldFill;

    public int HeldTicks;

    public int HeldFrom;

    /// <summary>Whether anything has said where it is yet. If not, it starts at the feet.</summary>
    public bool HasBeenTouched;

    /// <summary>Tick before which a touch isn't worth telling the other machines about.</summary>
    public uint NextShare;

    /// <summary>What's come off it and not gone yet. Purely for show, and different on every machine.</summary>
    public readonly List<BodyNode> Nodes = [];

    /// <summary>The fraction of a node owed so far.</summary>
    private float shedding;

    /// <summary>The square root of a person's width by height, which sizes how much a body sheds against one.</summary>
    private const float PERSON_SIZE = 41f;

    /// <summary>Tries at finding somewhere with enough on it to shed from.</summary>
    private const int SHED_TRIES = 6;

    /// <summary>A cell with less than this has too little to give anything off.</summary>
    private const float SHEDS_ABOVE = 0.25f;

    private const int MIN_NODES = 12;

    private const int MAX_NODES = 72;

    /// <summary>Moves what's come off already and gives off what's due.</summary>
    public void UpdateNodes(Entity body)
    {
        if (Residue.BodyShed is not { } shed)
            return;

        BodyBox box = BodyBox.Of(body);
        float margin = box.Slot * 0.5f;

        for (int i = Nodes.Count - 1; i >= 0; i--)
        {
            BodyNode node = Nodes[i];

            node.Velocity = (node.Velocity + shed.Pull * new Vector2(1f, box.GravDir) + new Vector2(Main.windSpeedCurrent * shed.Wind, 0f)) * shed.Drag;
            node.Offset += node.Velocity - body.velocity * (1f - shed.Cling);
            node.Life--;

            // The slot's edge is as far as it can be drawn, so it fades out before getting there.
            float reach = MathF.Max(MathF.Abs(node.Offset.X), MathF.Abs(node.Offset.Y)) + node.RadiusNow * 2f;
            Vector2 world = box.Middle + node.Offset;

            if (node.Life <= 0f || reach >= margin || WorldGen.SolidTile((int)(world.X / 16f), (int)(world.Y / 16f)))
                Nodes.RemoveAt(i);
            else
                Nodes[i] = node;
        }

        float scale = MathF.Sqrt(box.Width * box.Height) / PERSON_SIZE;

        shedding += shed.Rate * Grid.Coverage * scale;

        int room = Math.Clamp((int)(MIN_NODES * scale), MIN_NODES, MAX_NODES);

        while (shedding >= 1f)
        {
            shedding--;

            if (Nodes.Count < room && TryPickCell(out int x, out int y, out float amount))
                Nodes.Add(Shed(in shed, in box, x, y, amount, scale));
        }
    }

    private bool TryPickCell(out int x, out int y, out float amount)
    {
        for (int attempt = 0; attempt < SHED_TRIES; attempt++)
        {
            x = Main.rand.Next(BodyGrid.COLUMNS);
            y = Main.rand.Next(BodyGrid.ROWS);
            amount = Grid.At(x, y);

            if (amount >= SHEDS_ABOVE)
                return true;
        }

        x = y = 0;
        amount = 0f;

        return false;
    }

    private static BodyNode Shed(in BodyShed shed, in BodyBox box, int x, int y, float amount, float scale)
    {
        Vector2 cell = new Vector2(box.Width / (float)BodyGrid.COLUMNS, box.Height / (float)BodyGrid.ROWS);
        Vector2 place = new Vector2((x + Main.rand.NextFloat()) * cell.X - box.Width * 0.5f, (y + Main.rand.NextFloat()) * cell.Y - box.Height * 0.5f);
        float life = shed.Life * Main.rand.NextFloat(0.6f, 1f);

        return new BodyNode
        {
            Offset = place * new Vector2(box.Direction < 0 ? -1f : 1f, box.GravDir),
            Velocity = (shed.Launch + Main.rand.NextVector2Circular(shed.Scatter, shed.Scatter)) * new Vector2(1f, box.GravDir),
            Life = life,
            MaxLife = life,
            Radius = shed.Radius * MathF.Sqrt(MathF.Max(scale, 1f)) * Main.rand.NextFloat(0.75f, 1.25f),
            Amount = amount,
        };
    }
}

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
///     The residues on one body. Each tick its owner says how much of the body each should cover and anything that touches it
///     says where, and each coat's grid spreads from there until it covers that much.
/// </summary>
internal sealed class BodyCoats
{
    /// <summary>How much of the body a debuff covers. Afflictions go by their concentration, see <see cref="ChemicalGlobalNPC.CoverageOf" />.</summary>
    private const float DEBUFF_FILL = 0.5f;

    /// <summary>Ticks of a debuff left when its coat starts to thin.</summary>
    private const float DEBUFF_FADE_TICKS = 45f;

    private static long lastArrival;

    private readonly List<BodyCoat> coats = [];

    /// <summary>False lets the canvas skip a body with nothing on it.</summary>
    public bool IsCoated => coats.Count > 0;

    /// <summary>Every coat on the body, oldest first.</summary>
    public IReadOnlyList<BodyCoat> All => coats;

    private BodyCoat? Find(IResidue residue)
    {
        foreach (BodyCoat coat in coats)
        {
            if (coat.Residue == residue)
                return coat;
        }

        return null;
    }

    /// <summary>Coats always arrive at the end, which keeps the list in arrival order.</summary>
    private BodyCoat Ensure(IResidue residue)
    {
        BodyCoat? coat = Find(residue);

        if (coat is null)
            coats.Add(coat = new BodyCoat(residue, ++lastArrival));

        return coat;
    }

    /// <summary>How much of the body a residue covers now, 0..1.</summary>
    public float FillOf(ChemicalDamageType type) => BodyResidues.Of(type) is { } residue && Find(residue) is { } coat ? coat.Grid.Coverage : 0f;

    /// <summary>Says <paramref name="type" /> should cover this much of the body this tick.</summary>
    public void Want(ChemicalDamageType type, float amount)
    {
        if (amount <= 0f || BodyResidues.Of(type) is not { } residue)
            return;

        BodyCoat coat = Ensure(residue);

        coat.Wanted = MathF.Max(coat.Wanted, amount);
    }

    /// <summary>Holds <paramref name="type" /> on the body by itself for <paramref name="ticks" />, with no affliction or debuff behind it.</summary>
    /// <param name="from">
    ///     The machine asking. Several can hold up one coat, as two players' afflictions do. The highest stands, and only
    ///     the machine that set it can bring it down early, so they don't take turns.
    /// </param>
    public void Lay(ChemicalDamageType type, float amount, int ticks, int from = -1)
    {
        if (BodyResidues.Of(type) is not { } residue)
            return;

        // Nothing to take back from a coat that isn't there.
        BodyCoat? coat = amount > 0f ? Ensure(residue) : Find(residue);

        if (coat is null || (coat.HeldTicks > 0 && coat.HeldFrom != from && amount < coat.HeldFill))
            return;

        coat.HeldFill = amount;
        coat.HeldTicks = ticks;
        coat.HeldFrom = from;
    }

    /// <summary>Puts some of a residue on the body at a point of its grid.</summary>
    /// <param name="radius">As a share of the body's height.</param>
    /// <param name="amount">What the middle of it gets, 0..1.</param>
    public BodyCoat Touch(IResidue residue, float u, float v, float radius, float amount, Color tint = default)
    {
        BodyCoat coat = Ensure(residue);

        coat.Grid.Touch(u, v, radius, amount);
        coat.HasBeenTouched = true;

        if (tint.A > 0)
            coat.Tint = tint;

        return coat;
    }

    public void WantDebuffs(int[] buffTypes, int[] buffTimes)
    {
        for (int i = 0; i < buffTypes.Length; i++)
        {
            if (buffTimes[i] > 0 && BodyResidues.TryGetDebuff(buffTypes[i], out ChemicalDamageType type))
                Want(type, DEBUFF_FILL * MathHelper.Clamp(buffTimes[i] / DEBUFF_FADE_TICKS, 0f, 1f));
        }
    }

    /// <summary>Takes this tick's wants and moves every coat by them. Call once per tick, after they're all in.</summary>
    public void Step(Entity body)
    {
        for (int i = coats.Count - 1; i >= 0; i--)
        {
            BodyCoat coat = coats[i];
            BodyFlow flow = coat.Residue.BodyFlow;

            if (coat.HeldTicks > 0)
            {
                coat.HeldTicks--;
                coat.Wanted = MathF.Max(coat.Wanted, coat.HeldFill);
            }

            bool held = coat.Wanted > 0f;
            float budget = MathF.Max(coat.Wanted, flow.Reach);

            coat.Wanted = 0f;

            // Asked for with no word on where.
            if (held && !coat.HasBeenTouched)
            {
                coat.Grid.Seed(in flow);
                coat.HasBeenTouched = true;
            }

            coat.Grid.Step(in flow, budget, held);
            coat.UpdateNodes(body);

            if (coat.Grid.IsEmpty && coat.Nodes.Count == 0)
            {
                coats.RemoveAt(i);
                continue;
            }

            coat.Age++;
            coat.Residue.UpdateBody(body, coat.Grid.Coverage);
        }
    }
}

/// <summary>An NPC's coats: its debuffs, its afflictions and its clings, each in its Type's canvas.</summary>
internal sealed class BodyCoatNPC : GlobalNPC
{
    private static readonly ChemicalDamageType[] Types = Enum.GetValues<ChemicalDamageType>();

    /// <summary>Steps of coverage worth telling anyone about. Easing hides the rest.</summary>
    private const int SHARED_LEVELS = 8;

    /// <summary>Ticks between telling the other machines an unchanged coverage still stands.</summary>
    private const int SHARE_TICKS = 60;

    /// <summary>What that holds a coat for past the next one being due, so a late packet doesn't make it dip.</summary>
    private const int SHARE_SLACK = 30;

    private readonly int[] sharedLevels = new int[Types.Length];

    private readonly int[] shareCooldowns = new int[Types.Length];

    /// <summary>Whether any coverage is out there still to be taken back.</summary>
    private bool sharing;

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
        ChemicalGlobalNPC chemistry = npc.GetGlobalNPC<ChemicalGlobalNPC>();

        if (Main.netMode != NetmodeID.SinglePlayer && (chemistry.HasChemistry || sharing))
            ShareChemistry(npc, chemistry);

        if (Main.dedServ)
            return;

        Coats.WantDebuffs(npc.buffType, npc.buffTime);

        if (chemistry.HasChemistry)
        {
            foreach (ChemicalDamageType type in Types)
                Coats.Want(type, chemistry.CoverageOf(type));
        }

        Coats.Step(npc);
    }

    /// <summary>
    ///     Afflictions and clings only exist on the machine that dealt them, which is the server for chemistry nobody
    ///     released. Debuffs need none of this, as vanilla syncs them. The damage stays where it is: this only says how much
    ///     of the body to show.
    /// </summary>
    private void ShareChemistry(NPC npc, ChemicalGlobalNPC chemistry)
    {
        bool any = false;

        foreach (ChemicalDamageType type in Types)
        {
            int i = (int)type;
            int level = (int)MathF.Ceiling(chemistry.CoverageOf(type) * SHARED_LEVELS);

            if (level == 0 && sharedLevels[i] == 0)
                continue;

            any |= level > 0;

            if (level == sharedLevels[i] && --shareCooldowns[i] > 0)
                continue;

            sharedLevels[i] = level;
            shareCooldowns[i] = SHARE_TICKS;

            BodyCoatNet.Send(npc, type, level / (float)SHARED_LEVELS, SHARE_TICKS + SHARE_SLACK);
        }

        sharing = any;
    }
}

/// <summary>A player's coats. Players have no afflictions, so only debuffs and what they touch show for now.</summary>
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
