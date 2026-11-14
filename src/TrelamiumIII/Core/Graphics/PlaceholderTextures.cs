using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace TrelamiumIII.Core.Graphics;

/// <summary>
///     Gives this mod's content a placeholder sprite when its conventional texture path has no file, instead of failing
///     the mod load. Hooks the base <c>Texture</c> getters, so an override that doesn't call base is never replaced.
/// </summary>
public static class PlaceholderTextures
{
    /// <param name="Kind">tModLoader base type without the "Mod" prefix: Item, Tile, Buff...</param>
    public readonly record struct Entry(string Kind, string Name, string ExpectedPath);

    private static readonly ConcurrentDictionary<string, string> resolvedPaths = new();

    private static readonly ConcurrentDictionary<string, Entry> missing = new();

    private static Mod? owner;

    /// <summary>Complete once content setup has requested every texture.</summary>
    public static ICollection<Entry> Missing => missing.Values;

    /// <summary>Called from the mod constructor, since <c>AutoloadEquip</c> reads <c>Texture</c> during Register.</summary>
    internal static void Install(Mod mod)
    {
        owner = mod;

        MonoModHooks.Add(TextureGetter(typeof(ModItem)), ItemTexture);
        MonoModHooks.Add(TextureGetter(typeof(ModNPC)), NPCTexture);
        MonoModHooks.Add(TextureGetter(typeof(ModProjectile)), ProjectileTexture);
        MonoModHooks.Add(TextureGetter(typeof(ModTexturedType)), TexturedTypeTexture);
    }

    private static MethodInfo TextureGetter(Type type)
        => type.GetProperty("Texture", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!.GetMethod!;

    private static string ItemTexture(Func<ModItem, string> orig, ModItem self)
<<<<<<< HEAD
        => Resolve(self, orig(self), Assets.Textures.Placeholder.PlaceholderItem.KEY);

    private static string NPCTexture(Func<ModNPC, string> orig, ModNPC self)
        => Resolve(self, orig(self), Assets.Textures.Placeholder.PlaceholderItem.KEY);

    private static string ProjectileTexture(Func<ModProjectile, string> orig, ModProjectile self)
        => Resolve(self, orig(self), Assets.Textures.Placeholder.PlaceholderItem.KEY);
=======
        => Resolve(self, orig(self), Assets.Textures.PlaceholderItem.KEY);

    private static string NPCTexture(Func<ModNPC, string> orig, ModNPC self)
        => Resolve(self, orig(self), Assets.Textures.PlaceholderItem.KEY);

    private static string ProjectileTexture(Func<ModProjectile, string> orig, ModProjectile self)
        => Resolve(self, orig(self), Assets.Textures.PlaceholderItem.KEY);
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb

    private static string TexturedTypeTexture(Func<ModTexturedType, string> orig, ModTexturedType self)
        => Resolve(self, orig(self), self switch
        {
<<<<<<< HEAD
            ModTile => Assets.Textures.Placeholder.FallbackTile.KEY,
            ModWall => Assets.Textures.Placeholder.FallbackWall.KEY,
            ModBuff => Assets.Textures.Placeholder.PlaceholderBuff.KEY,
            _ => Assets.Textures.Placeholder.PlaceholderItem.KEY
=======
            ModTile => Assets.Textures.FallbackTile.KEY,
            ModWall => Assets.Textures.FallbackWall.KEY,
            ModBuff => Assets.Textures.PlaceholderBuff.KEY,
            _ => Assets.Textures.PlaceholderItem.KEY
>>>>>>> 6caac6b18e663ba14545f8ff6bb0ae697d11ebfb
        });

    private static string Resolve(ModType self, string path, string placeholder)
    {
        // The content source exists from PrepareAssets on; a result from before then must not be cached.
        if (owner?.RootContentSource is not { } source || self.GetType().Assembly != typeof(PlaceholderTextures).Assembly)
            return path;

        if (resolvedPaths.TryGetValue(path, out var resolved))
            return resolved;

        // Read the source directly: ModContent.HasAsset is always false on a dedicated server, which still requests textures.
        var prefix = owner.Name + "/";
        var exists = !path.StartsWith(prefix) || source.HasAsset(path[prefix.Length..]);

        resolved = exists ? path : placeholder;
        resolvedPaths[path] = resolved;

        if (resolved != path && missing.TryAdd(path, new Entry(KindOf(self), self.Name, path)))
            owner.Logger.Info($"No texture at {path}; {self.Name} uses a placeholder.");

        return resolved;
    }

    private static string KindOf(ModType self)
    {
        var type = self.GetType();

        while (type.BaseType is { } baseType && type.Assembly != typeof(ModType).Assembly)
            type = baseType;

        return type.Name.StartsWith("Mod") ? type.Name[3..] : type.Name;
    }
}
