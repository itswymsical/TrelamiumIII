using MonoMod.Cil;
using System.Collections.Generic;

namespace TrelamiumIII.Common.Systems;

/// <summary>
/// Lets vanilla cattails and lily pads stand on modded tiles. <see cref="WorldGen.CheckCatTail"/> and
/// <see cref="WorldGen.CheckLilyPad"/> pick the plant's style from its anchor's type and break the plant on types they
/// don't list, so both read the anchor type through <see cref="ResolveAnchor"/>.
/// </summary>
public class WaterPlantAnchorSystem : ModSystem
{
    private static readonly Dictionary<int, int> anchorAliases = [];

    /// <summary> Cattails and lily pads on <paramref name="type"/> survive and pick their style as on <paramref name="vanillaType"/>. </summary>
    public static void AddAnchorAlias(int type, int vanillaType) => anchorAliases[type] = vanillaType;

    public static int ResolveAnchor(int type) => anchorAliases.TryGetValue(type, out int vanillaType) ? vanillaType : type;

    public override void Load()
    {
        IL_WorldGen.CheckCatTail += ResolveAnchorType;
        IL_WorldGen.CheckLilyPad += ResolveAnchorType;
    }

    public override void Unload()
    {
        IL_WorldGen.CheckCatTail -= ResolveAnchorType;
        IL_WorldGen.CheckLilyPad -= ResolveAnchorType;
        anchorAliases.Clear();
    }

    /// <summary> Both methods store the anchor's type in a local and set the style local to -1 straight after. </summary>
    private void ResolveAnchorType(ILContext il)
    {
        ILCursor c = new(il);

        if (!c.TryGotoNext(MoveType.Before,
            i => i.MatchCall<Tile>("get_type"),
            i => i.MatchLdindU2(),
            i => i.MatchStloc(out _),
            i => i.MatchLdcI4(-1)))
        {
            Mod.Logger.Warn($"Could not patch WorldGen.{il.Method.Name}, failed to match the anchor type read before the style lookup.");

            throw new ILPatchFailureException(Mod, il, null);
        }

        c.Index += 2;
        c.EmitDelegate<Func<int, int>>(ResolveAnchor);
    }
}
