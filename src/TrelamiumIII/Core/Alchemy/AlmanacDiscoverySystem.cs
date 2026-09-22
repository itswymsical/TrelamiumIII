using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;
using TrelamiumIII.Content.Alchemy;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Per-world Almanac discovery: brewed outputs and obtained materials. Hidden ingredient entries show as "?" until obtained.
/// </summary>
public class AlmanacDiscoverySystem : ModSystem, Common.Subworlds.ISharedWorldData
{
    private static readonly HashSet<int> _unlocked = new();      // brewed outputs
    private static readonly HashSet<int> _ingredients = new();   // obtained materials

    public static IReadOnlyCollection<int> Unlocked => _unlocked;
    public static int Count => _unlocked.Count;
    public static bool IsUnlocked(int itemType) => itemType > 0 && _unlocked.Contains(itemType);

    /// <summary>Mark a brewed output as discovered. Returns true if newly added.</summary>
    public static bool Unlock(int itemType)
    {
        if (itemType <= 0) return false;
        return _unlocked.Add(itemType);
    }

    public static bool IsIngredientObtained(int itemType) =>
        itemType > 0 && _ingredients.Contains(itemType);

    /// <summary>Mark a material as obtained. Returns true if newly added.</summary>
    public static bool ObtainIngredient(int itemType)
    {
        if (itemType <= 0) return false;

        // Announce only when this actually reveals a "?" slot: starter (Hidden = false)
        // materials are visible from the start, and repeat obtains shouldn't re-toast.
        bool wasVisible = IsIngredientVisible(itemType);
        if (!_ingredients.Add(itemType)) return false;

        //if (!wasVisible)
        //    Main.NewText($"[i:{ModContent.ItemType<AlchemistsAlmanacItem>()}] The almanac has updated with a new ingredient!", Color.GreenYellow);
        //

        return true;
    }

    /// <summary>Shown in the Ingredients index if authored as visible (Hidden = false) or obtained.</summary>
    public static bool IsIngredientVisible(int itemType) =>
        itemType > 0 && (!AlmanacEntryRegistry.For(itemType).Hidden || _ingredients.Contains(itemType));

    // Bulk discovery toggles, for the /almanac dev command. These deliberately bypass
    // ObtainIngredient: its "new ingredient!" toast would fire once per entry.

    /// <summary>Discover every output the Recipes index lists. Returns how many were newly added.</summary>
    public static int UnlockAllRecipes()
    {
        int added = 0;
        foreach (int type in AlmanacIndex.AllOutputTypes)
            if (Unlock(type)) added++;
        return added;
    }

    /// <summary>Obtain every material the Ingredients index lists. Returns how many were newly added.</summary>
    public static int UnlockAllIngredients()
    {
        int added = 0;
        foreach (int type in AlchemyIngredients.AllIngredientTypes)
            if (type > 0 && _ingredients.Add(type)) added++;
        return added;
    }

    /// <summary>Forget every brewed output. Returns how many were cleared.</summary>
    public static int LockAllRecipes()
    {
        int cleared = _unlocked.Count;
        _unlocked.Clear();
        return cleared;
    }

    /// <summary>Forgets every obtained material; Hidden = false entries stay visible. Returns how many were cleared.</summary>
    public static int LockAllIngredients()
    {
        int cleared = _ingredients.Count;
        _ingredients.Clear();
        return cleared;
    }

    public override void ClearWorld()
    {
        _unlocked.Clear();
        _ingredients.Clear();
    }

    public override void SaveWorldData(TagCompound tag)
    {
        SaveSet(_unlocked, tag, "almanacVanilla", "almanacMod");
        SaveSet(_ingredients, tag, "almanacIngVanilla", "almanacIngMod");
    }

    public override void LoadWorldData(TagCompound tag)
    {
        _unlocked.Clear();
        _ingredients.Clear();
        LoadSet(_unlocked, tag, "almanacVanilla", "almanacMod");
        LoadSet(_ingredients, tag, "almanacIngVanilla", "almanacIngMod");
    }

    // Modded item IDs aren't stable across reloads, so those persist by mod/internal name.
    private static void SaveSet(HashSet<int> set, TagCompound tag, string vanillaKey, string modKey)
    {
        if (set.Count == 0) return;

        var vanilla = new List<int>();
        var modded = new List<string>();

        foreach (int type in set)
        {
            if (type < ItemID.Count)
            {
                vanilla.Add(type);
                continue;
            }

            var probe = new Item();
            probe.SetDefaults(type);
            if (probe.ModItem != null)
                modded.Add($"{probe.ModItem.Mod.Name}/{probe.ModItem.Name}");
        }

        if (vanilla.Count > 0) tag[vanillaKey] = vanilla;
        if (modded.Count > 0) tag[modKey] = modded;
    }

    private static void LoadSet(HashSet<int> set, TagCompound tag, string vanillaKey, string modKey)
    {
        if (tag.ContainsKey(vanillaKey))
            foreach (int t in tag.GetList<int>(vanillaKey))
                set.Add(t);

        if (tag.ContainsKey(modKey))
        {
            foreach (string entry in tag.GetList<string>(modKey))
            {
                int slash = entry.IndexOf('/');
                if (slash <= 0 || slash >= entry.Length - 1) continue;

                string modName = entry[..slash];
                string itemName = entry[(slash + 1)..];

                if (ModContent.TryFind<ModItem>(modName, itemName, out var mi))
                    set.Add(mi.Type);
            }
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(_unlocked.Count);
        foreach (int t in _unlocked) writer.Write(t);
        writer.Write(_ingredients.Count);
        foreach (int t in _ingredients) writer.Write(t);
    }

    public override void NetReceive(BinaryReader reader)
    {
        _unlocked.Clear();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++) _unlocked.Add(reader.ReadInt32());

        _ingredients.Clear();
        count = reader.ReadInt32();
        for (int i = 0; i < count; i++) _ingredients.Add(reader.ReadInt32());
    }
}

/// <summary>Marks alchemical materials in the local player's inventory as obtained, revealing their "?" slots.</summary>
public class IngredientDiscoveryPlayer : ModPlayer
{
    private int _timer;

    public override void PostUpdate()
    {
        if (Player.whoAmI != Main.myPlayer) return;
        if (++_timer < 30) return;   // twice a second is plenty
        _timer = 0;

        foreach (var item in Player.inventory)
        {
            if (item is null || item.IsAir) continue;
            if (AlmanacDiscoverySystem.IsIngredientObtained(item.type)) continue;
            if (AlchemyIngredients.IsSubstanceType(item.type) || AlchemyIngredients.IsInputType(item.type))
                AlmanacDiscoverySystem.ObtainIngredient(item.type);
        }
    }
}
