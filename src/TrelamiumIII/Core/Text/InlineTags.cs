using System.Collections;
using System.Globalization;
using System.Reflection;
using Terraria.GameContent;
using Terraria.UI.Chat;

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
///             <c>[tex:TrelamiumIII/Content/Fungore/FungalInfection]</c> -- any texture, for cases
///             with no buff behind them. The option is a scale multiplier: <c>[tex/0.5:Path]</c>.
///         </item>
///     </list>
/// </remarks>
public sealed class InlineTagSystem : ModSystem
{
    internal const string BUFF_TAG = "buff";
    internal const string TEXTURE_TAG = "tex";

    public override void Load()
    {
        // Terraria's registry is a static dictionary that uses Add, so a leftover entry from a
        // previous load would throw on the next one. Clearing first makes reloads survivable.
        UnregisterTags();

        ChatManager.Register<BuffTagHandler>(BUFF_TAG);
        ChatManager.Register<InlineTextureTagHandler>(TEXTURE_TAG);
    }

    public override void Unload() => UnregisterTags();

    private static void UnregisterTags()
    {
        FieldInfo? field = typeof(ChatManager).GetField("_handlers", BindingFlags.NonPublic | BindingFlags.Static);

        if (field?.GetValue(null) is not IDictionary handlers)
            return;

        handlers.Remove(BUFF_TAG);
        handlers.Remove(TEXTURE_TAG);
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
