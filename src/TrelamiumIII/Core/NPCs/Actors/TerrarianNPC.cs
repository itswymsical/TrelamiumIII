using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.Bestiary;
using Terraria.ModLoader.IO;
using TrelamiumIII.Core.NPCs.Combat;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// A <see cref="WorldNPC"/> that looks like a Terrarian: drawn through
/// <see cref="Main.PlayerRenderer"/> off a backing <see cref="Player"/>, with hair,
/// skin, clothing, armour and dyes.
///
/// <para>
/// Everything about appearance and item posing lives here rather than on
/// <see cref="WorldNPC"/>, so an NPC with its own sprite pays none of it. The look
/// itself is a <see cref="TerrarianModel"/>, which any NPC can carry; this class
/// wires one into a world NPC's AI, draw, save and sync, and keeps the appearance
/// setters close to hand.
/// </para>
///
/// <para>
/// Set the look in <see cref="SetAppearanceDefaults"/>; it runs before
/// <see cref="WorldNPC.SetWorldNPCDefaults"/>, on a puppet that already exists.
/// </para>
/// </summary>
public abstract class TerrarianNPC : WorldNPC
{
    public override string Texture => AssetReferences.Core.NPCs.Actors.WorldNPC.KEY;

    /// <summary>The player model this NPC renders as.</summary>
    public TerrarianModel Model { get; private set; }

    /// <summary>
    /// Drives the backing player's pose, item animation and held-item visuals.
    /// Updated once per tick from <see cref="PostAI"/>.
    /// </summary>
    public PlayerPuppet Puppet => Model?.Puppet;

    /// <summary>The backing player. All appearance state lives on it.</summary>
    public Player DummyPlayer => Model?.Player;

    public sealed override void SetDefaults()
    {
        Model = new TerrarianModel(NPC);
        SetAppearanceDefaults();

        base.SetDefaults();
    }

    /// <summary>Hair, skin, clothes, equipment. Called once the puppet exists.</summary>
    protected abstract void SetAppearanceDefaults();

    public override void PostAI()
    {
        base.PostAI();

        Puppet.FrontArmRotation = GameArmRotation();

        // Last, so the pose reflects this tick's final position and a swing started
        // this tick shows its first animation frame immediately.
        Puppet.Update();
    }

    /// <summary>Uses <see cref="WorldNPC.CombatWeapon"/> through the puppet, arming it as an ally on first use.</summary>
    protected override bool BeginAttack(Vector2 aimAt)
    {
        if (CombatWeapon <= 0)
            return false;

        if (Puppet.Combat == null)
            Puppet.ArmWith(PuppetAllegiance.Ally);

        Puppet.Hold(CombatWeapon);
        return Puppet.BeginUse(aimAt);
    }

    /// <summary>Front arm during rock-paper-scissors. Null outside a game or while the director has the NPC.</summary>
    private float? GameArmRotation()
    {
        if (State is not (TownState.GameLead or TownState.GameListen) || Director is { IsBusy: true })
            return null;

        return TownPoses.RockPaperScissorsArm(StateTicks % TownPoses.RockPaperScissorsCycle);
    }

    /// <summary>Swaps in <see cref="TerrarianEntryIcon"/>; the vanilla icon cannot draw a player model.</summary>
    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        base.SetBestiary(database, bestiaryEntry);
        bestiaryEntry.Icon = new TerrarianEntryIcon(Type);
    }

    // Appearance

    /// <summary>True = male, false = female.</summary>
    public bool Male
    {
        get => DummyPlayer.Male;
        set => DummyPlayer.Male = value;
    }

    /// <summary>Skin variant index. <see cref="Utilities.Extensions.PlayerClothingStyleID"/></summary>
    public int ClothingStyle
    {
        get => DummyPlayer.skinVariant;
        set => DummyPlayer.skinVariant = value;
    }

    public int HairType
    {
        get => DummyPlayer.hair;
        set => DummyPlayer.hair = value;
    }

    public Color HairColor
    {
        get => DummyPlayer.hairColor;
        set => DummyPlayer.hairColor = value;
    }

    public Color SkinColor
    {
        get => DummyPlayer.skinColor;
        set => DummyPlayer.skinColor = value;
    }

    public Color EyeColor
    {
        get => DummyPlayer.eyeColor;
        set => DummyPlayer.eyeColor = value;
    }

    public Color ShirtColor
    {
        get => DummyPlayer.shirtColor;
        set => DummyPlayer.shirtColor = value;
    }

    public Color UnderShirtColor
    {
        get => DummyPlayer.underShirtColor;
        set => DummyPlayer.underShirtColor = value;
    }

    public Color PantsColor
    {
        get => DummyPlayer.pantsColor;
        set => DummyPlayer.pantsColor = value;
    }

    public Color ShoeColor
    {
        get => DummyPlayer.shoeColor;
        set => DummyPlayer.shoeColor = value;
    }

    /// <summary>Draw at full brightness regardless of world lighting.</summary>
    public bool IgnoreWorldLighting
    {
        get => Puppet.IgnoreLighting;
        set => Puppet.IgnoreLighting = value;
    }

    /// <summary>Texture clothing laid over the armour slots. Null wears the armour.</summary>
    public ActorOutfit? Outfit
    {
        get => Model.Outfit;
        set => Model.Outfit = value;
    }

    // Equipment. Slot layout on TerrarianModel.

    /// <summary>Sets head armour or vanity. 0 clears it.</summary>
    public void SetHeadSlot(int itemId, bool vanity = true) => Model.SetHeadSlot(itemId, vanity);

    /// <summary>Sets body armour or vanity. 0 clears it.</summary>
    public void SetBodySlot(int itemId, bool vanity = true) => Model.SetBodySlot(itemId, vanity);

    /// <summary>Sets leg armour or vanity. 0 clears it.</summary>
    public void SetLegSlot(int itemId, bool vanity = true) => Model.SetLegSlot(itemId, vanity);

    /// <summary>Sets an accessory in slot 0-6, or its vanity counterpart.</summary>
    public void SetAccessory(int slotIndex, int itemId, bool vanity = false) => Model.SetAccessory(slotIndex, itemId, vanity);

    /// <summary>
    /// Sets a dye. Slots mirror armour: 0 head, 1 body, 2 legs, 3-9 accessories.
    /// </summary>
    public void SetDye(int slotIndex, int dyeItemId) => Model.SetDye(slotIndex, dyeItemId);

    // Drawing

    public sealed override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;

    public sealed override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Model.Draw(spriteBatch, PreparePlayerRender, FinalizePlayerRender);

        PostDrawActor(spriteBatch, screenPos, drawColor);

        Model.DrawDebug(spriteBatch);
    }

    /// <summary>
    /// Called with the SpriteBatch ended, before the player renderer runs. The device
    /// render target is free to redirect here.
    /// </summary>
    protected virtual void PreparePlayerRender() { }

    /// <summary>Called after the player renderer runs. Restore the render target here.</summary>
    protected virtual void FinalizePlayerRender() { }

    /// <summary>Extra effects on top of the player model. World-space, batch active.</summary>
    protected virtual void PostDrawActor(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) { }

    // Persistence

    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);

        tag["male"] = Male;
        tag["body"] = ClothingStyle;
        tag["hair"] = HairType;
        tag["hairColor"] = (int)HairColor.PackedValue;
        tag["skinColor"] = (int)SkinColor.PackedValue;
        tag["eyeColor"] = (int)EyeColor.PackedValue;
        tag["shirtColor"] = (int)ShirtColor.PackedValue;
        tag["underShirtColor"] = (int)UnderShirtColor.PackedValue;
        tag["pantsColor"] = (int)PantsColor.PackedValue;
        tag["shoeColor"] = (int)ShoeColor.PackedValue;

        tag["armor"] = SaveItems(DummyPlayer.armor);
        tag["dyes"] = SaveItems(DummyPlayer.dye);

        if (Outfit != null)
            tag["outfit"] = Outfit.FullName;
    }

    public override void LoadData(TagCompound tag)
    {
        base.LoadData(tag);

        if (tag.ContainsKey("male")) Male = tag.GetBool("male");
        if (tag.ContainsKey("body")) ClothingStyle = tag.GetInt("body");
        if (tag.ContainsKey("hair")) HairType = tag.GetInt("hair");
        if (tag.ContainsKey("hairColor")) HairColor = Unpack(tag.GetInt("hairColor"));
        if (tag.ContainsKey("skinColor")) SkinColor = Unpack(tag.GetInt("skinColor"));
        if (tag.ContainsKey("eyeColor")) EyeColor = Unpack(tag.GetInt("eyeColor"));
        if (tag.ContainsKey("shirtColor")) ShirtColor = Unpack(tag.GetInt("shirtColor"));
        if (tag.ContainsKey("underShirtColor")) UnderShirtColor = Unpack(tag.GetInt("underShirtColor"));
        if (tag.ContainsKey("pantsColor")) PantsColor = Unpack(tag.GetInt("pantsColor"));
        if (tag.ContainsKey("shoeColor")) ShoeColor = Unpack(tag.GetInt("shoeColor"));

        // Sound identity follows gender, which SetAppearanceDefaults picked before load.
        NPC.HitSound = Male ? SoundID.PlayerHit : SoundID.FemaleHit;
        NPC.DeathSound = SoundID.PlayerKilled;

        // An outfit that has since been removed leaves whatever SetAppearanceDefaults chose.
        if (tag.ContainsKey("outfit") && ModContent.TryFind(tag.GetString("outfit"), out ActorOutfit outfit))
            Outfit = outfit;

        if (tag.ContainsKey("armor"))
        {
            for (int i = 0; i < DummyPlayer.armor.Length; i++)
                DummyPlayer.armor[i].TurnToAir();

            foreach (TagCompound entry in tag.GetList<TagCompound>("armor"))
            {
                int slot = entry.GetInt("slot");
                if (slot >= 0 && slot < DummyPlayer.armor.Length)
                    DummyPlayer.armor[slot] = ItemIO.Load(entry.GetCompound("item"));
            }
        }

        if (!tag.ContainsKey("dyes"))
            return;

        foreach (TagCompound entry in tag.GetList<TagCompound>("dyes"))
        {
            int slot = entry.GetInt("slot");
            if (slot < 0 || slot >= DummyPlayer.dye.Length)
                continue;

            // Round-trip through SetDye so the c* shader cache stays in sync.
            SetDye(slot, ItemIO.Load(entry.GetCompound("item")).type);
        }
    }

    /// <summary>Head, body and leg armour and vanity: the slots appearance rolls write.</summary>
    private static readonly int[] SyncedArmorSlots = [0, 1, 2, 10, 11, 12];

    /// <summary>Appearance is rolled on every side and loaded only on the server, so clients take the server's.</summary>
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(Male);
        writer.Write((byte)ClothingStyle);
        writer.Write7BitEncodedInt(HairType);

        foreach (Color color in (Color[])[HairColor, SkinColor, EyeColor, ShirtColor, UnderShirtColor, PantsColor, ShoeColor])
            writer.Write(color.PackedValue);

        foreach (int slot in SyncedArmorSlots)
            writer.Write7BitEncodedInt(DummyPlayer.armor[slot].type);

        writer.Write7BitEncodedInt(Outfit == null ? 0 : Outfit.Type + 1);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        Male = reader.ReadBoolean();
        ClothingStyle = reader.ReadByte();
        HairType = reader.Read7BitEncodedInt();

        HairColor = new Color { PackedValue = reader.ReadUInt32() };
        SkinColor = new Color { PackedValue = reader.ReadUInt32() };
        EyeColor = new Color { PackedValue = reader.ReadUInt32() };
        ShirtColor = new Color { PackedValue = reader.ReadUInt32() };
        UnderShirtColor = new Color { PackedValue = reader.ReadUInt32() };
        PantsColor = new Color { PackedValue = reader.ReadUInt32() };
        ShoeColor = new Color { PackedValue = reader.ReadUInt32() };

        foreach (int slot in SyncedArmorSlots)
        {
            int type = reader.Read7BitEncodedInt();

            if (DummyPlayer.armor[slot].type != type)
                DummyPlayer.armor[slot].SetDefaults(type);
        }

        Outfit = ActorOutfit.Get(reader.Read7BitEncodedInt() - 1);

        NPC.HitSound = Male ? SoundID.PlayerHit : SoundID.FemaleHit;
    }

    private static List<TagCompound> SaveItems(Item[] items)
    {
        List<TagCompound> saved = [];

        for (int i = 0; i < items.Length; i++)
        {
            if (!items[i].IsAir)
                saved.Add(new TagCompound { ["slot"] = i, ["item"] = ItemIO.Save(items[i]) });
        }

        return saved;
    }

    private static Color Unpack(int packed) => new() { PackedValue = (uint)packed };
}
