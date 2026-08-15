using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;
using TrelamiumIII.Core.UI;

namespace TrelamiumIII.Content.Pets;

internal sealed class WorkshopSlot(Item[] stations, int index)
    : BasicItemSlot(stations, index, ItemSlot.Context.InventoryItem, SLOT_SCALE)
{
    public const float SLOT_SCALE = 0.85f;

    public override bool ValidItemForSlot(Item item) => item.IsAir || CraftingStationRegistry.IsStation(item);

    /// <summary>A faint centre dot, so an empty slot still reads as a slot against the world behind it.</summary>
    protected override void DrawEmptyPlaceholder(SpriteBatch spriteBatch)
    {
        Texture2D texture = TextureAssets.MagicPixel.Value;
        Rectangle source = texture.Frame();
        Rectangle bounds = DrawnBounds();

        spriteBatch.Draw(
            texture,
            new Vector2(bounds.X + bounds.Width * 0.5f, bounds.Y + bounds.Height * 0.5f),
            source,
            Color.White * 0.35f,
            0f,
            source.Size() / 2f,
            Scale,
            SpriteEffects.None,
            0f
        );
    }

    protected override void SlotLogic(ref Item item)
    {
        base.SlotLogic(ref item);

        if (!item.IsAir)
            return;

        Main.hoverItemName = Language.GetTextValue("Mods.TrelamiumIII.Misc.WorkshopSlot");
        Main.mouseText = true;
    }
}

internal sealed class PossessedWorkshopUIState : UIState
{
    private const float SLOT_BOX = 52f * WorkshopSlot.SLOT_SCALE;
    private const float SLOT_GAP = 4f * WorkshopSlot.SLOT_SCALE;
    private const float PADDING = 8f;

    /// <summary>Kept clear of the screen edges, so a workshop stood in a corner is still usable.</summary>
    private const float SCREEN_MARGIN = 12f;

    private UIPanel panel = null!;

    private readonly List<WorkshopSlot> slots = [];

    /// <summary>The workshop the slots point at.</summary>
    private Item? bound;

    /// <summary>
    ///     Where the panel is in raw screen pixels, for the pet's own click test.
    /// </summary>
    /// <remarks>
    ///     Read a frame later than it is written, which does not matter: the pet holding the panel is
    ///     planted for as long as it is open, so these bounds do not move while anyone can click them.
    /// </remarks>
    public Rectangle ScreenBounds { get; private set; }

    public override void OnInitialize()
    {
        Width = Height = StyleDimension.Fill;

        panel = new UIPanel
        {
            Width = StyleDimension.FromPixels(PossessedWorkshop.SLOT_COUNT * SLOT_BOX + (PossessedWorkshop.SLOT_COUNT - 1) * SLOT_GAP + PADDING * 2f),
            Height = StyleDimension.FromPixels(SLOT_BOX + PADDING * 2f),
            BackgroundColor = new Color(48, 38, 32) * 0.92f,
            BorderColor = new Color(96, 74, 58),
        };

        panel.SetPadding(PADDING);
        Append(panel);
    }

    public override void Update(GameTime gameTime)
    {
        if (PossessedWorkshopUI.TendedWorkshop is not { ModItem: PossessedWorkshop workshop })
            return;

        if (!ReferenceEquals(bound, workshop.Item))
            Bind(workshop);

        Anchor();

        base.Update(gameTime);
    }

    /// <summary>Parks the panel over the pet's head, in UI coordinates, and clamped to the screen.</summary>
    private void Anchor()
    {
        if (PossessedWorkshopUI.TendedPet is not { } pet)
            return;

        Vector2 world = pet.Top + new Vector2(0f, -PossessedWorkshopPet.PANEL_GAP);

        // Through the game's own view matrix, so the panel tracks the workshop through zoom rather
        // than drifting off it, then out of screen pixels into the UI's own scale.
        Vector2 screen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);
        Vector2 anchor = screen / Main.UIScale;

        float width = panel.Width.Pixels;
        float height = panel.Height.Pixels;

        float maxX = Main.screenWidth / Main.UIScale - width - SCREEN_MARGIN;
        float maxY = Main.screenHeight / Main.UIScale - height - SCREEN_MARGIN;

        float left = MathHelper.Clamp(anchor.X - width * 0.5f, SCREEN_MARGIN, Math.Max(SCREEN_MARGIN, maxX));
        float top = MathHelper.Clamp(anchor.Y - height, SCREEN_MARGIN, Math.Max(SCREEN_MARGIN, maxY));

        panel.Left.Set(left, 0f);
        panel.Top.Set(top, 0f);

        LayoutSlots();

        panel.Recalculate();

        ScreenBounds = new Rectangle(
            (int)(left * Main.UIScale),
            (int)(top * Main.UIScale),
            (int)(width * Main.UIScale),
            (int)(height * Main.UIScale)
        );
    }

    private void LayoutSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].Left.Set(i * (SLOT_BOX + SLOT_GAP), 0f);
            slots[i].Top.Set(0f, 0f);
        }
    }

    private void Bind(PossessedWorkshop workshop)
    {
        foreach (WorkshopSlot slot in slots)
            panel.RemoveChild(slot);

        slots.Clear();

        for (int i = 0; i < workshop.Stations.Length; i++)
        {
            var slot = new WorkshopSlot(workshop.Stations, i);

            slots.Add(slot);
            panel.Append(slot);
        }

        bound = workshop.Item;
    }
}

/// <summary>
///     Owns the station panel: what it is attached to, whether it is up, and where it draws.
/// </summary>
public sealed class PossessedWorkshopUI : ModSystem
{
    private static UserInterface? userInterface;
    private static PossessedWorkshopUIState? state;

    /// <summary>Index of the pet whose panel is open, or -1. Purely local -- a panel is one player's.</summary>
    private static int tending = -1;

    public static bool Open => tending >= 0;

    public static Projectile? TendedPet => Open ? Main.projectile[tending] : null;

    public static Item? TendedWorkshop => Open
        ? Main.LocalPlayer.GetModPlayer<WorkshopPlayer>().ActiveWorkshop
        : null;

    public static bool MouseOverPanel => Open
        && state is not null
        && state.ScreenBounds.Contains(Main.mouseX, Main.mouseY);

    public static bool IsTending(Projectile pet) => Open && tending == pet.whoAmI;

    public static void Toggle(Projectile pet)
    {
        if (IsTending(pet))
        {
            Close();

            return;
        }

        tending = pet.whoAmI;
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    public static void Close()
    {
        if (!Open)
            return;

        tending = -1;
        SoundEngine.PlaySound(SoundID.MenuClose);
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        state = new PossessedWorkshopUIState();
        state.Activate();

        userInterface = new UserInterface();
        userInterface.SetState(state);
    }

    public override void Unload()
    {
        userInterface = null;
        state = null;
        tending = -1;
    }

    public override void OnWorldUnload() => tending = -1;

    public override void UpdateUI(GameTime gameTime)
    {
        if (!Open)
            return;

        if (!StillReachable())
        {
            Close();

            return;
        }

        userInterface?.Update(gameTime);
    }

    /// <summary>
    ///     Whether the thing the panel is attached to is still there and still within arm's reach.
    ///     Walking off, dismissing the pet or dying all close it through here.
    /// </summary>
    private static bool StillReachable()
    {
        if (tending < 0 || tending >= Main.maxProjectiles)
            return false;

        Projectile pet = Main.projectile[tending];

        if (!pet.active || pet.type != ModContent.ProjectileType<PossessedWorkshopPet>() || pet.owner != Main.myPlayer)
            return false;

        Player player = Main.LocalPlayer;

        if (player.dead || player.Distance(pet.Center) > PossessedWorkshopPet.PANEL_RANGE)
            return false;

        return player.GetModPlayer<WorkshopPlayer>().ActiveWorkshop?.ModItem is PossessedWorkshop;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int inventoryIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");

        if (inventoryIndex == -1)
            return;

        // Above the inventory: the player opened this deliberately, and a panel pinned to a spot in
        // the world cannot dodge the inventory the way a docked one can.
        layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
            "TrelamiumIII: Possessed Workshop",
            () =>
            {
                if (Open)
                    userInterface?.Draw(Main.spriteBatch, new GameTime());

                return true;
            },
            InterfaceScaleType.UI
        ));
    }
}
