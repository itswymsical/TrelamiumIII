using TrelamiumIII.Core.NPCs.Combat;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// The player model an NPC renders as: a backing <see cref="Player"/> holding hair, skin, clothing, armour, dyes and an
/// <see cref="ActorOutfit"/>, the <see cref="PlayerPuppet"/> that keeps it animating, and the draw that puts it on screen.
///
/// <para>
/// <see cref="TerrarianNPC"/> owns one, but nothing here needs a <see cref="WorldNPC"/>: any NPC can carry a model,
/// vanilla ones through <see cref="TerrarianModelComponent"/>. Code that wants an NPC's model asks <see cref="Of"/>
/// rather than casting.
/// </para>
///
/// <para>
/// The carrier calls <see cref="PlayerPuppet.Update"/> once per tick after the NPC has moved, and <see cref="Draw"/>
/// from its own draw.
/// </para>
/// </summary>
public sealed class TerrarianModel
{
    public TerrarianModel(NPC npc)
    {
        Puppet = new PlayerPuppet(npc, new Player());

        // Safe defaults so the backing player renders without the engine mistaking it for a real, networked one.
        Player.direction = -1;
        Player.gravDir = 1f;
        Player.isDisplayDollOrInanimate = true;
        Player.dead = false;
        Player.ghost = false;
        Player.active = true;
        Player.whoAmI = Main.maxPlayers;
    }

    /// <summary>
    /// Drives the backing player's pose, item animation and held-item visuals.
    /// </summary>
    public PlayerPuppet Puppet { get; }

    /// <summary>The backing player. All appearance state lives on it.</summary>
    public Player Player => Puppet.Player;

    public NPC NPC => Puppet.NPC;

    /// <summary>The model <paramref name="npc"/> renders as, or null when it draws its own sprite.</summary>
    public static TerrarianModel? Of(NPC npc)
    {
        if (npc.ModNPC is TerrarianNPC actor)
            return actor.Model;

        return npc.TryGetGlobalNPC(out TerrarianModelComponent component) && component.Enabled ? component.Model : null;
    }

    /// <summary>Texture clothing laid over the armour slots. Null wears the armour.</summary>
    public ActorOutfit? Outfit
    {
        get => Player.GetModPlayer<ActorOutfitPlayer>().Outfit;
        set => Player.GetModPlayer<ActorOutfitPlayer>().Outfit = value;
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

    /// <summary>Puts <paramref name="itemId"/> in raw armour slot <paramref name="slot"/>. 0 clears it.</summary>
    public void SetArmorSlot(int slot, int itemId)
    {
        if (slot < 0 || slot >= Player.armor.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));

        Player.armor[slot].SetDefaults(itemId);
    }

    /// <summary>
    /// Sets a dye. Slots mirror armour: 0 head, 1 body, 2 legs, 3-9 accessories.
    /// </summary>
    public void SetDye(int slotIndex, int dyeItemId)
    {
        if (slotIndex < 0 || slotIndex >= Player.dye.Length)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));

        Player.dye[slotIndex].SetDefaults(dyeItemId);

        // The dummy never runs Player.UpdateEquips, so the c* shader cache fields that
        // PlayerDrawSet.BoringSetup reads are never filled in. Mirror them here.
        int shaderId = Player.dye[slotIndex].dye;

        switch (slotIndex)
        {
            case 0: Player.cHead = shaderId; break;
            case 1: Player.cBody = shaderId; break;
            case 2: Player.cLegs = shaderId; break;
        }
    }

    // Drawing

    /// <summary>
    /// Draws the backing player where the NPC stands. The player renderer opens batches of its own, so
    /// <paramref name="spriteBatch"/> is ended around it and restarted in world space after.
    /// <paramref name="beforeRender"/> and <paramref name="afterRender"/> run while it is ended - the place to redirect
    /// the render target and put it back.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Action? beforeRender = null, Action? afterRender = null)
    {
        Puppet.SyncTransform();

        spriteBatch.End();
        beforeRender?.Invoke();

        // Every layer runs, the held item included. PlayerDrawSet takes the weapon
        // from Player.lastVisualizedSelectedItem and its position from
        // Player.itemLocation, both of which PlayerPuppet.Update wrote this tick.
        Main.PlayerRenderer.DrawPlayer(
            Main.Camera, Player, Player.position,
            Player.fullRotation, Player.fullRotationOrigin,
            shadow: 0f, scale: 1f);

        afterRender?.Invoke();

        spriteBatch.Begin(
            SpriteSortMode.Immediate, BlendState.AlphaBlend,
            Main.DefaultSamplerState, DepthStencilState.None,
            Main.Rasterizer, null,
            Main.GameViewMatrix.TransformationMatrix);
    }

    /// <summary>
    /// Draws the backing player into a batch the caller has open, for a capture rather than the screen. Unlike
    /// <see cref="Draw"/> it leaves the batch alone, and <paramref name="shadow"/> lets the caller decide what comes along:
    /// see <see cref="Residues.Bodies.BodyCanvas"/>.
    /// </summary>
    public void DrawInto(float shadow)
    {
        Puppet.SyncTransform();

        Main.PlayerRenderer.DrawPlayer(
            Main.Camera, Player, Player.position,
            Player.fullRotation, Player.fullRotationOrigin,
            shadow, scale: 1f);
    }

    /// <summary>Hitbox and swing overlays while <see cref="PuppetDebug.ShowHitboxes"/> is on. World space, batch active.</summary>
    public void DrawDebug(SpriteBatch spriteBatch)
    {
        if (!PuppetDebug.ShowHitboxes)
            return;

        PuppetDebug.DrawHitbox(spriteBatch, NPC.Hitbox, Color.CornflowerBlue);
        Puppet.Combat?.DrawDebug(spriteBatch);
    }
}
