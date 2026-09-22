using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Content.Items.Alchemy.AlchemyStations;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// Basic station panel: an input slot, three component slots, an output slot and a bottle return slot. The output shows a
/// silhouette preview; clicking it starts a brew. The advanced station uses <see cref="AdvancedAlchemyStationUI"/>.
/// </summary>
internal class AlchemyStationUI : UIState, IAlchemyStationUI
{
    public const int COMPONENT_SLOT_COUNT = 3;

    /// <summary>Slot draw scale; the layout constants in <see cref="BuildPanel"/> derive from it.</summary>
    private const float SLOT_SCALE = 1.0625f;

    public readonly Item[] Components = new Item[COMPONENT_SLOT_COUNT];

    public readonly Item[] InputContainer = new Item[1];

    public readonly Item[] OutputContainer = new Item[1];

    public readonly Item[] BottleReturnContainer = new Item[1];

    private AlchemyStationPanel _panel;
    private AlchemyCategoryLabel _categoryLabel;
    private UIText _hintLabel;

    /// <summary>World position (pixels) the panel anchors to, set by the system each frame; null leaves it put.</summary>
    public Vector2? WorldAnchor;

    /// <summary>Basic panel draws its own inline progress bar under the output slot.</summary>
    public bool ShowInlineBrewBar => true;

    /// <summary>True while a brew is running.</summary>
    public bool IsBrewing { get; private set; }

    /// <summary>True once a brew finishes.</summary>
    public bool IsResultReady { get; private set; }

    /// <summary>0..<see cref="_totalBrewTicks"/>, advances by 1 each frame while brewing.</summary>
    public int BrewTicks { get; private set; }

    /// <summary>0..1 progress through the active brew, used by the output slot's progress bar.</summary>
    public float BrewProgress => _totalBrewTicks > 0
        ? MathHelper.Clamp((float)BrewTicks / _totalBrewTicks, 0f, 1f)
        : 0f;

    public AlchemyStationMode Mode { get; private set; }

    /// <summary>Batches the next brew produces: the selected <see cref="AlchemyBatchSizes.Presets"/> entry clamped to <see cref="MaxOutputCount"/>.</summary>
    public int OutputCount => AlchemyBatchSizes.Of(_batchPreset, MaxOutputCount);

    /// <summary>Largest <see cref="OutputCount"/> the current ingredients can support.</summary>
    public int MaxOutputCount { get; private set; } = 1;

    private int _batchPreset;

    /// <summary>The result item locked in when the brew started. Used to populate the output on completion.</summary>
    private Item _brewSnapshot = new();

    private readonly AlchemyPreview _preview = new();

    /// <summary>Per-slot unit costs locked in when the brew started, so mid-brew edits can't change the bill.</summary>
    private int[] _brewCosts = Array.Empty<int>();

    /// <summary>Input items consumed per batch, locked in when the brew started.</summary>
    private int _brewInputCost = 1;

    /// <summary>Whether the active brew deposits bottles into <see cref="BottleReturnContainer" /> on completion.</summary>
    private bool _brewReturnsBottles;

    private int _brewCount = 1;

    private int _totalBrewTicks;

    public override void OnInitialize()
    {
        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++) Components[i] = new Item();
        InputContainer[0] = new Item();
        OutputContainer[0] = new Item();
        BottleReturnContainer[0] = new Item();

        BuildPanel();
    }

    private void BuildPanel()
    {
        //                  [input]
        //     [component]-[component]-[component]
        //                  [output]-[bottle]
        //                  [batch]
        const float PANEL_W = 412f;
        const float SLOT = 52f * SLOT_SCALE;   // BasicItemSlot box size at SLOT_SCALE
        const float SLOT_GAP = 31f;
        const float METER_SPACE = 15f;         // potency meters drawn below the input and component slots
        const float CENTER_X = PANEL_W * 0.5f;
        const float COLUMN_LEFT = CENTER_X - SLOT * 0.5f;

        const float INPUT_TOP = 65f;
        const float COMPONENT_TOP = INPUT_TOP + SLOT + METER_SPACE + 40f;
        const float OUTPUT_TOP = COMPONENT_TOP + SLOT + METER_SPACE + 32f;

        const float SLOT_STRIDE = SLOT + SLOT_GAP;
        const float ROW_LEFT = CENTER_X - (SLOT * 3f + SLOT_GAP * 2f) * 0.5f;

        const float BATCH_W = 60f;
        const float BATCH_H = 24f;
        const float BATCH_TOP = OUTPUT_TOP + SLOT + 16f;
        const float PANEL_H = BATCH_TOP + BATCH_H + 16f;

        const float MODE_BUTTON_SIZE = 26f;

        _panel = new AlchemyStationPanel
        {
            Width = StyleDimension.FromPixels(PANEL_W),
            Height = StyleDimension.FromPixels(PANEL_H),
            HAlign = 0f,
            VAlign = 0f,
            Left = StyleDimension.FromPixels(80f),
            Top = StyleDimension.FromPixels(260f),
        };
        // No padding, so every offset below is a panel-local pixel matching the link coordinates.
        _panel.SetPadding(0f);
        Append(_panel);

        _categoryLabel = new AlchemyCategoryLabel(0.95f)
        {
            Top = StyleDimension.FromPixels(8f),
            Width = StyleDimension.FromPercent(1f),
            Height = StyleDimension.FromPixels(24f),
        };
        _panel.Append(_categoryLabel);

        _hintLabel = new UIText(string.Empty, 0.78f, large: false)
        {
            HAlign = 0.5f,
            Top = StyleDimension.FromPixels(36f),
            TextColor = new Color(180, 180, 180),
        };
        _panel.Append(_hintLabel);

        var modeButton = new AlchemyModeButton(MODE_BUTTON_SIZE)
        {
            Left = StyleDimension.FromPixels(PANEL_W - MODE_BUTTON_SIZE - 10f),
            Top = StyleDimension.FromPixels(10f),
            OnClick = CycleMode,
            IsEnabled = CanCycleMode,
        };
        _panel.Append(modeButton);

        var input = new AlchemyInputSlot(InputContainer, this, SLOT_SCALE)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT),
            Top = StyleDimension.FromPixels(INPUT_TOP),
        };
        _panel.Append(input);

        var components = new AlchemyComponentSlot[COMPONENT_SLOT_COUNT];

        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++)
        {
            components[i] = new AlchemyComponentSlot(Components, i, this, SLOT_SCALE)
            {
                Left = StyleDimension.FromPixels(ROW_LEFT + i * SLOT_STRIDE),
                Top = StyleDimension.FromPixels(COMPONENT_TOP),
            };
            _panel.Append(components[i]);
        }

        var output = new AlchemyOutputSlot(OutputContainer, this, SLOT_SCALE)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT),
            Top = StyleDimension.FromPixels(OUTPUT_TOP),
        };
        _panel.Append(output);

        var bottleReturn = new AlchemyBottleReturnSlot(BottleReturnContainer, SLOT_SCALE)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT + SLOT_STRIDE),
            Top = StyleDimension.FromPixels(OUTPUT_TOP),
        };
        _panel.Append(bottleReturn);

        // Links behind the slots: input into the middle component, components chained, outer components into the output.
        // A link leaving a slot starts at the slot's edge, or below its potency meter while one is drawn.
        const float COMPONENT_MID_Y = COMPONENT_TOP + SLOT * 0.5f;
        const float COMPONENT_BOTTOM = COMPONENT_TOP + SLOT;
        const float FUNNEL_Y = OUTPUT_TOP + 5f;
        _panel.MeterClearance = METER_SPACE;
        _panel.Links =
        [
            new SlotLink(new Vector2(CENTER_X, INPUT_TOP + SLOT), new Vector2(CENTER_X, COMPONENT_TOP),
                () => input.ShowsPotencyMeter),

            new SlotLink(new Vector2(ROW_LEFT + SLOT, COMPONENT_MID_Y),
                new Vector2(ROW_LEFT + SLOT_STRIDE, COMPONENT_MID_Y)),
            new SlotLink(new Vector2(ROW_LEFT + SLOT_STRIDE + SLOT, COMPONENT_MID_Y),
                new Vector2(ROW_LEFT + SLOT_STRIDE * 2f, COMPONENT_MID_Y)),

            new SlotLink(new Vector2(ROW_LEFT + SLOT * 0.75f, COMPONENT_BOTTOM),
                new Vector2(COLUMN_LEFT, FUNNEL_Y), () => components[0].ShowsPotencyMeter),
            new SlotLink(new Vector2(ROW_LEFT + SLOT_STRIDE * 2f + SLOT * 0.25f, COMPONENT_BOTTOM),
                new Vector2(COLUMN_LEFT + SLOT, FUNNEL_Y), () => components[2].ShowsPotencyMeter),

            new SlotLink(new Vector2(COLUMN_LEFT + SLOT, OUTPUT_TOP + SLOT * 0.5f),
                new Vector2(COLUMN_LEFT + SLOT_STRIDE, OUTPUT_TOP + SLOT * 0.5f)),
        ];

        var batch = new AlchemyBatchButton(BATCH_W, BATCH_H, textScale: 0.85f)
        {
            Left = StyleDimension.FromPixels(CENTER_X - BATCH_W * 0.5f),
            Top = StyleDimension.FromPixels(BATCH_TOP),
            OnClick = CycleBatch,
            IsEnabled = CanCycleBatch,
            Count = () => OutputCount,
            IsCapped = () => OutputCount < AlchemyBatchSizes.Presets[_batchPreset],
        };
        _panel.Append(batch);
    }

    /// <summary>
    /// Anchors the panel above its <see cref="WorldAnchor"/> (the station's output spout), tracking the tile
    /// as the camera moves and clamping to the screen. Uses the game-view transform + physical pixels, matching
    /// how the rest of the mod's world-anchored UI is positioned.
    /// </summary>
    private void RepositionPanel()
    {
        if (_panel == null || WorldAnchor == null)
            return;

        Vector2 screen = Vector2.Transform(WorldAnchor.Value - Main.screenPosition, Main.GameViewMatrix.EffectMatrix);

        float w = _panel.Width.Pixels;
        float h = _panel.Height.Pixels;
        float left = screen.X - w * 0.5f;
        float top = screen.Y - h - 24f;

        left = MathHelper.Clamp(left, 8f, Main.screenWidth - w - 8f);
        top = MathHelper.Clamp(top, 8f, Main.screenHeight - h - 8f);

        _panel.Left.Set(left, 0f);
        _panel.Top.Set(top, 0f);
        _panel.Recalculate();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        RepositionPanel();

        // Mouse-click failsafe
        if (_panel != null && _panel.ContainsPoint(Main.MouseScreen))
            Main.LocalPlayer.mouseInterface = true;

        if (IsBrewing)
        {
            BrewTicks++;
            OutputContainer[0] = _brewSnapshot;

            if (BrewTicks >= _totalBrewTicks)
                CompleteBrew();
        }
        else if (IsResultReady)
        {
            // Player has taken the result = return to preview mode.
            if (OutputContainer[0].IsAir)
                IsResultReady = false;
        }
        else
        {
            UpdatePreview();
        }

        UpdateLabels();
    }

    private void UpdatePreview()
    {
        _preview.Refresh(InputContainer[0], Components, BottleReturnContainer[0]);
        MaxOutputCount = _preview.MaxBatches;
        OutputContainer[0] = _preview.ItemFor(OutputCount);
    }

    /// <inheritdoc/>
    public int RequiredPotency(int index) => _preview.Resolution.RequiredPotencyFor(index);

    private bool CanCycleBatch() => !IsBrewing && !IsResultReady && !OutputContainer[0].IsAir && MaxOutputCount > 1;

    private void CycleBatch() => _batchPreset = AlchemyBatchSizes.Next(_batchPreset, MaxOutputCount);

    private bool CanCycleMode() => !IsBrewing && !IsResultReady
        && AlchemyStationRules.NextMode(Mode, InputContainer[0], Components) != Mode;

    private void CycleMode() => Mode = AlchemyStationRules.NextMode(Mode, InputContainer[0], Components);

    private void UpdateLabels()
    {
        if (_categoryLabel == null) return;

        if (IsBrewing)
        {
            int secsLeft = (int)Math.Ceiling((_totalBrewTicks - BrewTicks) / 60f);
            _categoryLabel.SetText("Brewing...", new Color(255, 240, 160));
            _hintLabel.SetText($"{secsLeft}s remaining");
            return;
        }

        if (IsResultReady)
        {
            _categoryLabel.SetText("Done", new Color(180, 240, 160));
            _hintLabel.SetText("Chemicals produced!");
            return;
        }

        BrewCategory category = _preview.Resolution.Composition.Category;
        _categoryLabel.SetText(category == BrewCategory.None ? string.Empty : BrewCategories.DisplayName(category),
            BrewCategories.ColorOf(category));
        _hintLabel.SetText(IdleHint());
    }

    /// <summary>Idle hint. A stack shortfall takes priority over the placement prompt.</summary>
    private string IdleHint()
    {
        if (!OutputContainer[0].IsAir)
            return "Click the result to brew.";

        var missing = _preview.Resolution.Shortfall;

        if (missing.Exists)
            return $"Needs {missing.Required} {Lang.GetItemNameValue(missing.ItemType)} ({missing.Have} slotted).";

        bool empty = InputContainer[0].IsAir;

        return Mode switch
        {
            AlchemyStationMode.SolventMixture => empty ? "Place a solvent to mix into." : "Add solvents or solutes.",
            AlchemyStationMode.SoluteMixture => empty ? "Place a solute to mix into." : "Add solutes to mix.",
            _ => empty ? "Place a solvent and solutes." : "Add solutes to the solvent.",
        };
    }

    /// <summary>
    /// Called by <see cref="AlchemyOutputSlot"/> on left-click. Snapshots the current
    /// preview and starts the brew timer.
    /// </summary>
    public void TryStartBrew()
    {
        if (IsBrewing || IsResultReady) return;
        if (OutputContainer[0].IsAir) return;
        if (InputContainer[0].IsAir) return;

        AlchemyResolution resolution = _preview.Resolution;
        if (resolution.ReturnsBottle && StationBottles.Capacity(BottleReturnContainer[0]) < OutputCount) return;

        _brewSnapshot = OutputContainer[0].Clone();
        _brewCosts = (int[])resolution.ComponentCosts.Clone();
        _brewInputCost = resolution.InputCost;
        _brewReturnsBottles = resolution.ReturnsBottle;
        _brewCount = OutputCount;
        _totalBrewTicks = resolution.BrewTicks(AlchemyStationTile.BrewSpeed);
        BrewTicks = 0;
        IsBrewing = true;

        SoundEngine.PlaySound(Assets.SFX.Brewing.Asset);
    }

    private void CompleteBrew()
    {
        var result = _brewSnapshot.Clone();
        int count = _brewCount;

        if (!InputContainer[0].IsAir)
        {
            InputContainer[0].stack -= Math.Max(1, _brewInputCost) * count;
            if (InputContainer[0].stack <= 0) InputContainer[0].TurnToAir();
        }

        if (_brewReturnsBottles)
            StationBottles.Deposit(BottleReturnContainer, count);

        for (int i = 0; i < Components.Length; i++)
        {
            if (Components[i].IsAir) continue;
            int perUnit = i < _brewCosts.Length ? Math.Max(1, _brewCosts[i]) : 1;
            Components[i].stack -= perUnit * count;
            if (Components[i].stack <= 0) Components[i].TurnToAir();
        }

        // Result lives in the output slot until the player picks it up.
        OutputContainer[0] = result;
        IsResultReady = true;

        // Almanac discovery  only flag real recipe outputs, not fallback brews.
        if (!result.IsAir && AlmanacIndex.HasRecipeFor(result.type))
            AlmanacDiscoverySystem.Unlock(result.type);

        IsBrewing = false;
        BrewTicks = 0;
        _totalBrewTicks = 0;
        _brewSnapshot = new Item();

        SoundEngine.PlaySound(SoundID.ResearchComplete);
        SoundEngine.PlaySound(Assets.SFX.BrewingComplete.Asset);
    }

    /// <summary>Drop all current slot contents into the player inventory. Used by the transient (simple) station on close.</summary>
    public void ReturnAllToPlayer()
    {
        var player = Main.LocalPlayer;
        var src = player.GetSource_Misc("AlchemyStationClose");

        // Hand back a finished result before resetting state. Previews are not real items and never get returned.
        if (IsResultReady && !OutputContainer[0].IsAir)
            player.QuickSpawnItem(src, OutputContainer[0], OutputContainer[0].stack);

        if (!InputContainer[0].IsAir)
            player.QuickSpawnItem(src, InputContainer[0], InputContainer[0].stack);

        if (!BottleReturnContainer[0].IsAir)
            player.QuickSpawnItem(src, BottleReturnContainer[0], BottleReturnContainer[0].stack);

        for (int i = 0; i < Components.Length; i++)
            if (!Components[i].IsAir)
                player.QuickSpawnItem(src, Components[i], Components[i].stack);

        ClearSlots();
        ResetBrewState();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < Components.Length; i++)
            Components[i] = new Item();
        InputContainer[0] = new Item();
        OutputContainer[0] = new Item();
        BottleReturnContainer[0] = new Item();
    }

    private void ResetBrewState()
    {
        IsBrewing = false;
        IsResultReady = false;
        BrewTicks = 0;
        _totalBrewTicks = 0;
        _brewSnapshot = new Item();
        _brewCosts = Array.Empty<int>();
        _brewInputCost = 1;
        _brewReturnsBottles = false;
        _brewCount = 1;
        _batchPreset = 0;
        _preview.Reset();
    }
}
