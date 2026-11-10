using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using Terraria.Graphics;
using Terraria.Localization;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     A base liquid whose tiles each carry an <see cref="InternalLiquid" />. internal-types share the type's flow and
///     merge rules and differ in tint, light, strength and name, so variants cost no liquid IDs.
///     Draws the grayscale BaseLiquid set multiplied by the internal-type's tint.
/// </summary>
public abstract class CustomLiquid : ModLiquid
{
    private readonly List<InternalLiquidfall> falls = [];

    protected CustomLiquid() => CustomLiquidSet = new OwnInternalLiquid(this);

    public CustomLiquidSet CustomLiquidSet { get; }

    public IReadOnlyList<InternalLiquid> InternalLiquids => CustomLiquidSet.InternalLiquids;

    public InternalLiquid Default => CustomLiquidSet.Default;

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

        foreach (InternalLiquid internalType in InternalLiquids)
            AddFall(internalType);
    }

    private void AddFall(InternalLiquid internalType)
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
        CustomLiquidSet.Register();
        DefineItemReactions();

        LocalizedText mapName = CreateMapEntryName();

        foreach (InternalLiquid internalType in InternalLiquids)
            AddMapEntry(MapColor(internalType), mapName, (_, x, y) => InternalLiqAt(x, y).DisplayName);
    }

    public override void Unload() => CustomLiquidSet.ClearRegistry();

    /// <summary>
    ///     Adds a static internalType. Other content may add more after <see cref="DefineLiquidTypes" />, up until
    ///     SetStaticDefaults registers the set, as each solvent does to <see cref="Content.Liquids.SolventLiquid" />.
    /// </summary>
    protected InternalLiquid AddInternalLiquid(InternalLiquid internalType)
    {
        CustomLiquidSet.AddLiquid(internalType);

        if (defined)
            AddFall(internalType);

        return internalType;
    }

    /// <inheritdoc cref="CustomLiquidSet.AddMix" />
    protected void AddMix(InternalLiquid receiver, InternalLiquid donor, InternalLiquid result) => CustomLiquidSet.AddMix(receiver, donor, result);

    /// <inheritdoc cref="CustomLiquidSet.AddReaction(InternalLiquid?, InternalLiquid, int[])" />
    protected void AddReaction(InternalLiquid? from, InternalLiquid to, params int[] tiles) => CustomLiquidSet.AddReaction(from, to, tiles);

    /// <inheritdoc cref="CustomLiquidSet.AddReaction(InternalLiquid?, InternalLiquid, Predicate{int})" />
    protected void AddReaction(InternalLiquid? from, InternalLiquid to, Predicate<int> tiles) => CustomLiquidSet.AddReaction(from, to, tiles);

    /// <inheritdoc cref="CustomLiquidSet.AddCorrosion" />
    protected void AddCorrosion(InternalLiquid? from, Predicate<int> tiles, ushort? becomes = null) => CustomLiquidSet.AddCorrosion(from, tiles, becomes);

    /// <inheritdoc cref="CustomLiquidSet.AddItemReaction" />
    protected void AddItemReaction(InternalLiquid? from, ItemReaction reaction) => CustomLiquidSet.AddItemReaction(reaction);

    public InternalLiquid Get(byte index) => CustomLiquidSet.Get(index);

    public InternalLiquid? Find(string name) => CustomLiquidSet.Find(name);

    /// <inheritdoc cref="CustomLiquidSet.Resolve" />
    public InternalLiquid? Resolve(string name) => CustomLiquidSet.Resolve(name);

    /// <inheritdoc cref="CustomLiquidSet.InternalLiqAt" />
    public InternalLiquid InternalLiqAt(int i, int j) => CustomLiquidSet.InternalLiqAt(i, j);

    /// <summary>A internalType this liquid can create on demand from its name, or null. Not yet added to the liquid.</summary>
    protected virtual InternalLiquid? CreateDynamic(string name) => null;

    /// <inheritdoc cref="CustomLiquidSet.Mix" />
    public virtual byte Mix(byte receiver, byte donor) => CustomLiquidSet.MixByTable(receiver, donor);

    /// <inheritdoc cref="CustomLiquidSet.BottledItem" />
    public virtual int BottledItem(InternalLiquid internalType) => CustomLiquidSet.BottledItemByTable(internalType);

    /// <summary>Contact effect on a player in this liquid, given the internalType around them.</summary>
    protected virtual void OnPlayerContact(Player player, InternalLiquid internalType) { }

    /// <summary>Contact effect on an NPC in this liquid, given the internalType around them.</summary>
    protected virtual void OnNPCContact(NPC npc, InternalLiquid internalType) { }

    public sealed override void OnPlayerCollision(Player player) => OnPlayerContact(player, CustomLiquidSet.InternalLiqIn(player.Hitbox));

    public sealed override void OnNPCCollision(NPC npc) => OnNPCContact(npc, CustomLiquidSet.InternalLiqIn(npc.Hitbox));

    public override ushort GetMapOption(int i, int j)
    {
        byte index = InternalLiqAt(i, j).Index;
        return index < CustomLiquidSet.StaticCount ? index : (ushort)0;
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
    protected virtual Color MapColor(InternalLiquid internalType) => Multiply(internalType.Tint, new Color(150, 150, 150));

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
    private sealed class OwnInternalLiquid(CustomLiquid owner) : CustomLiquidSet
    {
        public override int LiquidType => owner.Type;

        public override string FullName => owner.FullName;

        public override string Name => owner.Name;

        public override string LiquidDisplayName(InternalLiquid internalType)
            => owner.GetLocalization("Liquids." + internalType.Name, () => internalType.Name).Value;

        protected internal override InternalLiquid? CreateDynamic(string name) => owner.CreateDynamic(name);

        public override byte Mix(byte receiver, byte donor) => owner.Mix(receiver, donor);

        public override int BottledItem(InternalLiquid internalType) => owner.BottledItem(internalType);

        protected internal override void OnPlayerContact(Player player, InternalLiquid internalType) => owner.OnPlayerContact(player, internalType);

        protected internal override void OnNPCContact(NPC npc, InternalLiquid internalType) => owner.OnNPCContact(npc, internalType);
    }
}

/// <summary>The liquidfall for one internalType: its liquid's fall sheet multiplied by the internalType's tint.</summary>
[Autoload(false)]
public sealed class InternalLiquidfall(CustomLiquid liquid, InternalLiquid internalType) : ModWaterfallStyle
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
