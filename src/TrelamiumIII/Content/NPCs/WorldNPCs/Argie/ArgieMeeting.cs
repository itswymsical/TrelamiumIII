using Terraria.DataStructures;
using TrelamiumIII.Content.Fungore;
using TrelamiumIII.Content.Items.Quest;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Content.Jobs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Argie;

/// <summary>
///     Meeting Argie, and what her lines hand over. Either greeting opening counts as the meeting
///     (<see cref="ArgiePlayer.Met" />). While <see cref="BrighterKey" /> plays, glowing mushroom tiles around the local
///     player are lit brighter.
/// </summary>
/// <remarks>The brightening is drawn by each client for its own player, and only lights tiles; nothing is saved.</remarks>
public sealed class ArgieMeeting : ModSystem
{
    public const string BrighterKey = "Argie.Brighter";

    /// <summary>Tiles around the player whose mushrooms brighten.</summary>
    private const int GLOW_RADIUS = 26;

    private const float GLOW_RISE = 1f / 90f;

    private const float GLOW_FALL = 1f / 240f;

    private static readonly Vector3 GlowLight = new(0.15f, 0.45f, 1.1f);

    private static readonly Color HaloColor = new(70, 140, 255);

    private static float glow;

    public override void Load() => DialogueManager.OnDialogueStart += OnDialogueStarted;

    public override void Unload() => DialogueManager.OnDialogueStart -= OnDialogueStarted;

    public override void ClearWorld() => glow = 0f;

    private static void OnDialogueStarted(string dialogueKey)
    {
        if (Main.dedServ || dialogueKey is not (ArgieNPC.GreetingKey or ArgieNPC.LetterGreetingKey))
            return;

        Main.LocalPlayer.GetModPlayer<ArgiePlayer>().Met = true;
    }

    #region glow

    public override void PostUpdateEverything()
    {
        if (Main.dedServ || Main.gameMenu)
            return;

        bool noticing = DialogueManager.Instance.GetActiveDialogueKey() == BrighterKey;

        glow = noticing ? Math.Min(glow + GLOW_RISE, 1f) : Math.Max(glow - GLOW_FALL, 0f);

        if (glow <= 0f)
            return;

        ForEachGlowingMushroom(Main.LocalPlayer, Brighten);
        Lighting.AddLight(Main.LocalPlayer.Center, GlowLight * (0.5f * glow));
    }

    /// <summary>Halos over the brightened mushrooms. Their own light is already near full, so light alone barely shows.</summary>
    public override void PostDrawTiles()
    {
        if (glow <= 0f || Main.gameMenu)
            return;

        Texture2D halo = Assets.Textures.VFX.Glow.Asset.Value;
        Vector2 origin = halo.Size() * 0.5f;
        float time = Main.GlobalTimeWrappedHourly;

        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None,
            Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

        ForEachGlowingMushroom(Main.LocalPlayer, (x, y, strength) =>
        {
            float pulse = 0.8f + 0.2f * MathF.Sin(time * 2.4f + x * 0.7f + y * 0.45f);
            Vector2 position = new Vector2(x * 16f + 8f, y * 16f + 8f) - Main.screenPosition;

            Main.spriteBatch.Draw(halo, position, null, HaloColor * (strength * pulse * 0.9f), 0f, origin, 0.11f, SpriteEffects.None, 0f);
        });

        Main.spriteBatch.End();
    }

    private static void Brighten(int x, int y, float strength)
    {
        Lighting.AddLight(x, y, GlowLight.X * strength, GlowLight.Y * strength, GlowLight.Z * strength);

        if (Main.rand.NextFloat() >= 0.02f * strength)
            return;

        Vector2 at = new(x * 16f + 8f, y * 16f + 4f);
        Vector2 toPlayer = (Main.LocalPlayer.Center - at).SafeNormalize(-Vector2.UnitY);

        ParticleSystem.Spawn<ScentMoteParticle>(at, toPlayer * Main.rand.NextFloat(0.6f, 1.4f) - Vector2.UnitY * 0.4f,
            timeLeft: Main.rand.Next(60, 110), scale: Main.rand.NextFloat(0.7f, 1.1f), rotation: Main.rand.NextFloat(MathHelper.TwoPi));
    }

    /// <summary>
    ///     Calls <paramref name="action" /> with every exposed glowing mushroom tile within <see cref="GLOW_RADIUS" /> of
    ///     <paramref name="player" />, and how strongly it brightens: the eased <see cref="glow" />, falling off with distance.
    /// </summary>
    private static void ForEachGlowingMushroom(Player player, Action<int, int, float> action)
    {
        Point center = player.Center.ToTileCoordinates();
        float eased = glow * glow * (3f - 2f * glow);

        for (int x = center.X - GLOW_RADIUS; x <= center.X + GLOW_RADIUS; x++)
        {
            for (int y = center.Y - GLOW_RADIUS; y <= center.Y + GLOW_RADIUS; y++)
            {
                if (!WorldGen.InWorld(x, y, 1) || !IsGlowingMushroom(x, y))
                    continue;

                float distance = Vector2.Distance(new Vector2(x, y), center.ToVector2()) / GLOW_RADIUS;
                float falloff = 1f - distance * distance;

                if (falloff > 0f)
                    action(x, y, eased * falloff);
            }
        }
    }

    /// <summary>Mushroom plants, trees and vines, and mushroom grass with open air beside it.</summary>
    private static bool IsGlowingMushroom(int x, int y)
    {
        Tile tile = Main.tile[x, y];

        if (!tile.HasTile)
            return false;

        if (tile.TileType is TileID.MushroomPlants or TileID.MushroomTrees or TileID.MushroomVines)
            return true;

        return tile.TileType == TileID.MushroomGrass
            && (!WorldGen.SolidTile(x, y - 1) || !WorldGen.SolidTile(x, y + 1) || !WorldGen.SolidTile(x - 1, y) || !WorldGen.SolidTile(x + 1, y));
    }

    #endregion

    #region handovers

    /// <summary>Takes Silvian's letter, once. Run when a conversation that reads it completes.</summary>
    public static void TakeLetter()
    {
        Player player = Main.LocalPlayer;
        var argie = player.GetModPlayer<ArgiePlayer>();

        if (argie.LetterDelivered)
            return;

        argie.LetterDelivered = true;
        player.ConsumeItem(ModContent.ItemType<SilviansLetter>(), reverseOrder: false, includeVoidBag: true);
    }

    /// <summary>
    ///     The spore survey is done, the player holds no spore and Fungore is not alive, so she hands over another. Covers a
    ///     lost fight and refights.
    /// </summary>
    public static bool SporeDue(Player player) =>
        player.GetModPlayer<ArgiePlayer>().JobStateOf(JobID.SporeSurvey) == JobState.Completed
        && !player.HasItem(ModContent.ItemType<MyconidSpore>())
        && !NPC.AnyNPCs(ModContent.NPCType<FungoreBoss>());

    public static void GiveSpore()
    {
        Player player = Main.LocalPlayer;

        if (SporeDue(player))
            RewardBag.GiveOrDrop(player, new Item(ModContent.ItemType<MyconidSpore>()), new EntitySource_Misc("ArgieSpore"));
    }

    #endregion
}
