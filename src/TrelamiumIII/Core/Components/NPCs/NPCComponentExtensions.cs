using System.Diagnostics.CodeAnalysis;
using Terraria;

namespace TrelamiumIII.Core.Components.NPCs;

/// <summary>
///     Provides <see cref="NPC"/> extension methods regarding <see cref="NPCComponent"/>.
/// </summary>
/// <remarks>
///     Kept outside of the <c>Utilities.Extensions</c> namespace for convenience.
/// </remarks>
public static class NPCComponentExtensions
{
    public static bool TryEnable<T>(this NPC npc, [NotNullWhen(true)] out T component) where T : NPCComponent
    {
        if (!npc.TryGetGlobalNPC(out component))
        {
            return false;
        }
        
        component!.Enabled = true;

        return true;
    }
}