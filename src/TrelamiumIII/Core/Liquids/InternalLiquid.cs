using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using Terraria.Graphics;
using Terraria.Localization;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     A base liquid whose tiles each carry a <see cref="InternalLiquidType" />. Conditions share the type's flow and
///     merge rules and differ in tint, light, strength and name, so variants cost no liquid IDs.
///     Draws the grayscale BaseLiquid set multiplied by the internalType's tint.
/// </summary>
public abstract class InternalLiquid : ModLiquid
{
    private readonly List<InternalLiquidfall> falls = [];

    protected InternalLiquid() => InternalLiquidSet = new OwnInternalLiquid(this);

    public InternalLiquidSet InternalLiquidSet { get; }

    public IReadOnlyList<InternalLiquidType> InternalLiquids => InternalLiquidSet.InternalLiquids;

    public InternalLiquidType Default => InternalLiquidSet.Default;

    /// <summary>Path of the liquid sheet; its _Block, _Slope and fall sheets sit beside it with those suffixes.</summary>
    public virtual string TextureBase => AssetReferences.Content.Liquids.BaseLiquid.KEY;

    public sealed override string Texture => TextureBase;

    public sealed override string BlockTexture => TextureBase + "_Block";

    public sealed override string SlopeTexture => TextureBase + "_Slope";

    /// <summary>Whether <see cref="Load" /> has run, after which each added internalType gets its liquidfall straight away.</summary>
    private bool defined;

    /// <summary>Adds internalTypes with <see cref="AddInternalLiquid" />, then mixes and reactions. The first is the default.</summary>
    protected abstract void DefineLiquidTypes();

    public override void Load()
    {
        DefineLiquidTypes();
        defined = true;

        foreach (InternalLiquidType internalType in InternalLiquids)
            AddFall(internalType);
    }

    private void AddFall(InternalLiquidType internalType)
    {
        // Waterfall styles autoload client-side only; on a dedicated server their texture array is never resized.
        if (Main.dedServ)
            return;

        var fall = new InternalLiquidfall(this, internalType);
        Mod.AddContent(fall);
        falls.Add(fall);
    }

    /// <summary>Adds item reactions with <see cref="AddItemReaction" />. Runs from SetStaticDefaults, once every item type is assigned.</summary>
    protected virtual void DefineItemReactions() { }

    public override void SetStaticDefaults()
    {
        InternalLiquidSet.Register();
        DefineItemReactions();

        LocalizedText mapName = CreateMapEntryName();

        foreach (InternalLiquidType internalType in InternalLiquids)
            AddMapEntry(MapColor(internalType), mapName, (_, x, y) => InternalLiqAt(x, y).DisplayName);
    }

    public override void Unload() => InternalLiquidSet.ClearRegistry();

    /// <summary>
    ///     Adds a static internalType. Other content may add more after <see cref="DefineLiquidTypes" />, up until
    ///     SetStaticDefaults registers the set, as each solvent does to <see cref="Content.Liquids.SolventLiquid" />.
    /// </summary>
    protected InternalLiquidType AddInternalLiquid(InternalLiquidType internalType)
    {
        InternalLiquidSet.AddCondition(internalType);

        if (defined)
            AddFall(internalType);

        return internalType;
    }

    /// <inheritdoc cref="InternalLiquidSet.AddMix" />
    protected void AddMix(InternalLiquidType receiver, InternalLiquidType donor, InternalLiquidType result) => InternalLiquidSet.AddMix(receiver, donor, result);

    /// <inheritdoc cref="InternalLiquidSet.AddReaction(InternalLiquidType?, InternalLiquidType, int[])" />
    protected void AddReaction(InternalLiquidType? from, InternalLiquidType to, params int[] tiles) => InternalLiquidSet.AddReaction(from, to, tiles);

    /// <inheritdoc cref="InternalLiquidSet.AddReaction(InternalLiquidType?, InternalLiquidType, Predicate{int})" />
    protected void AddReaction(InternalLiquidType? from, InternalLiquidType to, Predicate<int> tiles) => InternalLiquidSet.AddReaction(from, to, tiles);

    /// <inheritdoc cref="InternalLiquidSet.AddCorrosion" />
    protected void AddCorrosion(InternalLiquidType? from, Predicate<int> tiles, ushort? becomes = null) => InternalLiquidSet.AddCorrosion(from, tiles, becomes);

    /// <inheritdoc cref="InternalLiquidSet.AddItemReaction" />
    protected void AddItemReaction(InternalLiquidType? from, ItemReaction reaction) => InternalLiquidSet.AddItemReaction(reaction);

    public InternalLiquidType Get(byte index) => InternalLiquidSet.Get(index);

    public InternalLiquidType? Find(string name) => InternalLiquidSet.Find(name);

    /// <inheritdoc cref="InternalLiquidSet.Resolve" />
    public InternalLiquidType? Resolve(string name) => InternalLiquidSet.Resolve(name);

    /// <inheritdoc cref="InternalLiquidSet.InternalLiqAt" />
    public InternalLiquidType InternalLiqAt(int i, int j) => InternalLiquidSet.InternalLiqAt(i, j);

    /// <summary>A internalType this liquid can create on demand from its name, or null. Not yet added to the liquid.</summary>
    protected virtual InternalLiquidType? CreateDynamic(string name) => null;

    /// <inheritdoc cref="InternalLiquidSet.Mix" />
    public virtual byte Mix(byte receiver, byte donor) => InternalLiquidSet.MixByTable(receiver, donor);

    /// <inheritdoc cref="InternalLiquidSet.BottledItem" />
    public virtual int BottledItem(InternalLiquidType internalType) => InternalLiquidSet.BottledItemByTable(internalType);

    /// <summary>Contact effect on a player in this liquid, given the internalType around them.</summary>
    protected virtual void OnPlayerContact(Player player, InternalLiquidType internalType) { }

    /// <summary>Contact effect on an NPC in this liquid, given the internalType around them.</summary>
    protected virtual void OnNPCContact(NPC npc, InternalLiquidType internalType) { }

    public sealed override void OnPlayerCollision(Player player) => OnPlayerContact(player, InternalLiquidSet.InternalLiqIn(player.Hitbox));

    public sealed override void OnNPCCollision(NPC npc) => OnNPCContact(npc, InternalLiquidSet.InternalLiqIn(npc.Hitbox));

    public override ushort GetMapOption(int i, int j)
    {
        byte index = InternalLiqAt(i, j).Index;
        return index < InternalLiquidSet.StaticCount ? index : (ushort)0;
    }

    public override int ChooseWaterfallStyle(int i, int j)
    {
        if (falls.Count == 0)
            return base.ChooseWaterfallStyle(i, j);

        byte index = InternalLiqAt(i, j).Index;
        return falls[index < falls.Count ? index : 0].Slot;
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Vector3 light = InternalLiqAt(i, j).Light;
        r = Math.Max(r, light.X);
        g = Math.Max(g, light.Y);
        b = Math.Max(b, light.Z);
    }

    public override bool PreSlopeDraw(int i, int j, bool behindBlocks, ref Vector2 drawPosition, ref Rectangle liquidSize, ref VertexColors colors)
    {
        Tint(ref colors, InternalLiqAt(i, j).Tint);
        return true;
    }

    public override void RetroDrawEffects(int i, int j, SpriteBatch spriteBatch, ref RetroLiquidDrawInfo drawData, float liquidAmountModified, int liquidGFXQuality)
        => drawData.liquidColor = Multiply(drawData.liquidColor, InternalLiqAt(i, j).Tint);

    /// <summary>How the internalType reads on the map: the tint over the texture's mid-grey body.</summary>
    protected virtual Color MapColor(InternalLiquidType internalType) => Multiply(internalType.Tint, new Color(150, 150, 150));

    public static void Tint(ref VertexColors colors, Color tint)
    {
        colors.TopLeftColor = Multiply(colors.TopLeftColor, tint);
        colors.TopRightColor = Multiply(colors.TopRightColor, tint);
        colors.BottomLeftColor = Multiply(colors.BottomLeftColor, tint);
        colors.BottomRightColor = Multiply(colors.BottomRightColor, tint);
    }

    /// <summary>Component-wise RGB multiply, keeping <paramref name="color" />'s alpha.</summary>
    public static Color Multiply(Color color, Color tint) => new(
        (byte)(color.R * tint.R / 255),
        (byte)(color.G * tint.G / 255),
        (byte)(color.B * tint.B / 255),
        color.A);

    /// <summary>Routes the set's overridable behaviour back to the liquid's virtuals.</summary>
    private sealed class OwnInternalLiquid(InternalLiquid owner) : InternalLiquidSet
    {
        public override int LiquidType => owner.Type;

        public override string FullName => owner.FullName;

        public override string Name => owner.Name;

        public override string ConditionDisplayName(InternalLiquidType internalType)
            => owner.GetLocalization("Conditions." + internalType.Name, () => internalType.Name).Value;

        protected internal override InternalLiquidType? CreateDynamic(string name) => owner.CreateDynamic(name);

        public override byte Mix(byte receiver, byte donor) => owner.Mix(receiver, donor);

        public override int BottledItem(InternalLiquidType internalType) => owner.BottledItem(internalType);

        protected internal override void OnPlayerContact(Player player, InternalLiquidType internalType) => owner.OnPlayerContact(player, internalType);

        protected internal override void OnNPCContact(NPC npc, InternalLiquidType internalType) => owner.OnNPCContact(npc, internalType);
    }
}

/// <summary>The liquidfall for one internalType: its liquid's fall sheet multiplied by the internalType's tint.</summary>
[Autoload(false)]
public sealed class InternalLiquidfall(InternalLiquid liquid, InternalLiquidType internalType) : ModWaterfallStyle
{
    public override string Name => $"{liquid.Name}_{internalType.Name}fall";

    public override string Texture => liquid.TextureBase + "fall";

    public override void ColorMultiplier(ref float r, ref float g, ref float b, float a)
    {
        r *= internalType.Tint.R / 255f;
        g *= internalType.Tint.G / 255f;
        b *= internalType.Tint.B / 255f;
    }
}
