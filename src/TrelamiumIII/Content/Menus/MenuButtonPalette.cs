using MonoMod.Cil;

namespace TrelamiumIII.Content.Menus;

/// <summary>Implemented by a <see cref="ModMenu" /> to recolour the title-screen buttons while it is selected.</summary>
public interface IMenuButtonPalette
{
    /// <summary>
    ///     Replaces DrawMenu's day/night base colour. Buttons lighten it halfway to white, then dim idle ones by 40%.
    /// </summary>
    Color ButtonBase { get; }

    /// <summary>Fill a hovered button eases toward; vanilla eases toward (255, 215, 0).</summary>
    Color ButtonHover { get; }

    /// <summary>Colour of the hovered button's outline passes, drawn additively and scaled by hover strength.</summary>
    Color ButtonGlow { get; }

    /// <summary>When true the hovered fill turns additive as it eases in, so it glows over a dark scene.</summary>
    bool AdditiveHover { get; }
}

/// <summary>
///     Patches DrawMenu's button colours for the selected menu's <see cref="IMenuButtonPalette" />. Vanilla behaviour is
///     unchanged when the selected menu does not implement it.
/// </summary>
/// <remarks>Credit: the base-colour patch follows ZenTheMod's InkPondMenu (WizenkleBoss), via Reverie.</remarks>
public sealed class MenuButtonPaletteSystem : ModSystem
{
    private static IMenuButtonPalette? Palette => Main.gameMenu ? MenuLoader.CurrentMenu as IMenuButtonPalette : null;

    public override void Load()
    {
        IL_Main.DrawMenu += PatchDrawMenu;
    }

    private void PatchDrawMenu(ILContext il)
    {
        var c = new ILCursor(il);

        PatchBaseColor(c, il);
        PatchButtonColor(c, il);
    }

    /// <summary>After <c>Color color = new(b, b, b, 255)</c>, where b comes from Main.tileColor.</summary>
    private void PatchBaseColor(ILCursor c, ILContext il)
    {
        var color = 0;
        var shade = 0;

        if (!c.TryGotoNext(MoveType.After,
                i => i.MatchLdloca(out color),
                i => i.MatchLdloc(out shade),
                i => i.MatchLdloc(shade),
                i => i.MatchLdloc(shade),
                i => i.MatchLdcI4(255),
                i => i.MatchCall<Color>(".ctor")))
        {
            throw new ILPatchFailureException(Mod, il, new Exception("Menu base colour construction not found."));
        }

        c.EmitLdloca(color);
        c.EmitDelegate(static (ref Color value) =>
        {
            if (Palette is { } palette)
                value = palette.ButtonBase;
        });
    }

    /// <summary>
    ///     In the button loop: <c>if (focused == item &amp;&amp; layer == 4)</c> eases the fill toward yellow by
    ///     <c>alpha / 255</c>, then the colour is built from the eased channels. Layers 0-3 are the black outline passes.
    /// </summary>
    private void PatchButtonColor(ILCursor c, ILContext il)
    {
        var focused = 0;
        var item = 0;
        var layer = 0;
        var alpha = 0;

        if (!c.TryGotoNext(MoveType.After,
                i => i.MatchLdloc(out focused),
                i => i.MatchLdloc(out item),
                i => i.MatchBneUn(out _),
                i => i.MatchLdloc(out layer),
                i => i.MatchLdcI4(4),
                i => i.MatchBneUn(out _),
                i => i.MatchLdloc(out alpha),
                i => i.MatchConvR4(),
                i => i.MatchLdcR4(255f),
                i => i.MatchDiv(),
                i => i.MatchStloc(out _)))
        {
            throw new ILPatchFailureException(Mod, il, new Exception("Menu button hover lerp not found."));
        }

        ReplaceChannel(c, il, 255f, static palette => palette.ButtonHover.R);
        ReplaceChannel(c, il, 215f, static palette => palette.ButtonHover.G);
        ReplaceChannel(c, il, 0f, static palette => palette.ButtonHover.B);

        var color = 0;

        if (!c.TryGotoNext(MoveType.After,
                i => i.MatchLdloca(out color),
                i => i.MatchLdloc(out _),
                i => i.MatchConvU1(),
                i => i.MatchLdloc(out _),
                i => i.MatchConvU1(),
                i => i.MatchLdloc(out _),
                i => i.MatchConvU1(),
                i => i.MatchLdloc(alpha),
                i => i.MatchConvU1(),
                i => i.MatchCall<Color>(".ctor")))
        {
            throw new ILPatchFailureException(Mod, il, new Exception("Menu button colour construction not found."));
        }

        c.EmitLdloca(color);
        c.EmitLdloc(layer);
        c.EmitLdloc(focused);
        c.EmitLdloc(item);
        c.EmitDelegate(static (ref Color value, int pass, int focusedItem, int thisItem) =>
        {
            if (Palette is not { } palette || focusedItem != thisItem)
                return;

            // value.A is the hover strength. On a premultiplied colour, lowering A towards 0 makes AlphaBlend add.
            var strength = value.A / 255f;

            if (pass != 4)
                value = (palette.ButtonGlow * strength) with { A = 0 };
            else if (palette.AdditiveHover)
                value.A = (byte)(value.A * (1f - strength));
        });
    }

    /// <summary>Wraps the next <c>ldc.r4 <paramref name="vanilla" /></c> so it yields the palette's channel instead.</summary>
    private void ReplaceChannel(ILCursor c, ILContext il, float vanilla, Func<IMenuButtonPalette, byte> channel)
    {
        if (!c.TryGotoNext(MoveType.After, i => i.MatchLdcR4(vanilla)))
            throw new ILPatchFailureException(Mod, il, new Exception($"Menu hover channel {vanilla} not found."));

        c.EmitDelegate((float value) => Palette is { } palette ? channel(palette) : value);
    }
}
