using System.Collections.Generic;
using Terraria.GameContent.Liquid;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Every solvent as a liquid: one static <see cref="SolventCondition" /> per solvent, added through
///     <see cref="Register" /> while content loads. Different solvents flowing together turn mixed; meeting any other
///     liquid they submerge in it, as brews do.
/// </summary>
public sealed class SolventLiquid : InternalLiquid
{
    /// <summary>Conditions registered before this liquid loaded.</summary>
    private static readonly List<SolventCondition> pending = [];

    private static SolventLiquid? loaded;

    public InternalLiquidType Mixed { get; private set; } = null!;

    /// <summary>Adds a solvent's condition. Call during Load; conditions are fixed once SetStaticDefaults runs.</summary>
    public static void Register(SolventCondition condition)
    {
        if (loaded is { } liquid)
            liquid.AddInternalLiquid(condition);
        else
            pending.Add(condition);
    }

    protected override void DefineLiquidTypes()
    {
        Mixed = AddInternalLiquid(new InternalLiquidType("Mixed") { Tint = new Color(190, 180, 170) });

        foreach (SolventCondition condition in pending)
            AddInternalLiquid(condition);

        pending.Clear();
    }

    public override void Load()
    {
        base.Load();
        loaded = this;
    }

    public override void Unload()
    {
        loaded = null;
        pending.Clear();
        base.Unload();
    }

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        LiquidRenderer.DEFAULT_OPACITY[Type] = 0.7f;
        SlopeOpacity = 0.6f;
        ChecksForDrowning = true;
        AllowEmitBreathBubbles = true;
    }

    public override byte Mix(byte receiver, byte donor) => receiver == donor ? receiver : Mixed.Index;

    public override int BottledItem(InternalLiquidType condition) => condition is SolventCondition solvent ? solvent.ItemType() : ItemID.None;

    protected override void OnPlayerContact(Player player, InternalLiquidType condition)
    {
        if (condition is SolventCondition solvent)
            solvent.OnPlayerContact(player);
    }

    protected override void OnNPCContact(NPC npc, InternalLiquidType condition)
    {
        if (condition is SolventCondition solvent)
            solvent.OnNPCContact(npc);
    }
}

/// <summary>
///     A solvent's liquid. By default it only exists to be bottled; subclass it to give a solvent contact effects.
/// </summary>
/// <param name="itemType">The solvent's item type, read lazily since types are assigned after Load.</param>
public class SolventCondition(string name, Func<int> itemType) : InternalLiquidType(name)
{
    public int ItemType() => itemType();

    public override string DisplayName => Lang.GetItemNameValue(itemType());

    public virtual void OnPlayerContact(Player player) { }

    public virtual void OnNPCContact(NPC npc) { }
}
