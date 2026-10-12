using System;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using TrelamiumIII.Content.Alchemy.AlchemyStations;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Core.UI.AlchemyStation;

/// <summary>
/// Basic station panel: an input slot, three component slots, an output slot and a bottle return slot. The output shows a
/// silhouette preview; clicking it starts a brew, which this panel runs itself. The advanced station uses <see cref="AdvancedAlchemyStationUI"/>.
/// </summary>
internal sealed class AlchemyStationUI : AlchemyStationUIBase
{
    /// <summary>Slot draw scale; the layout constants in <see cref="BuildPanel"/> derive from it.</summary>
    private const float SLOT_SCALE = 1.0625f;

    private readonly Item[] components = new Item[COMPONENT_SLOT_COUNT];
    private readonly Item[] input = new Item[1];
    private readonly Item[] output = new Item[1];
    private readonly Item[] bottleReturn = new Item[1];

    private AlchemyStationPanel _panel;
    private AlchemyCategoryLabel _categoryLabel;
    private UIText _hintLabel;

    private AlchemyStationMode mode;

    private bool isBrewing;
    private bool isResultReady;

    /// <summary>0..<see cref="_totalBrewTicks"/>, advancing by 1 each frame while brewing.</summary>
    private int brewTicks;

    private int _totalBrewTicks;

    /// <summary>The result locked in when the brew started; the output shows it until completion.</summary>
    private Item _brewSnapshot = new();

    /// <summary>Per-slot unit costs locked in when the brew started, so mid-brew edits can't change the bill.</summary>
    private int[] _brewCosts = [];

    /// <summary>Input items consumed per batch, locked in when the brew started.</summary>
    private int _brewInputCost = 1;

    private bool _brewReturnsBottles;

    private int _brewCount = 1;

    /// <summary>World position (pixels) the panel anchors to, set by the system each frame; null leaves it put.</summary>
    public Vector2? WorldAnchor;

    public override bool ShowInlineBrewBar => true;

    public override bool IsBrewing => isBrewing;

    public override bool IsResultReady => isResultReady;

    public override float BrewProgress => _totalBrewTicks > 0 ? MathHelper.Clamp((float)brewTicks / _totalBrewTicks, 0f, 1f) : 0f;

    public override AlchemyStationMode Mode => mode;

    protected override Item[] Input => input;

    protected override Item[] Components => components;

    protected override Item[] Output => output;

    protected override Item[] BottleReturn => bottleReturn;

    protected override void SetMode(AlchemyStationMode mode) => this.mode = mode;

    public override void OnInitialize()
    {
        ClearSlots();
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

        var inputSlot = new AlchemyInputSlot(input, this, SLOT_SCALE)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT),
            Top = StyleDimension.FromPixels(INPUT_TOP),
        };
        _panel.Append(inputSlot);

        var componentSlots = new AlchemyComponentSlot[COMPONENT_SLOT_COUNT];

        for (int i = 0; i < COMPONENT_SLOT_COUNT; i++)
        {
            componentSlots[i] = new AlchemyComponentSlot(components, i, this, SLOT_SCALE)
            {
                Left = StyleDimension.FromPixels(ROW_LEFT + i * SLOT_STRIDE),
                Top = StyleDimension.FromPixels(COMPONENT_TOP),
            };
            _panel.Append(componentSlots[i]);
        }

        var outputSlot = new AlchemyOutputSlot(output, this, SLOT_SCALE)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT),
            Top = StyleDimension.FromPixels(OUTPUT_TOP),
        };
        _panel.Append(outputSlot);

        var bottleSlot = new AlchemyBottleReturnSlot(bottleReturn, SLOT_SCALE)
        {
            Left = StyleDimension.FromPixels(COLUMN_LEFT + SLOT_STRIDE),
            Top = StyleDimension.FromPixels(OUTPUT_TOP),
        };
        _panel.Append(bottleSlot);

        // Links behind the slots: input into the middle component, components chained, outer components into the output.
        // A link leaving a slot starts at the slot's edge, or below its potency meter while one is drawn.
        const float COMPONENT_MID_Y = COMPONENT_TOP + SLOT * 0.5f;
        const float COMPONENT_BOTTOM = COMPONENT_TOP + SLOT;
        const float FUNNEL_Y = OUTPUT_TOP + 5f;
        _panel.MeterClearance = METER_SPACE;
        _panel.Links =
        [
            new SlotLink(new Vector2(CENTER_X, INPUT_TOP + SLOT), new Vector2(CENTER_X, COMPONENT_TOP),
                () => inputSlot.ShowsPotencyMeter),

            new SlotLink(new Vector2(ROW_LEFT + SLOT, COMPONENT_MID_Y),
                new Vector2(ROW_LEFT + SLOT_STRIDE, COMPONENT_MID_Y)),
            new SlotLink(new Vector2(ROW_LEFT + SLOT_STRIDE + SLOT, COMPONENT_MID_Y),
                new Vector2(ROW_LEFT + SLOT_STRIDE * 2f, COMPONENT_MID_Y)),

            new SlotLink(new Vector2(ROW_LEFT + SLOT * 0.75f, COMPONENT_BOTTOM),
                new Vector2(COLUMN_LEFT, FUNNEL_Y), () => componentSlots[0].ShowsPotencyMeter),
            new SlotLink(new Vector2(ROW_LEFT + SLOT_STRIDE * 2f + SLOT * 0.25f, COMPONENT_BOTTOM),
                new Vector2(COLUMN_LEFT + SLOT, FUNNEL_Y), () => componentSlots[2].ShowsPotencyMeter),

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
            IsCapped = () => OutputCount < AlchemyBatchSizes.Presets[BatchPreset],
        };
        _panel.Append(batch);
    }

    /// <summary>Anchors the panel above <see cref="WorldAnchor"/> (the station's output spout), clamped to the screen.</summary>
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

        if (_panel != null && _panel.ContainsPoint(Main.MouseScreen))
            Main.LocalPlayer.mouseInterface = true;

        if (isBrewing)
        {
            brewTicks++;
            output[0] = _brewSnapshot;

            if (brewTicks >= _totalBrewTicks)
                CompleteBrew();
        }
        else if (isResultReady)
        {
            // The player took the result: back to previewing.
            if (output[0].IsAir)
                isResultReady = false;
        }
        else
        {
            UpdatePreview();
        }

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (_categoryLabel == null) return;

        if (isBrewing)
        {
            int secsLeft = (int)Math.Ceiling((_totalBrewTicks - brewTicks) / 60f);
            _categoryLabel.SetText("Brewing...", new Color(255, 240, 160));
            _hintLabel.SetText($"{secsLeft}s remaining");
            return;
        }

        if (isResultReady)
        {
            _categoryLabel.SetText("Done", new Color(180, 240, 160));
            _hintLabel.SetText("Chemicals produced!");
            return;
        }

        ShowCategory(_categoryLabel);
        _hintLabel.SetText(IdleHint());
    }

    /// <summary>Idle hint. A stack shortfall takes priority over the placement prompt.</summary>
    private string IdleHint()
    {
        if (!output[0].IsAir)
            return "Click the result to brew.";

        var missing = Preview.Resolution.Shortfall;

        if (missing.Exists)
            return $"Needs {missing.Required} {Lang.GetItemNameValue(missing.ItemType)} ({missing.Have} slotted).";

        bool empty = input[0].IsAir;

        return mode switch
        {
            AlchemyStationMode.SolventMixture => empty ? "Place a solvent to mix into." : "Add solvents or solutes.",
            AlchemyStationMode.SoluteMixture => empty ? "Place a solute to mix into." : "Add solutes to mix.",
            _ => empty ? "Place a solvent and solutes." : "Add solutes to the solvent.",
        };
    }

    protected override void StartBrew(AlchemyResolution resolution, int count)
    {
        _brewSnapshot = output[0].Clone();
        _brewCosts = (int[])resolution.ComponentCosts.Clone();
        _brewInputCost = resolution.InputCost;
        _brewReturnsBottles = resolution.ReturnsBottle;
        _brewCount = count;
        _totalBrewTicks = resolution.BrewTicks(AlchemyStationTile.BrewSpeed);
        brewTicks = 0;
        isBrewing = true;
    }

    private void CompleteBrew()
    {
        var result = _brewSnapshot.Clone();
        int count = _brewCount;

        if (!input[0].IsAir)
        {
            input[0].stack -= Math.Max(1, _brewInputCost) * count;
            if (input[0].stack <= 0) input[0].TurnToAir();
        }

        if (_brewReturnsBottles)
            StationBottles.Deposit(bottleReturn, count);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].IsAir) continue;
            int perUnit = i < _brewCosts.Length ? Math.Max(1, _brewCosts[i]) : 1;
            components[i].stack -= perUnit * count;
            if (components[i].stack <= 0) components[i].TurnToAir();
        }

        // The result stays in the output slot until the player picks it up.
        output[0] = result;
        isResultReady = true;

        // Only real recipe outputs count as Almanac discoveries, not fallback brews.
        if (!result.IsAir && AlmanacIndex.HasRecipeFor(result.type))
            AlmanacDiscoverySystem.Unlock(result.type);

        isBrewing = false;
        brewTicks = 0;
        _totalBrewTicks = 0;
        _brewSnapshot = new Item();

        SoundEngine.PlaySound(SoundID.ResearchComplete);
        SoundEngine.PlaySound(Assets.SFX.BrewingComplete.Asset);
    }

    /// <summary>Hands every slot's contents back to the player, then resets. The transient station calls this on close.</summary>
    public void ReturnAllToPlayer()
    {
        var player = Main.LocalPlayer;
        var src = player.GetSource_Misc("AlchemyStationClose");

        // A finished result is returned; a preview is not a real item.
        if (isResultReady && !output[0].IsAir)
            player.QuickSpawnItem(src, output[0], output[0].stack);

        if (!input[0].IsAir)
            player.QuickSpawnItem(src, input[0], input[0].stack);

        if (!bottleReturn[0].IsAir)
            player.QuickSpawnItem(src, bottleReturn[0], bottleReturn[0].stack);

        for (int i = 0; i < components.Length; i++)
            if (!components[i].IsAir)
                player.QuickSpawnItem(src, components[i], components[i].stack);

        ClearSlots();
        ResetBrewState();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < components.Length; i++)
            components[i] = new Item();

        input[0] = new Item();
        output[0] = new Item();
        bottleReturn[0] = new Item();
    }

    private void ResetBrewState()
    {
        isBrewing = false;
        isResultReady = false;
        brewTicks = 0;
        _totalBrewTicks = 0;
        _brewSnapshot = new Item();
        _brewCosts = [];
        _brewInputCost = 1;
        _brewReturnsBottles = false;
        _brewCount = 1;
        ResetPreview();
    }
}
