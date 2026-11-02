using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ReLogic.Content;
using TrelamiumIII.Content.DevTools.VfxLab.Bench;
using TrelamiumIII.Content.DevTools.VfxLab.Generation;
using TrelamiumIII.Content.DevTools.VfxLab.Recipes;
using TrelamiumIII.Core.Graphics.Vfx;
using TrelamiumIII.Core.Tiles.Conditions;

namespace TrelamiumIII.Content.DevTools.VfxLab.UI;

/// <summary> Lays a draft field's recipe out as inspector rows: how it's released, how it moves, what it leaves, and the stack of stages that makes its look. </summary>
internal sealed class SlotInspector(Inspector inspector)
{
    private static readonly string[] Kinds = [RecipePhysics.Gas, RecipePhysics.Liquid, RecipePhysics.Plasma];

    private readonly HashSet<string> folded = [];

    /// <summary> Stages start folded, or their parameters would bury everything below them. These are the ones that have been opened. </summary>
    private readonly HashSet<string> openedStages = [];

    public List<InspectorField> Build(LabSlot slot)
    {
        VfxRecipe recipe = slot.Recipe!;
        List<InspectorField> rows = [];

        AddStack(rows, slot, recipe);
        AddModules(rows, slot, recipe);
        AddPhysics(rows, slot, recipe);
        AddResidue(rows, slot, recipe);

        rows.Add(Header("emitter", "Release", "How the Emit and Spray tools let this field out. Not part of what ships."));
        rows.AddRange(EmitterRows(recipe.Emitter, () => slot.Changed()));

        return rows;
    }

    /// <summary> Rows for how a field is released, used for drafts and real fields alike. </summary>
    public static IEnumerable<InspectorField> EmitterRows(RecipeEmitter emitter, Action changed)
    {
        yield return Number("emit/rate", "Rate", "Nodes per second", () => emitter.Rate, value => { emitter.Rate = value; changed(); }, 0f, 120f);
        yield return Number("emit/radius", "Radius", "Pixels", () => emitter.Radius, value => { emitter.Radius = value; changed(); }, 2f, 120f);
        yield return Number("emit/life", "Life", "Ticks, before the field's own decay", () => emitter.Life, value => { emitter.Life = value; changed(); }, 1f, 1200f);
        yield return Number("emit/speed", "Speed", "Pixels per tick", () => emitter.Speed, value => { emitter.Speed = value; changed(); }, 0f, 16f);
        yield return Number("emit/angle", "Angle", "Degrees. -90 is straight up", () => emitter.Angle, value => { emitter.Angle = value; changed(); }, -180f, 180f);
        yield return Number("emit/spread", "Spread", "Degrees either side of the angle", () => emitter.Spread, value => { emitter.Spread = value; changed(); }, 0f, 180f);
        yield return Number("emit/temperature", "Temperature", "Celsius. Negative uses the field's own", () => emitter.Temperature, value => { emitter.Temperature = value; changed(); }, -1f, 2000f);
    }

    private InspectorField Header(string id, string label, string? tip = null, IReadOnlyList<InspectorButton>? buttons = null, bool nested = false) => new()
    {
        Id = id,
        Label = label,
        Kind = FieldKind.Header,
        Tip = tip,
        Buttons = buttons,
        Nested = nested,
        Folded = folded.Contains(id),
        FoldChanged = isFolded =>
        {
            if (isFolded)
                folded.Add(id);
            else
                folded.Remove(id);
        },
    };

    private static InspectorField Number(string id, string label, string? tip, Func<float> get, Action<float> set, float? min, float? max) => new()
    {
        Id = id,
        Label = label,
        Kind = FieldKind.Numbers,
        Tip = tip,
        Get = () => [get()],
        Set = values => set(values[0]),
        Min = min,
        Max = max,
    };

    private void AddStack(List<InspectorField> rows, LabSlot slot, VfxRecipe recipe)
    {
        StageLibrary library = LabSession.Library;

        rows.Add(Header("stack", "Shader stages", "What the look is made of, top to bottom, in the order they run"));

        foreach (RecipeStage stage in RecipeEdit.InRunOrder(recipe, library))
        {
            string prefix = $"stage:{stage.Id}";

            List<InspectorButton> buttons =
            [
                new InspectorButton($"{prefix}:up", "^", () => { if (RecipeEdit.MoveStage(recipe, stage.Id, -1, library)) slot.Changed(true); }, "Run earlier in its phase"),
                new InspectorButton($"{prefix}:down", "v", () => { if (RecipeEdit.MoveStage(recipe, stage.Id, 1, library)) slot.Changed(true); }, "Run later in its phase"),
                new InspectorButton($"{prefix}:on", stage.Enabled ? "on" : "off", () => { stage.Enabled = !stage.Enabled; slot.Changed(true); }, "Switch off without losing its values", () => stage.Enabled),
                new InspectorButton($"{prefix}:remove", "x", () => { RecipeEdit.RemoveStage(recipe, stage.Id); slot.Changed(true); }, "Remove"),
            ];

            if (!library.Stages.TryGetValue(stage.Type, out StageDefinition? definition))
            {
                rows.Add(Header(prefix, $"{stage.Id}: {stage.Type} (missing)", "This build has no stage file of that name", buttons, nested: true));
                continue;
            }

            rows.Add(StageHeader(prefix, stage, definition, buttons));

            foreach (ParamDefinition parameter in definition.Params)
                rows.Add(StageRow(slot, stage, parameter));
        }

        rows.Add(new InspectorField
        {
            Id = "addstage",
            Label = "Add stage",
            Kind = FieldKind.Buttons,
            SectionRow = true,
            Buttons =
            [
                new InspectorButton("addstage", "+ Add stage", () => inspector.OpenList("Add a stage", AvailableStages(slot, recipe), inspector.LastPressed)),
            ],
        });
    }

    private InspectorField StageHeader(string id, RecipeStage stage, StageDefinition definition, IReadOnlyList<InspectorButton> buttons) => new()
    {
        Id = id,
        Label = stage.Enabled ? $"{definition.Phase}: {stage.Id}" : $"{definition.Phase}: {stage.Id} (off)",
        Kind = FieldKind.Header,
        Tip = definition.Name,
        Buttons = buttons,
        Nested = true,
        Folded = !openedStages.Contains(id),
        FoldChanged = isFolded =>
        {
            if (isFolded)
                openedStages.Remove(id);
            else
                openedStages.Add(id);
        },
    };

    private List<(string Label, Action Pick)> AvailableStages(LabSlot slot, VfxRecipe recipe)
    {
        StageLibrary library = LabSession.Library;

        if (!library.Hosts.TryGetValue(recipe.Surface, out HostDefinition? host))
            return [];

        return library.CompatibleStages(host)
            .Where(static stage => stage.Phase != StagePhase.Finish)
            .OrderBy(static stage => stage.Phase)
            .ThenBy(static stage => stage.Name, StringComparer.Ordinal)
            .Select(stage => ($"{stage.Phase}: {stage.Name}", (Action)(() =>
            {
                // Open, as whoever just added it is about to tune it.
                openedStages.Add($"stage:{RecipeEdit.AddStage(recipe, stage.Name).Id}");
                slot.Changed(true);
            })))
            .ToList();
    }

    private static InspectorField StageRow(LabSlot slot, RecipeStage stage, ParamDefinition parameter)
    {
        string id = $"{stage.Id}/{parameter.Name}";
        Func<bool> isChanged = () => stage.Values.ContainsKey(parameter.Name);
        Action reset = () =>
        {
            stage.Values.Remove(parameter.Name);
            slot.Changed();
        };

        if (parameter.Kind == ParamKind.Noise)
        {
            return new InspectorField
            {
                Id = id,
                Label = parameter.Name,
                Kind = FieldKind.Texture,
                GetTexture = () => ShaderGenerator.TextureOf(stage, parameter),
                SetTexture = texture =>
                {
                    stage.Set(parameter.Name, texture);
                    slot.Changed();
                },
                IsChanged = isChanged,
                Reset = reset,
            };
        }

        bool whole = parameter.Kind == ParamKind.Int;
        bool ranged = !float.IsInfinity(parameter.Min) && !float.IsInfinity(parameter.Max);

        return new InspectorField
        {
            Id = id,
            Label = parameter.Name,
            Kind = parameter.Kind == ParamKind.Color ? FieldKind.Color : FieldKind.Numbers,
            Tip = whole ? $"{parameter.Name}: changing this recompiles the shader" : parameter.Name,
            Min = ranged ? parameter.Min : null,
            Max = ranged ? parameter.Max : null,
            Get = () => ShaderGenerator.NumbersOf(stage, parameter),
            Set = values =>
            {
                if (whole)
                    stage.Set(parameter.Name, (int)MathF.Round(values[0]));
                else if (values.Length == 1)
                    stage.Set(parameter.Name, values[0]);
                else
                    stage.Set(parameter.Name, values);

                slot.Changed();
            },
            IsChanged = isChanged,
            Reset = reset,
        };
    }

    private void AddModules(List<InspectorField> rows, LabSlot slot, VfxRecipe recipe)
    {
        rows.Add(Header("modules", "Behaviour", "What this field does beyond its physics"));

        foreach (RecipeModule module in recipe.Modules.ToArray())
        {
            string prefix = $"module:{module.Type}";
            Type? type = ModuleCatalog.Find(module.Type);

            rows.Add(Header(prefix, type is null ? $"{module.Type} (missing)" : module.Type, type is null ? null : ModuleCatalog.Create(module)?.Tip,
            [
                new InspectorButton($"{prefix}:remove", "x", () =>
                {
                    recipe.Modules.Remove(module);
                    slot.Changed(true);
                }, "Remove"),
            ], nested: true));

            if (type is null)
                continue;

            object defaults = Activator.CreateInstance(type)!;

            foreach (FieldInfo field in ModuleCatalog.Settings(type))
            {
                float fallback = (float)field.GetValue(defaults)!;
                var range = field.GetCustomAttribute<ModuleRangeAttribute>();

                rows.Add(new InspectorField
                {
                    Id = $"{module.Type}/{field.Name}",
                    Label = field.Name,
                    Kind = FieldKind.Numbers,
                    Min = range?.Min,
                    Max = range?.Max,
                    Get = () => [module.Get(field.Name, fallback)],
                    Set = values =>
                    {
                        module.Set(field.Name, values[0]);
                        slot.Changed();
                    },
                    IsChanged = () => module.Values.ContainsKey(field.Name),
                    Reset = () =>
                    {
                        module.Values.Remove(field.Name);
                        slot.Changed();
                    },
                });
            }
        }

        rows.Add(new InspectorField
        {
            Id = "addmodule",
            Label = "Add behaviour",
            Kind = FieldKind.Buttons,
            SectionRow = true,
            Buttons =
            [
                new InspectorButton("addmodule", "+ Add behaviour", () => inspector.OpenList("Add a behaviour", ModuleCatalog.Types
                    .Where(type => recipe.Modules.All(module => module.Type != type.Name))
                    .Select(type => (type.Name, (Action)(() =>
                    {
                        recipe.Modules.Add(new RecipeModule { Type = type.Name });
                        slot.Changed(true);
                    })))
                    .ToList(), inspector.LastPressed)),
            ],
        });
    }

    private void AddPhysics(List<InspectorField> rows, LabSlot slot, VfxRecipe recipe)
    {
        RecipePhysics physics = recipe.Physics;

        InspectorField Row(string key, string label, string tip, Func<float> get, Action<float> set, float? min, float? max)
            => Number($"physics/{key}", label, tip, get, value =>
            {
                set(value);
                slot.Changed();
            }, min, max);

        rows.Add(Header("physics", "Physics", "How it moves"));

        rows.Add(new InspectorField
        {
            Id = "physics/kind",
            Label = "Kind",
            Kind = FieldKind.Choice,
            Tip = "A liquid is used up when it lands; a gas spreads along what it lands on",
            Choices = Kinds,
            GetChoice = () => Array.IndexOf(Kinds, physics.Kind),
            SetChoice = choice =>
            {
                physics.Kind = Kinds[choice];
                slot.Changed();
            },
        });

        rows.Add(Row("density", "Density", "0 is buoyant and 1 is dense: it blends between Buoyancy and Gravity", () => physics.Density, value => physics.Density = value, 0f, 1f));
        rows.Add(Row("viscosity", "Viscosity", "0 flows freely and 1 is sluggish", () => physics.Viscosity, value => physics.Viscosity = value, 0f, 1f));
        rows.Add(Row("buoyancy", "Buoyancy", "Lift on a node of density 0", () => physics.Buoyancy, value => physics.Buoyancy = value, 0f, 0.5f));
        rows.Add(Row("gravity", "Gravity", "Pull on a node of density 1", () => physics.Gravity, value => physics.Gravity = value, 0f, 0.5f));
        rows.Add(Row("drag", "Drag", "Velocity kept each tick", () => physics.Drag, value => physics.Drag = value, 0.8f, 1f));
        rows.Add(Row("lifedecay", "Life decay", "Life lost each tick", () => physics.LifeDecay, value => physics.LifeDecay = value, 0f, 8f));
        rows.Add(Row("enclosed", "Enclosed loss", "Life lost each tick inside a block, and when a liquid lands", () => physics.EnclosedPenalty, value => physics.EnclosedPenalty = value, 0f, 16f));
        rows.Add(Row("temperature", "Temperature", "Celsius, unless the release sets its own", () => physics.Temperature, value => physics.Temperature = value, null, null));
        rows.Add(Row("tempmin", "Heat range low", "What reads as no heat to the shader", () => physics.TemperatureMin, value => physics.TemperatureMin = value, null, null));
        rows.Add(Row("tempmax", "Heat range high", "What reads as full heat to the shader", () => physics.TemperatureMax, value => physics.TemperatureMax = value, null, null));
        rows.Add(Row("maxnodes", "Max nodes", "How many before the oldest give way", () => physics.MaxNodes, value => physics.MaxNodes = (int)value, 10f, 2000f));
        rows.Add(Row("flow", "Flow reference", "Speed that counts as full flow to the shader", () => physics.FlowReference, value => physics.FlowReference = value, 0.1f, 32f));
        rows.Add(Row("stretch", "Stamp stretch", "How far a node's blob stretches along its heading", () => physics.StampStretch, value => physics.StampStretch = value, 0f, 2f));
    }

    private void AddResidue(List<InspectorField> rows, LabSlot slot, VfxRecipe recipe)
    {
        rows.Add(Header("residue", "Residue", "What it leaves on tiles where it pools"));

        rows.Add(new InspectorField
        {
            Id = "residue/pick",
            Label = "Leaves",
            Kind = FieldKind.Buttons,
            Buttons =
            [
                new InspectorButton("residue/pick", recipe.Links.Deposit?.Residue ?? "Nothing. Choose a tile condition", () =>
                {
                    List<(string Label, Action Pick)> items =
                    [
                        ("Nothing", () =>
                        {
                            recipe.Links.Deposit = null;
                            slot.Changed(true);
                        }),
                    ];

                    foreach (ITileCondition condition in TileConditionSystem.All.OrderBy(static condition => condition.Name, StringComparer.Ordinal))
                    {
                        items.Add((condition.Name, () =>
                        {
                            recipe.Links.Deposit ??= new RecipeDeposit();
                            recipe.Links.Deposit.Residue = condition.Name;
                            slot.Changed(true);
                        }));
                    }

                    inspector.OpenList("Leaves behind", items, inspector.LastPressed);
                }),
            ],
        });

        if (recipe.Links.Deposit is not { } deposit)
            return;

        rows.Add(Number("residue/threshold", "Threshold", "Coverage a tile needs before it's marked. Under 1, a single passing wisp does it", () => deposit.Threshold, value =>
        {
            deposit.Threshold = value;
            slot.Changed();
        }, 0.05f, 5f));

        rows.Add(Number("residue/interval", "Interval", "Ticks between passes over the tiles", () => deposit.Interval, value =>
        {
            deposit.Interval = (int)value;
            slot.Changed();
        }, 1f, 60f));
    }
}
