using System.Collections.Generic;
using System.IO;
using Terraria.Localization;
using Terraria.ModLoader.IO;
using TrelamiumIII.Content.Rarities;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A mushroom from your son. Stored on the item instance, so it stays a mushroom everywhere (including
///     <see cref="MyclobutanilPlayer" />'s surge) but heals more. Save, network and creation all go through <see cref="Bless" />.
/// </summary>
public sealed class SonsMushroom : GlobalItem
{
    /// <summary>Against the vanilla mushroom's fifteen.</summary>
    public const int HEAL_LIFE = 45;

    // Assigned in SetStaticDefaults, which runs long before anything can hold a mushroom.
    private static LocalizedText displayName = null!;

    private static LocalizedText flavour = null!;

    public override bool InstancePerEntity => true;

    /// <summary>Needs late instantiation: the type is only known once the item has run its defaults.</summary>
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
        mushroom.rare = ModContent.RarityType<MyconidRarity>();

        if (mushroom.TryGetGlobalItem(out SonsMushroom son))
            son.Bless(mushroom);

        return mushroom;
    }

    /// <summary>Marks <paramref name="item" /> as a gift, and applies everything that changes.</summary>
    private void Bless(Item item)
    {
        FromSon = true;

        // Set on the instance rather than via GetHealLife, so the tooltip shows exactly what is healed.
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
