using System.Collections.Generic;
using ModLiquidLib.ModLoader;
using ModLiquidLib.Utils.Structs;
using Terraria.Graphics;
using Terraria.Localization;

namespace TrelamiumIII.Core.Liquids;

/// <summary>
///     A base liquid whose tiles each carry a <see cref="LiquidCondition" />. Conditions share the type's flow and
///     merge rules and differ in tint, light, strength and name, so variants cost no liquid IDs.
///     Draws the grayscale BaseLiquid set multiplied by the condition's tint.
/// </summary>
public abstract class ConditionalLiquid : ModLiquid
{
    private readonly List<ConditionLiquidfall> falls = [];

    protected ConditionalLiquid() => ConditionSet = new OwnConditions(this);

    public LiquidConditionSet ConditionSet { get; }

    public IReadOnlyList<LiquidCondition> Conditions => ConditionSet.Conditions;

    public LiquidCondition Default => ConditionSet.Default;

    /// <summary>Path of the liquid sheet; its _Block, _Slope and fall sheets sit beside it with those suffixes.</summary>
    public virtual string TextureBase => AssetReferences.Content.Liquids.BaseLiquid.KEY;

    public sealed override string Texture => TextureBase;

    public sealed override string BlockTexture => TextureBase + "_Block";

    public sealed override string SlopeTexture => TextureBase + "_Slope";

    /// <summary>Adds conditions with <see cref="AddCondition" />, then mixes and reactions. The first is the default.</summary>
    protected abstract void DefineConditions();

    public override void Load()
    {
        DefineConditions();

        // Waterfall styles autoload client-side only; on a dedicated server their texture array is never resized.
        if (Main.dedServ)
            return;

        foreach (LiquidCondition condition in Conditions)
        {
            var fall = new ConditionLiquidfall(this, condition);
            Mod.AddContent(fall);
            falls.Add(fall);
        }
    }

    /// <summary>Adds item reactions with <see cref="AddItemReaction" />. Runs from SetStaticDefaults, once every item type is assigned.</summary>
    protected virtual void DefineItemReactions() { }

    public override void SetStaticDefaults()
    {
        ConditionSet.Register();
        DefineItemReactions();

        LocalizedText mapName = CreateMapEntryName();

        foreach (LiquidCondition condition in Conditions)
            AddMapEntry(MapColor(condition), mapName, (_, x, y) => ConditionAt(x, y).DisplayName);
    }

    public override void Unload() => LiquidConditionSet.ClearRegistry();

    protected LiquidCondition AddCondition(LiquidCondition condition) => ConditionSet.AddCondition(condition);

    /// <inheritdoc cref="LiquidConditionSet.AddMix" />
    protected void AddMix(LiquidCondition receiver, LiquidCondition donor, LiquidCondition result) => ConditionSet.AddMix(receiver, donor, result);

    /// <inheritdoc cref="LiquidConditionSet.AddReaction(LiquidCondition?, LiquidCondition, int[])" />
    protected void AddReaction(LiquidCondition? from, LiquidCondition to, params int[] tiles) => ConditionSet.AddReaction(from, to, tiles);

    /// <inheritdoc cref="LiquidConditionSet.AddReaction(LiquidCondition?, LiquidCondition, Predicate{int})" />
    protected void AddReaction(LiquidCondition? from, LiquidCondition to, Predicate<int> tiles) => ConditionSet.AddReaction(from, to, tiles);

    /// <inheritdoc cref="LiquidConditionSet.AddCorrosion" />
    protected void AddCorrosion(LiquidCondition? from, Predicate<int> tiles, ushort? becomes = null) => ConditionSet.AddCorrosion(from, tiles, becomes);

    /// <inheritdoc cref="LiquidConditionSet.AddItemReaction" />
    protected void AddItemReaction(ItemReaction reaction) => ConditionSet.AddItemReaction(reaction);

    public LiquidCondition Get(byte index) => ConditionSet.Get(index);

    public LiquidCondition? Find(string name) => ConditionSet.Find(name);

    /// <inheritdoc cref="LiquidConditionSet.Resolve" />
    public LiquidCondition? Resolve(string name) => ConditionSet.Resolve(name);

    /// <inheritdoc cref="LiquidConditionSet.ConditionAt" />
    public LiquidCondition ConditionAt(int i, int j) => ConditionSet.ConditionAt(i, j);

    /// <summary>A condition this liquid can create on demand from its name, or null. Not yet added to the liquid.</summary>
    protected virtual LiquidCondition? CreateDynamic(string name) => null;

    /// <inheritdoc cref="LiquidConditionSet.Mix" />
    public virtual byte Mix(byte receiver, byte donor) => ConditionSet.MixByTable(receiver, donor);

    /// <inheritdoc cref="LiquidConditionSet.BottledItem" />
    public virtual int BottledItem(LiquidCondition condition) => 0;

    /// <summary>Contact effect on a player in this liquid, given the condition around them.</summary>
    protected virtual void OnPlayerContact(Player player, LiquidCondition condition) { }

    /// <summary>Contact effect on an NPC in this liquid, given the condition around them.</summary>
    protected virtual void OnNPCContact(NPC npc, LiquidCondition condition) { }

    public sealed override void OnPlayerCollision(Player player) => OnPlayerContact(player, ConditionSet.ConditionIn(player.Hitbox));

    public sealed override void OnNPCCollision(NPC npc) => OnNPCContact(npc, ConditionSet.ConditionIn(npc.Hitbox));

    public override ushort GetMapOption(int i, int j)
    {
        byte index = ConditionAt(i, j).Index;
        return index < ConditionSet.StaticCount ? index : (ushort)0;
    }

    public override int ChooseWaterfallStyle(int i, int j)
    {
        if (falls.Count == 0)
            return base.ChooseWaterfallStyle(i, j);

        byte index = ConditionAt(i, j).Index;
        return falls[index < falls.Count ? index : 0].Slot;
    }

    public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
    {
        Vector3 light = ConditionAt(i, j).Light;
        r = Math.Max(r, light.X);
        g = Math.Max(g, light.Y);
        b = Math.Max(b, light.Z);
    }

    public override bool PreSlopeDraw(int i, int j, bool behindBlocks, ref Vector2 drawPosition, ref Rectangle liquidSize, ref VertexColors colors)
    {
        Tint(ref colors, ConditionAt(i, j).Tint);
        return true;
    }

    public override void RetroDrawEffects(int i, int j, SpriteBatch spriteBatch, ref RetroLiquidDrawInfo drawData, float liquidAmountModified, int liquidGFXQuality)
        => drawData.liquidColor = Multiply(drawData.liquidColor, ConditionAt(i, j).Tint);

    /// <summary>How the condition reads on the map: the tint over the texture's mid-grey body.</summary>
    protected virtual Color MapColor(LiquidCondition condition) => Multiply(condition.Tint, new Color(150, 150, 150));

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
    private sealed class OwnConditions(ConditionalLiquid owner) : LiquidConditionSet
    {
        public override int LiquidType => owner.Type;

        public override string FullName => owner.FullName;

        public override string Name => owner.Name;

        public override string ConditionDisplayName(LiquidCondition condition)
            => owner.GetLocalization("Conditions." + condition.Name, () => condition.Name).Value;

        protected internal override LiquidCondition? CreateDynamic(string name) => owner.CreateDynamic(name);

        public override byte Mix(byte receiver, byte donor) => owner.Mix(receiver, donor);

        public override int BottledItem(LiquidCondition condition) => owner.BottledItem(condition);

        protected internal override void OnPlayerContact(Player player, LiquidCondition condition) => owner.OnPlayerContact(player, condition);

        protected internal override void OnNPCContact(NPC npc, LiquidCondition condition) => owner.OnNPCContact(npc, condition);
    }
}

/// <summary>The liquidfall for one condition: its liquid's fall sheet multiplied by the condition's tint.</summary>
[Autoload(false)]
public sealed class ConditionLiquidfall(ConditionalLiquid liquid, LiquidCondition condition) : ModWaterfallStyle
{
    public override string Name => $"{liquid.Name}_{condition.Name}fall";

    public override string Texture => liquid.TextureBase + "fall";

    public override void ColorMultiplier(ref float r, ref float g, ref float b, float a)
    {
        r *= condition.Tint.R / 255f;
        g *= condition.Tint.G / 255f;
        b *= condition.Tint.B / 255f;
    }
}
