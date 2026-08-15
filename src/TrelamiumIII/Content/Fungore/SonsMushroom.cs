using System.Collections.Generic;
using System.IO;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A mushroom that came from your son, and is therefore worth more than a mushroom.
/// </summary>
/// <remarks>
///     The gift rides on the item instance rather than on an item type of its own, so it stays a
///     mushroom everywhere that matters -- <see cref="MyclobutanilPlayer" />'s surge included -- and
///     differs only in what it heals and what it is called.
///     <para>
///         Everything the flag implies is applied through <see cref="Bless" />, and the save, network
///         and creation paths all go through it, so a mushroom that arrives from a world file or from
///         another client ends up identical to one grown on the spot.
///     </para>
/// </remarks>
public sealed class SonsMushroom : GlobalItem
{
    /// <summary>Against the vanilla mushroom's fifteen.</summary>
    public const int HEAL_LIFE = 45;

    // Assigned in SetStaticDefaults, which runs long before anything can hold a mushroom.
    private static LocalizedText displayName = null!;

    private static LocalizedText flavour = null!;

    public override bool InstancePerEntity => true;

    /// <summary>
    ///     The type is only known once the item has run its defaults, so the check has to wait for
    ///     late instantiation.
    /// </summary>
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        => lateInstantiation && MyclobutanilMushroom.Mushrooms.Contains(entity.type);

    public bool FromSon { get; private set; }

    public override void SetStaticDefaults()
    {
        displayName = Language.GetOrRegister("Mods.TrelamiumIII.Misc.SonsMushroom", () => "Son's Mushroom");
        flavour = Language.GetOrRegister("Mods.TrelamiumIII.Misc.SonsMushroomFlavour", () => "Grown for you, by your son");
    }

    /// <summary>A fresh Son's Mushroom, ready to be handed over.</summary>
    public static Item Create(int stack = 1)
    {
        var mushroom = new Item();

        mushroom.SetDefaults(ItemID.Mushroom);
        mushroom.stack = stack;

        if (mushroom.TryGetGlobalItem(out SonsMushroom son))
            son.Bless(mushroom);

        return mushroom;
    }

    /// <summary>Marks <paramref name="item" /> as a gift, and applies everything that changes.</summary>
    private void Bless(Item item)
    {
        FromSon = true;

        // Written onto the instance rather than handled through GetHealLife, so the number on the
        // tooltip is the number that gets healed without the two being derived separately.
        item.healLife = HEAL_LIFE;

        item.SetNameOverride(displayName.Value);
    }

    public override void SaveData(Item item, TagCompound tag)
    {
        if (FromSon)
            tag["fromSon"] = true;
    }

    public override void LoadData(Item item, TagCompound tag)
    {
        // Runs after SetDefaults has reset the item, which is exactly when the gift has to be
        // reapplied -- neither the heal nor the name survives on their own.
        if (tag.GetBool("fromSon"))
            Bless(item);
    }

    public override void NetSend(Item item, BinaryWriter writer) => writer.Write(FromSon);

    public override void NetReceive(Item item, BinaryReader reader)
    {
        if (reader.ReadBoolean())
            Bless(item);
    }

    /// <summary>A gift does not disappear into the stack you picked off a cave wall.</summary>
    public override bool CanStack(Item destination, Item source)
    {
        bool sourceFromSon = source.TryGetGlobalItem(out SonsMushroom other) && other.FromSon;

        return FromSon == sourceFromSon;
    }

    public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
    {
        if (!FromSon)
            return;

        tooltips.Add(new TooltipLine(Mod, "SonsMushroom", flavour.Value)
        {
            OverrideColor = new Color(232, 168, 90),
        });
    }
}
