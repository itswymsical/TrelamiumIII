using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// A <see cref="WorldNPC"/> that looks like a Terrarian: drawn through
/// <see cref="Main.PlayerRenderer"/> off a backing <see cref="Player"/>, with hair,
/// skin, clothing, armour and dyes.
///
/// <para>
/// Everything about appearance and item posing lives here rather than on
/// <see cref="WorldNPC"/>, so an NPC with its own sprite pays none of it. The
/// <see cref="Puppet"/> keeps that player animating - see
/// <see cref="PlayerPuppet"/> for why it has to.
/// </para>
///
/// <para>
/// Set the look in <see cref="SetAppearanceDefaults"/>; it runs after
/// <see cref="WorldNPC.SetWorldNPCDefaults"/>, on a puppet that already exists.
/// </para>
/// </summary>
public abstract class TerrarianNPC : WorldNPC
{
    public override string Texture => AssetReferences.Core.NPCs.Actors.WorldNPC.KEY;

    /// <summary>
    /// Drives the backing player's pose, item animation and held-item visuals.
    /// Updated once per tick from <see cref="PostAI"/>.
    /// </summary>
    public PlayerPuppet Puppet { get; private set; }

    /// <summary>The backing player. All appearance state lives on it.</summary>
    public Player DummyPlayer => Puppet?.Player;

    public sealed override void SetDefaults()
    {
        Puppet = new PlayerPuppet(NPC, new Player());
        InitializeDummyPlayer();

        base.SetDefaults();
    }

    /// <summary>Hair, skin, clothes, equipment. Called once the puppet exists.</summary>
    protected abstract void SetAppearanceDefaults();

    /// <summary>
    /// Safe defaults so the backing player renders without the engine mistaking it for
    /// a real, networked one.
    /// </summary>
    private void InitializeDummyPlayer()
    {
        DummyPlayer.direction = -1;
        DummyPlayer.gravDir = 1f;
        DummyPlayer.isDisplayDollOrInanimate = true;
        DummyPlayer.dead = false;
        DummyPlayer.ghost = false;
        DummyPlayer.active = true;
        DummyPlayer.whoAmI = Main.maxPlayers;
        SetAppearanceDefaults();
    }

    public override void PostAI()
    {
        base.PostAI();

        // Last, so the pose reflects this tick's final position and a swing started
        // this tick shows its first animation frame immediately.
        Puppet.Update();
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

    // Equipment.
    //   armor[0..2]   head, body, legs        armor[10..12] their vanity slots
    //   armor[3..9]   accessories             armor[13..19] their vanity slots

    /// <summary>Sets head armour or vanity. 0 clears it.</summary>
    public void SetHeadSlot(int itemId, bool vanity = true) => SetArmorSlot(vanity ? 10 : 0, itemId);

    /// <summary>Sets body armour or vanity. 0 clears it.</summary>
    public void SetBodySlot(int itemId, bool vanity = true) => SetArmorSlot(vanity ? 11 : 1, itemId);

    /// <summary>Sets leg armour or vanity. 0 clears it.</summary>
    public void SetLegSlot(int itemId, bool vanity = true) => SetArmorSlot(vanity ? 12 : 2, itemId);

    /// <summary>Sets an accessory in slot 0-6, or its vanity counterpart.</summary>
    public void SetAccessory(int slotIndex, int itemId, bool vanity = false)
    {
        if (slotIndex < 0 || slotIndex > 6)
            throw new ArgumentOutOfRangeException(nameof(slotIndex), "Accessory slot index must be 0-6.");

        SetArmorSlot(vanity ? 13 + slotIndex : 3 + slotIndex, itemId);
    }

    private void SetArmorSlot(int slot, int itemId)
    {
        if (slot < 0 || slot >= DummyPlayer.armor.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));

        DummyPlayer.armor[slot].SetDefaults(itemId);
    }

    /// <summary>
    /// Sets a dye. Slots mirror armour: 0 head, 1 body, 2 legs, 3-9 accessories.
    /// </summary>
    public void SetDye(int slotIndex, int dyeItemId)
    {
        if (slotIndex < 0 || slotIndex >= DummyPlayer.dye.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        DummyPlayer.dye[slotIndex].SetDefaults(dyeItemId);

        // The dummy never runs Player.UpdateEquips, so the c* shader cache fields that
        // PlayerDrawSet.BoringSetup reads are never filled in. Mirror them here.
        int shaderId = DummyPlayer.dye[slotIndex].dye;

        switch (slotIndex)
        {
            case 0: DummyPlayer.cHead = shaderId; break;
            case 1: DummyPlayer.cBody = shaderId; break;
            case 2: DummyPlayer.cLegs = shaderId; break;
        }
    }

    // Drawing

    public sealed override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;

    public sealed override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Puppet.SyncTransform();

        spriteBatch.End();
        PreparePlayerRender();

        // Every layer runs, the held item included. PlayerDrawSet takes the weapon
        // from Player.lastVisualizedSelectedItem and its position from
        // Player.itemLocation, both of which PlayerPuppet.Update wrote this tick.
        Main.PlayerRenderer.DrawPlayer(
            Main.Camera, DummyPlayer, DummyPlayer.position,
            DummyPlayer.fullRotation, DummyPlayer.fullRotationOrigin,
            shadow: 0f, scale: 1f);

        FinalizePlayerRender();

        spriteBatch.Begin(
            SpriteSortMode.Immediate, BlendState.AlphaBlend,
            Main.DefaultSamplerState, DepthStencilState.None,
            Main.Rasterizer, null,
            Main.GameViewMatrix.TransformationMatrix);

        PostDrawActor(spriteBatch, screenPos, drawColor);

        if (Combat.PuppetDebug.ShowHitboxes)
        {
            Combat.PuppetDebug.DrawHitbox(spriteBatch, NPC.Hitbox, Color.CornflowerBlue);
            Puppet.Combat?.DrawDebug(spriteBatch);
        }
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
