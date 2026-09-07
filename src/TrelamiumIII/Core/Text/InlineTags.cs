using System.Collections;
using System.Globalization;
using System.Reflection;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.UI.Chat;
using TrelamiumIII.Common.ChemicalClass;

namespace TrelamiumIII.Core.Text;

/// <summary>
///     Registers this mod's inline text tags with Terraria's snippet parser.
/// </summary>
/// <remarks>
///     <para>
///         Tags work anywhere Terraria parses text -- item tooltips, chat, signs, NPC dialogue --
///         because the parser is shared. Nothing has to opt in.
///     </para>
///     <list type="bullet">
///         <item>
///             <c>[buff:FungalInfection]</c> -- the buff's localised name followed by its icon.
///             Accepts an internal name, a <c>ModName/InternalName</c> pair, a vanilla buff name,
///             or a numeric id. An <c>RRGGBB</c> option retints the label:
///             <c>[buff/88FF88:FungalInfection]</c>.
///         </item>
///         <item>
///             <c>[chem:Thermal]</c> -- a chemical damage type's name followed by its element
///             medallion. Takes any <see cref="ChemicalDamageType" /> member, and accepts the same
///             <c>RRGGBB</c> option as <c>buff</c> when the default element tint is wrong for the
///             line.
///         </item>
///         <item>
///             <c>[tex:TrelamiumIII/Content/Fungore/FungalInfection]</c> -- any texture, for cases
///             with no buff behind them. The option is a scale multiplier: <c>[tex/0.5:Path]</c>.
///         </item>
///         <item>
///             <c>[gloss:Tidemark]</c> -- a glossary term, underscored and explained on hover.
///         </item>
///         <item>
///             <c>[lore:DrownedArchive]</c> -- a cross-reference to another lore book, which opens
///             when clicked inside one.
///         </item>
///         <item>
///             <c>[redacted:the third seal]</c> -- struck through with a bar until the reader has
///             earned it.
///         </item>
///         <item>
///             <c>[hand:someone wrote this later]</c> -- switches to the handwriting face mid-line.
///         </item>
///         <item>
///             <c>[ttblock/type.index:fallback]</c> -- a custom-drawn <see cref="Tooltips.TooltipBlock" />
///             in an item tooltip. Not written by hand: <see cref="Tooltips.ItemTooltipBlocks" />
///             emits it for each block an item's <see cref="Tooltips.ITooltipBlockProvider" />s
///             contribute, and the tag draws as its fallback text anywhere else.
///         </item>
///     </list>
/// </remarks>
public sealed class InlineTagSystem : ModSystem
{
    internal const string BUFF_TAG = "buff";
    internal const string TEXTURE_TAG = "tex";
    internal const string CHEMICAL_TAG = "chem";
    internal const string GLOSS_TAG = "gloss";
    internal const string LORE_TAG = "lore";
    internal const string REDACTED_TAG = "redacted";
    internal const string HAND_TAG = "hand";
    internal const string KEY_TAG = "key";

    /// <summary>A custom-drawn tooltip block. See <see cref="Tooltips.TooltipBlockTagHandler" />.</summary>
    internal const string BLOCK_TAG = "ttblock";

    /// <summary>The authoring tool's character-formatting run. See <see cref="FormatTagHandler" />.</summary>
    internal const string FORMAT_TAG = Authoring.FormatCodec.TAG;

    private static readonly string[] Tags =
    [
        BUFF_TAG,
        TEXTURE_TAG,
        CHEMICAL_TAG,
        GLOSS_TAG,
        LORE_TAG,
        REDACTED_TAG,
        HAND_TAG,
        KEY_TAG,
        BLOCK_TAG,
        FORMAT_TAG,
    ];

    public override void Load()
    {
        // Terraria's registry is a static dictionary that uses Add, so a leftover entry from a
        // previous load would throw on the next one. Clearing first makes reloads survivable.
        UnregisterTags();

        ChatManager.Register<BuffTagHandler>(BUFF_TAG);
        ChatManager.Register<InlineTextureTagHandler>(TEXTURE_TAG);
        ChatManager.Register<ChemicalTagHandler>(CHEMICAL_TAG);
        ChatManager.Register<GlossTagHandler>(GLOSS_TAG);
        ChatManager.Register<LoreLinkTagHandler>(LORE_TAG);
        ChatManager.Register<RedactedTagHandler>(REDACTED_TAG);
        ChatManager.Register<HandwritingTagHandler>(HAND_TAG);
        ChatManager.Register<HandwritingTagHandler>(KEY_TAG);
        ChatManager.Register<Tooltips.TooltipBlockTagHandler>(BLOCK_TAG);
        ChatManager.Register<FormatTagHandler>(FORMAT_TAG);
    }

    public override void Unload()
    {
        UnregisterTags();
        Tooltips.TooltipBlockRegistry.Clear();
    }

    private static void UnregisterTags()
    {
        FieldInfo? field = typeof(ChatManager).GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Static);

        if (field?.GetValue(null) is not IDictionary handlers)
            return;

        foreach (string tag in Tags)
            handlers.Remove(tag);
    }
}

/// <summary>Renders a buff's icon and localised name inline.</summary>
public sealed class BuffTagHandler : ITagHandler
{
    /// <summary>
    ///     Default label tint. Warm enough to read as a keyword against ordinary tooltip grey
    ///     without looking like an error.
    /// </summary>
    private static readonly Color DefaultLabelColor = new(255, 179, 71);

    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        string raw = $"[{InlineTagSystem.BUFF_TAG}:{text}]";

        if (!TryResolveBuff(text, out int type))
            return new TextSnippet(raw, baseColor);

        Color labelColor = InlineTags.ParseColor(options) ?? DefaultLabelColor;

        return new InlineTextureSnippet(
            TextureAssets.Buff[type],
            raw,
            labelColor,
            Lang.GetBuffName(type)
        );
    }

    private static bool TryResolveBuff(string text, out int type)
    {
        text = text.Trim();

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out type))
            return type > 0 && type < BuffLoader.BuffCount;

        // Explicit "ModName/InternalName", for referring to another mod's buff.
        int slash = text.IndexOf('/');

        if (slash > 0)
        {
            string modName = text[..slash];
            string buffName = text[(slash + 1)..];

            if (ModContent.TryFind(modName, buffName, out ModBuff external))
            {
                type = external.Type;
                return true;
            }

            type = 0;
            return false;
        }

        // Bare name: this mod first, since that is what content here will be referring to.
        if (ModContent.TryFind(nameof(TrelamiumIII), text, out ModBuff buff))
        {
            type = buff.Type;
            return true;
        }

        return BuffID.Search.TryGetId(text, out type);
    }
}

/// <summary>Renders an arbitrary texture inline. The escape hatch for anything without a buff behind it.</summary>
public sealed class InlineTextureTagHandler : ITagHandler
{
    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        string raw = $"[{InlineTagSystem.TEXTURE_TAG}:{text}]";
        string path = text.Trim();

        if (!ModContent.HasAsset(path))
            return new TextSnippet(raw, baseColor);

        float scale = 1f;

        if (!string.IsNullOrWhiteSpace(options)
            && float.TryParse(options, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            && parsed > 0f)
        {
            scale = parsed;
        }

        return new InlineTextureSnippet(ModContent.Request<Texture2D>(path), raw, baseColor, iconScale: scale);
    }
}

/// <summary>Renders a chemical damage type's name and element medallion inline.</summary>
/// <remarks>
///     The tag names a <see cref="ChemicalDamageType" /> rather than a texture path, so the medallion
///     beside the word is always the one that element actually uses. Sprite, tint and label all
///     come from <see cref="ElementDisplay" />, the same place the controller stat rows read them.
/// </remarks>
public sealed class ChemicalTagHandler : ITagHandler
{
    TextSnippet ITagHandler.Parse(string text, Color baseColor, string options)
    {
        string raw = $"[{InlineTagSystem.CHEMICAL_TAG}:{text}]";

        if (!TryResolveType(text, out ChemicalDamageType type))
            return new TextSnippet(raw, baseColor);

        return new InlineTextureSnippet(
            ElementDisplay.Icon(type),
            raw,
            InlineTags.ParseColor(options) ?? ElementDisplay.LabelColor(type),
            ElementDisplay.DisplayName(type)
        );
    }

    private static bool TryResolveType(string text, out ChemicalDamageType type)
    {
        // TryParse also accepts numbers, and hands back an undefined value for one out of range
        // rather than failing, so IsDefined is what keeps a typo from reaching Request.
        return Enum.TryParse(text.Trim(), ignoreCase: true, out type)
            && Enum.IsDefined(type)
            && type != ChemicalDamageType.None;
    }
}

internal static class InlineTags
{
    /// <summary>Parses an <c>RRGGBB</c> tag option, matching vanilla's <c>[c/hex:...]</c> convention.</summary>
    internal static Color? ParseColor(string options)
    {
        if (string.IsNullOrWhiteSpace(options))
            return null;

        if (!uint.TryParse(options.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed))
            return null;

        return new Color((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
    }
}
