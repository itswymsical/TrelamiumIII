using System.Diagnostics;
using SubworldLibrary;
using TrelamiumIII.Core.Graphics.Cinematics;
using TrelamiumIII.Core.Graphics.FbmScreen;

namespace TrelamiumIII.Common.Subworlds;

/// <summary>
/// Carries a realm's <see cref="FbmScreenRenderer"/> across the world boundary: a <see cref="RealmEntryCutscene"/>
/// covers the screen with it, <see cref="RealmLoadingScreen"/> keeps drawing the same instance, and it uncovers the
/// realm once the player arrives. One transition runs at a time; all state is client-side.
/// </summary>
public sealed class RealmTransition : ModSystem
{
    private enum Stage
    {
        None,

        /// <summary>The entry cutscene is covering the screen.</summary>
        Departing,

        /// <summary>Entered; the overlay holds at full coverage until SubworldLibrary switches to the loading screen.</summary>
        Handoff,

        /// <summary>The realm's loading screen is drawing the renderer.</summary>
        Loading,

        /// <summary>In the realm; the iris opens from the player.</summary>
        Arriving,
    }

    /// <summary>Drops the transition if the loading screen never takes over after <see cref="Handoff"/>.</summary>
    private const double HandoffTimeoutSeconds = 10.0;

    /// <summary>Drops the renderer if the loading screen stops drawing without the realm loading, e.g. a failed load.</summary>
    private const double LoadingTimeoutSeconds = 2.0;

    private const double ArrivalSeconds = 1.8;

    private static readonly Stopwatch Clock = new();

    private static Stage stage;
    private static SubworldRealm? realm;
    private static FbmScreenRenderer? screen;
    private static double stageStartSeconds;
    private static double lastFrameSeconds;
    private static double lastLoadingDrawSeconds;

    /// <summary>Real time, so it keeps counting on menu frames where nothing is built.</summary>
    private static double StageAge => Clock.Elapsed.TotalSeconds - stageStartSeconds;

    /// <summary>World position the iris closes on and opens from; the local player's centre when null.</summary>
    public static Vector2? FocusWorld { get; set; }

    /// <summary>True from the start of an entry cutscene until the world is left.</summary>
    public static bool HoldingPlayer => stage is Stage.Departing or Stage.Handoff;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        Clock.Start();

        On_Main.CheckMonoliths += BuildFrame;
        On_Main.DoDraw += DrawOverlay;
    }

    public override void Unload()
    {
        On_Main.CheckMonoliths -= BuildFrame;
        On_Main.DoDraw -= DrawOverlay;

        Reset();
        Clock.Reset();
    }

    /// <summary>Enters <typeparamref name="T"/> through its entry cutscene, or directly when it has none.</summary>
    public static bool Enter<T>() where T : SubworldRealm
    {
        if (stage != Stage.None)
            return false;

        T target = ModContent.GetInstance<T>();

        if (!Main.dedServ && target.ScreenStyle is not null && target.StartEntryCutscene())
            return true;

        return SubworldSystem.Enter<T>();
    }

    /// <summary>Opens a fresh renderer for <paramref name="target"/> at zero coverage. Null if it has no screen style.</summary>
    internal static FbmScreenRenderer? BeginDeparture(SubworldRealm target)
    {
        if (Open(target, keep: false) is not { } opened)
            return null;

        SetStage(Stage.Departing);
        FocusWorld = null;

        return opened;
    }

    /// <summary>Enters the departing realm. The overlay keeps covering the world until the loading screen takes over.</summary>
    internal static void Handoff()
    {
        if (stage != Stage.Departing || realm is null)
            return;

        SetStage(Stage.Handoff);

        if (!SubworldSystem.Enter(realm.FullName))
            Reset();
    }

    /// <summary>True while <paramref name="target"/>'s renderer is already on screen, from a handoff or an earlier loading frame.</summary>
    internal static bool IsContinuing(SubworldRealm target) => stage is Stage.Handoff or Stage.Loading && realm == target;

    /// <summary>
    /// The renderer for <paramref name="target"/>'s loading screen: the transition's own while one is handing off, a
    /// fresh one otherwise. Must be called every frame the loading screen draws. Null if it has no screen style.
    /// </summary>
    internal static FbmScreenRenderer? ForLoadingScreen(SubworldRealm target)
    {
        if (Open(target, keep: IsContinuing(target)) is not { } opened)
            return null;

        if (stage != Stage.Loading)
            SetStage(Stage.Loading);

        lastLoadingDrawSeconds = Clock.Elapsed.TotalSeconds;

        return opened;
    }

    private static FbmScreenRenderer? Open(SubworldRealm target, bool keep)
    {
        if (target.ScreenStyle is not { } style)
            return null;

        if (keep && screen is not null && realm == target)
            return screen;

        screen ??= new FbmScreenRenderer(style);
        screen.Reset();
        screen.Style = style;

        realm = target;
        target.PopulateScreen(screen);

        return screen;
    }

    private static void SetStage(Stage next)
    {
        stage = next;
        stageStartSeconds = Clock.Elapsed.TotalSeconds;
    }

    /// <summary>Main thread only: frees the renderer's targets.</summary>
    private static void Reset()
    {
        stage = Stage.None;
        realm = null;
        FocusWorld = null;

        screen?.Release();
        screen = null;
    }

    /// <summary>Builds the in-world overlay where Terraria expects mods to bind their own targets, before the world draws.</summary>
    private static void BuildFrame(On_Main.orig_CheckMonoliths orig)
    {
        orig();

        double now = Clock.Elapsed.TotalSeconds;
        float seconds = (float)Math.Min(now - lastFrameSeconds, 0.1);
        lastFrameSeconds = now;

        if (Main.dedServ || Main.gameMenu || screen is null || stage == Stage.None)
            return;

        if (stage == Stage.Loading)
        {
            if (SubworldSystem.Current != realm)
            {
                Reset();

                return;
            }

            SetStage(Stage.Arriving);
            FocusWorld = null;
            screen.Coverage = 1f;
            screen.Opacity = 1f;
        }

        if (stage == Stage.Arriving)
        {
            float progress = (float)Math.Min(StageAge / ArrivalSeconds, 1.0);

            if (progress >= 1f)
            {
                Reset();

                return;
            }

            screen.Coverage = 1f - progress * progress * (3f - 2f * progress);
        }

        // Held through the handoff: the world is being torn down on another thread by then.
        if (stage != Stage.Handoff)
            screen.Focus = FocusUv();

        screen.Advance(seconds);

        PresentationParameters presentation = Main.graphics.GraphicsDevice.PresentationParameters;
        screen.Render(Main.spriteBatch, Main.graphics.GraphicsDevice, presentation.BackBufferWidth, presentation.BackBufferHeight);
    }

    /// <summary>Last in the frame, so the overlay covers the interface and the cutscene letterbox too.</summary>
    private static void DrawOverlay(On_Main.orig_DoDraw orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);

        bool stale = stage switch
        {
            Stage.Departing => CutsceneSystem.CurrentCutscene is not RealmEntryCutscene,
            Stage.Handoff => StageAge > HandoffTimeoutSeconds,
            Stage.Loading => Main.gameMenu && Clock.Elapsed.TotalSeconds - lastLoadingDrawSeconds > LoadingTimeoutSeconds,
            _ => false,
        };

        if (stale)
            Reset();

        if (Main.gameMenu || stage is Stage.None or Stage.Loading || screen is null)
            return;

        PresentationParameters presentation = Main.graphics.GraphicsDevice.PresentationParameters;
        screen.Draw(Main.spriteBatch, new Rectangle(0, 0, presentation.BackBufferWidth, presentation.BackBufferHeight));
    }

    private static Vector2 FocusUv()
    {
        Vector2 world = FocusWorld ?? Main.LocalPlayer.Center;
        Vector2 size = new(Math.Max(Main.screenWidth, 1), Math.Max(Main.screenHeight, 1));

        // The overlay covers the zoomed frame, so the world point goes through the zoom before it becomes uv.
        return Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix) / size;
    }
}

/// <summary>Holds the local player still and unhurt while an entry cutscene runs.</summary>
public sealed class RealmTransitionPlayer : ModPlayer
{
    public override void SetControls()
    {
        if (Player.whoAmI != Main.myPlayer || !RealmTransition.HoldingPlayer)
            return;

        Player.controlLeft = false;
        Player.controlRight = false;
        Player.controlUp = false;
        Player.controlDown = false;
        Player.controlJump = false;
        Player.controlUseItem = false;
        Player.controlUseTile = false;
        Player.controlThrow = false;
        Player.controlHook = false;
        Player.controlMount = false;
        Player.controlQuickHeal = false;
        Player.controlQuickMana = false;
    }

    public override bool FreeDodge(Player.HurtInfo info) => Player.whoAmI == Main.myPlayer && RealmTransition.HoldingPlayer;
}
