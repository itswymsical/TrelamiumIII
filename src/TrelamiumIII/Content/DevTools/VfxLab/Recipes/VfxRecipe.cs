using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TrelamiumIII.Content.DevTools.VfxLab.Recipes;

/// <summary> A saved effect, and the source of truth for everything exported from it. </summary>
/// <remarks>
///     JSON so recipes diff cleanly and can be edited by hand. Whatever this version doesn't read is carried through
///     <see cref="Extra" /> and written back untouched.
/// </remarks>
internal sealed class VfxRecipe
{
    /// <summary> Newer than this won't load: it might mean something this build would silently get wrong. </summary>
    public const int CurrentFormat = 1;

    private static readonly Regex NamePattern = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    [JsonProperty("format", Order = 0)]
    public int Format { get; set; } = CurrentFormat;

    [JsonProperty("name", Order = 1)]
    public string Name { get; set; } = string.Empty;

    /// <summary> The host this draws on, by the name in its <c>.host.fxh</c>. </summary>
    [JsonProperty("surface", Order = 2)]
    public string Surface { get; set; } = "Field";

    [JsonProperty("stages", Order = 3)]
    public List<RecipeStage> Stages { get; set; } = [];

    /// <summary> Other recipes this one draws as part of itself, by name. A title screen is a stack of these. </summary>
    [JsonProperty("layers", Order = 4)]
    public List<string> Layers { get; set; } = [];

    [JsonProperty("physics", Order = 5)]
    public RecipePhysics Physics { get; set; } = new();

    [JsonProperty("modules", Order = 6)]
    public List<RecipeModule> Modules { get; set; } = [];

    [JsonProperty("links", Order = 7)]
    public RecipeLinks Links { get; set; } = new();

    /// <summary> How the lab releases this while it's being designed. Not part of what ships. </summary>
    [JsonProperty("emitter", Order = 8)]
    public RecipeEmitter Emitter { get; set; } = new();

    [JsonExtensionData]
    public IDictionary<string, JToken> Extra { get; set; } = new Dictionary<string, JToken>();

    /// <summary> A name doubles as a file name and a C# class name, so it has to work as both. </summary>
    public static bool IsValidName(string name) => NamePattern.IsMatch(name);

    public bool ShouldSerializeLayers() => Layers.Count > 0;

    public bool ShouldSerializeModules() => Modules.Count > 0;

    public bool ShouldSerializeLinks() => Links.Deposit is not null || Links.Extra.Count > 0;
}

/// <summary> One stage of a recipe: which stage file it is, its id within the recipe, and the values it overrides. </summary>
internal sealed class RecipeStage
{
    [JsonProperty("id", Order = 0)]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("type", Order = 1)]
    public string Type { get; set; } = string.Empty;

    /// <summary> Off keeps the stage and its values in the recipe, and out of the shader. </summary>
    [JsonProperty("enabled", Order = 2, DefaultValueHandling = DefaultValueHandling.Ignore)]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    /// <summary> Everything else on the stage: its parameters, by name. Anything not listed takes the stage file's default. </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken> Values { get; set; } = new Dictionary<string, JToken>();

    public RecipeStage Set(string name, float value)
    {
        Values[name] = RecipeNumbers.Shortest(value);
        return this;
    }

    public RecipeStage Set(string name, int value)
    {
        Values[name] = value;
        return this;
    }

    public RecipeStage Set(string name, string value)
    {
        Values[name] = value;
        return this;
    }

    /// <summary> Two numbers for a <c>float2</c>, three for a colour. </summary>
    public RecipeStage Set(string name, params float[] values)
    {
        Values[name] = new JArray(values.Select(static value => (object)RecipeNumbers.Shortest(value)));
        return this;
    }
}

internal static class RecipeNumbers
{
    /// <summary> The shortest double that reads back as the same float, so 0.36f saves as 0.36 and not 0.36000001430511475. </summary>
    public static double Shortest(float value) => double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
}

/// <summary> The numbers a field's motion is made of. Defaults are <c>FieldLayer</c>'s own. </summary>
internal sealed class RecipePhysics
{
    public const string Gas = "Gas";
    public const string Liquid = "Liquid";
    public const string Plasma = "Plasma";

    [JsonProperty("kind", Order = 0)]
    public string Kind { get; set; } = Gas;

    [JsonProperty("density", Order = 1)]
    public float Density { get; set; } = 0.25f;

    [JsonProperty("viscosity", Order = 2)]
    public float Viscosity { get; set; } = 0.15f;

    [JsonProperty("buoyancy", Order = 3)]
    public float Buoyancy { get; set; } = 0.055f;

    [JsonProperty("gravity", Order = 4)]
    public float Gravity { get; set; } = 0.07f;

    [JsonProperty("drag", Order = 5)]
    public float Drag { get; set; } = 0.984f;

    [JsonProperty("lifeDecay", Order = 6)]
    public float LifeDecay { get; set; } = 1f;

    [JsonProperty("enclosedPenalty", Order = 7)]
    public float EnclosedPenalty { get; set; } = 2f;

    [JsonProperty("temperature", Order = 8)]
    public float Temperature { get; set; } = 20f;

    [JsonProperty("temperatureMin", Order = 9)]
    public float TemperatureMin { get; set; }

    [JsonProperty("temperatureMax", Order = 10)]
    public float TemperatureMax { get; set; } = 1200f;

    [JsonProperty("maxNodes", Order = 11)]
    public int MaxNodes { get; set; } = 400;

    [JsonProperty("flowReference", Order = 12)]
    public float FlowReference { get; set; } = 6f;

    [JsonProperty("stampStretch", Order = 13)]
    public float StampStretch { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> Extra { get; set; } = new Dictionary<string, JToken>();
}

/// <summary> One module of a recipe: which one, and the settings it overrides. </summary>
internal sealed class RecipeModule
{
    [JsonProperty("type", Order = 0)]
    public string Type { get; set; } = string.Empty;

    [JsonExtensionData]
    public IDictionary<string, JToken> Values { get; set; } = new Dictionary<string, JToken>();

    public RecipeModule Set(string name, float value)
    {
        Values[name] = RecipeNumbers.Shortest(value);
        return this;
    }

    /// <summary> The saved setting, or <paramref name="fallback" /> if it isn't there or isn't a number. </summary>
    public float Get(string name, float fallback)
        => Values.TryGetValue(name, out JToken? value) && value.Type is JTokenType.Float or JTokenType.Integer ? value.Value<float>() : fallback;
}

/// <summary> How this recipe connects to anything else, by name. </summary>
internal sealed class RecipeLinks
{
    [JsonProperty("deposit", Order = 0, NullValueHandling = NullValueHandling.Ignore)]
    public RecipeDeposit? Deposit { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JToken> Extra { get; set; } = new Dictionary<string, JToken>();
}

/// <summary> The residue a field leaves where it pools. </summary>
internal sealed class RecipeDeposit
{
    /// <summary> The residue's name, or another recipe's once those can be residues. </summary>
    [JsonProperty("residue", Order = 0)]
    public string Residue { get; set; } = string.Empty;

    /// <summary> Coverage a tile needs before the field marks it. Under one, a single passing wisp does it. </summary>
    [JsonProperty("threshold", Order = 1)]
    public float Threshold { get; set; } = 0.9f;

    /// <summary> Ticks between passes over the tiles. </summary>
    [JsonProperty("interval", Order = 2)]
    public int Interval { get; set; } = 5;

    [JsonExtensionData]
    public IDictionary<string, JToken> Extra { get; set; } = new Dictionary<string, JToken>();
}

/// <summary> The lab's release of a field: how often, how big and which way. </summary>
internal sealed class RecipeEmitter
{
    /// <summary> Nodes per second. </summary>
    [JsonProperty("rate", Order = 0)]
    public float Rate { get; set; } = 12f;

    [JsonProperty("radius", Order = 1)]
    public float Radius { get; set; } = 28f;

    /// <summary> Ticks, before the field's life decay. </summary>
    [JsonProperty("life", Order = 2)]
    public float Life { get; set; } = 180f;

    /// <summary> Pixels per tick. </summary>
    [JsonProperty("speed", Order = 3)]
    public float Speed { get; set; } = 1.5f;

    /// <summary> Degrees, where -90 is straight up. </summary>
    [JsonProperty("angle", Order = 4)]
    public float Angle { get; set; } = -90f;

    /// <summary> Degrees either side of <see cref="Angle" />. </summary>
    [JsonProperty("spread", Order = 5)]
    public float Spread { get; set; } = 25f;

    /// <summary> Celsius, or negative for the field's own. </summary>
    [JsonProperty("temperature", Order = 6)]
    public float Temperature { get; set; } = -1f;

    [JsonExtensionData]
    public IDictionary<string, JToken> Extra { get; set; } = new Dictionary<string, JToken>();
}
