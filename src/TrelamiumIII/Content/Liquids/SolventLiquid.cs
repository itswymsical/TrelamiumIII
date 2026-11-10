using System.Collections.Generic;
using Terraria.GameContent.Liquid;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

/// <summary>
///     Every solvent as a liquid: one static <see cref="SolventLiquidInternals" /> per solvent, added through
///     <see cref="Register" /> while content loads. Different solvents flowing together turn mixed; meeting any other
///     liquid they submerge in it, as brews do.
/// </summary>
public sealed class SolventLiquid : CustomLiquid
{
    /// <summary>internal liquids registered before this liquid loaded.</summary>
    private static readonly List<SolventLiquidInternals> pending = [];

    private static SolventLiquid? loaded;

    public InternalLiquid Mixed { get; private set; } = null!;

    /// <summary>Adds a solvent's internal liquid. Call during Load; conditions are fixed once SetStaticDefaults runs.</summary>
    public static void Register(SolventLiquidInternals internals)
    {
        if (loaded is { } liquid)
            liquid.AddInternalLiquid(internals);
        else
            pending.Add(internals);
    }

    protected override void DefineLiquidTypes()
    {
        Mixed = AddInternalLiquid(new InternalLiquid("Mixed") { Tint = new Color(190, 180, 170) });

        foreach (SolventLiquidInternals condition in pending)
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

    public override int BottledItem(InternalLiquid condition) => condition is SolventLiquidInternals solvent ? solvent.ItemType() : ItemID.None;

    protected override void OnPlayerContact(Player player, InternalLiquid condition)
    {
        if (condition is SolventLiquidInternals solvent)
            solvent.OnPlayerContact(player);
    }

    protected override void OnNPCContact(NPC npc, InternalLiquid condition)
    {
        if (condition is SolventLiquidInternals solvent)
            solvent.OnNPCContact(npc);
    }
}

/// <summary>
///     A solvent's liquid. By default it only exists to be bottled; subclass it to give a solvent contact effects.
/// </summary>
/// <param name="itemType">The solvent's item type, read lazily since types are assigned after Load.</param>
public class SolventLiquidInternals(string name, Func<int> itemType) : InternalLiquid(name)
{
    public int ItemType() => itemType();

    public override string DisplayName => Lang.GetItemNameValue(itemType());

    public virtual void OnPlayerContact(Player player) { }

    public virtual void OnNPCContact(NPC npc) { }
}
