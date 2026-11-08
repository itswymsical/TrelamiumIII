using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent.Bestiary;
using Terraria.Localization;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Bestiary icon for a <see cref="TerrarianNPC"/>.
///
/// <para>
/// Vanilla's <see cref="UnlockableNPCEntryIcon"/> draws a dummy NPC through
/// <c>DrawNPCDirect</c>, which for a player-rendered NPC ends the UI batch, restarts it with
/// the world matrix and hands <see cref="Main.PlayerRenderer"/> an NPC that never ran
/// <see cref="PlayerPuppet.Update"/>. This icon owns a dummy of its own instead and draws its
/// backing player the way <c>UICharacter</c> does: UI-space position plus
/// <see cref="Main.screenPosition"/>, inside the batch the bestiary already opened.
/// </para>
///
/// <para>
/// Honours <see cref="NPCID.Sets.NPCBestiaryDrawOffset"/> for position, scale, direction and
/// portrait overrides. The figure walks in place while hovered and in the portrait.
/// </para>
/// </summary>
public sealed class TerrarianEntryIcon(int npcType) : IEntryIcon
{
    private const float DefaultWalkSpeed = 1.25f;

    /// <summary>Gap between the icon box's bottom edge and the figure's feet, matching vanilla.</summary>
    private const float FootInset = 8f;

    private TerrarianNPC? actor;
    private Vector2 positionOffset;
    private float scale = 1f;

    public IEntryIcon CreateClone() => new TerrarianEntryIcon(npcType);

    public void Update(BestiaryUICollectionInfo providedInfo, Rectangle hitbox, EntryIconDrawSettings settings)
    {
        TerrarianNPC actor = Actor;
        NPC dummy = actor.NPC;

        int direction = -1;
        float walkSpeed = DefaultWalkSpeed;
        positionOffset = Vector2.Zero;
        scale = 1f;

        if (NPCID.Sets.NPCBestiaryDrawOffset.TryGetValue(npcType, out NPCID.Sets.NPCBestiaryDrawModifiers modifiers))
        {
            scale = settings.IsPortrait ? modifiers.PortraitScale ?? modifiers.Scale : modifiers.Scale;
            positionOffset = modifiers.Position;
            direction = modifiers.Direction ?? direction;

            if (modifiers.Velocity != 0f)
                walkSpeed = modifiers.Velocity;

            if (settings.IsPortrait)
            {
                positionOffset.X = modifiers.PortraitPositionXOverride ?? positionOffset.X;
                positionOffset.Y = modifiers.PortraitPositionYOverride ?? positionOffset.Y;
            }
        }

        Place(dummy, settings);

        dummy.direction = dummy.spriteDirection = direction;
        dummy.velocity.X = settings.IsPortrait || settings.IsHovered ? direction * walkSpeed : 0f;

        // Fills the frames and the head/body/legs slots the renderer reads.
        actor.Puppet.Update();
    }

    public void Draw(BestiaryUICollectionInfo providedInfo, SpriteBatch spriteBatch, EntryIconDrawSettings settings)
    {
        TerrarianNPC actor = Actor;

        // The list scrolls between Update and Draw, so the box is re-read here.
        Place(actor.NPC, settings);
        actor.Puppet.SyncTransform();

        Player player = actor.DummyPlayer;

        using (new Main.CurrentPlayerOverride(player))
        {
            Main.PlayerRenderer.DrawPlayer(
                Main.Camera, player, player.position + Main.screenPosition,
                0f, Vector2.Zero, 0f, scale);
        }
    }

    public string GetHoverText(BestiaryUICollectionInfo providedInfo) =>
        GetUnlockState(providedInfo) ? Lang.GetNPCNameValue(npcType) : "???";

    public bool GetUnlockState(BestiaryUICollectionInfo providedInfo) =>
        providedInfo.UnlockState > BestiaryEntryUnlockState.NotKnownAtAll_0;

    /// <summary>
    /// Built on first use: the database is populated on servers and for entries nobody opens,
    /// and neither should pay for a <see cref="Player"/>.
    /// </summary>
    private TerrarianNPC Actor => actor ??= CreateActor();

    private TerrarianNPC CreateActor()
    {
        var dummy = new NPC { IsABestiaryIconDummy = true };
        dummy.SetDefaults(npcType);

        var created = (TerrarianNPC)dummy.ModNPC;
        created.IgnoreWorldLighting = true;

        return created;
    }

    private void Place(NPC dummy, EntryIconDrawSettings settings)
    {
        dummy.Bottom = settings.iconbox.TopLeft() + settings.iconbox.Size() * new Vector2(0.5f, 1f)
            + new Vector2(0f, -FootInset) + positionOffset;

        dummy.position = dummy.position.Floor();
    }
}
