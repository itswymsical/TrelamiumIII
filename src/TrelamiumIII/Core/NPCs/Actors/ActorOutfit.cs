using System.Collections.Generic;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Clothing for a <see cref="TerrarianModel"/> given as textures rather than items: head, body and leg sheets in the
/// player armour layout, registered as equip textures with no item behind them.
///
/// <para>
/// The sheets take the vanilla shapes - 40x1120 for head and legs, the 360x224 composite sheet for the body - and
/// draw through the ordinary armour layers, so arm compositing, use-style poses and dyes all work on them. A slot
/// the outfit leaves null falls through to whatever the armour slots say.
/// </para>
///
/// <para>
/// Hide what a sheet already paints in <see cref="ModType.SetStaticDefaults"/>, as an armour item would:
/// <c>ArmorIDs.Head.Sets.DrawHead[HeadSlot] = false</c> for a sheet with the face on it,
/// <c>ArmorIDs.Body.Sets.HidesTopSkin[BodySlot]</c> and <c>ArmorIDs.Legs.Sets.HidesBottomSkin[LegsSlot]</c> for skin.
/// </para>
/// </summary>
public abstract class ActorOutfit : ModType
{
    private static readonly List<ActorOutfit> registered = [];

    /// <summary>Head sheet asset path. Null leaves the head to the armour slots.</summary>
    public virtual string? HeadTexture => null;

    /// <summary>Body sheet asset path, in the composite layout. Null leaves the body to the armour slots.</summary>
    public virtual string? BodyTexture => null;

    /// <summary>Legs sheet asset path. Null leaves the legs to the armour slots.</summary>
    public virtual string? LegsTexture => null;

    /// <summary>Equip slot of <see cref="HeadTexture"/>, or -1.</summary>
    public int HeadSlot { get; private set; } = -1;

    /// <summary>Equip slot of <see cref="BodyTexture"/>, or -1.</summary>
    public int BodySlot { get; private set; } = -1;

    /// <summary>Equip slot of <see cref="LegsTexture"/>, or -1.</summary>
    public int LegsSlot { get; private set; } = -1;

    /// <summary>Index among loaded outfits, the same on every side. Sent instead of the name.</summary>
    public int Type { get; private set; }

    /// <summary>The outfit with <paramref name="type"/>, or null.</summary>
    public static ActorOutfit? Get(int type) => type >= 0 && type < registered.Count ? registered[type] : null;

    protected sealed override void Register()
    {
        ModTypeLookup<ActorOutfit>.Register(this);

        Type = registered.Count;
        registered.Add(this);

        HeadSlot = AddTexture(HeadTexture, EquipType.Head);
        BodySlot = AddTexture(BodyTexture, EquipType.Body);
        LegsSlot = AddTexture(LegsTexture, EquipType.Legs);
    }

    public sealed override void SetupContent() => SetStaticDefaults();

    public override void Unload() => registered.Clear();

    private int AddTexture(string? texture, EquipType type)
        => texture == null ? -1 : EquipLoader.AddEquipTexture(Mod, texture, type, name: $"{Name}_{type}");

    /// <summary>Lays this outfit's slots over the ones the armour gave <paramref name="player"/>.</summary>
    internal void Apply(Player player)
    {
        if (HeadSlot >= 0)
            player.head = HeadSlot;

        if (BodySlot >= 0)
            player.body = BodySlot;

        if (LegsSlot >= 0)
            player.legs = LegsSlot;
    }
}

/// <summary>
/// Wears an <see cref="ActorOutfit"/>. Null, the default and every real player's, wears the armour.
///
/// <para>
/// Setting <see cref="Player.head"/> and friends directly doesn't stick: <see cref="Player.PlayerFrame"/> rewrites them
/// from the armour slots on every call. It runs this hook straight after, and works out the body and leg frames after
/// that, so the outfit is what gets framed and drawn - in the world, the bestiary and the map head alike.
/// </para>
/// </summary>
public sealed class ActorOutfitPlayer : ModPlayer
{
    public ActorOutfit? Outfit { get; set; }

    public override void FrameEffects() => Outfit?.Apply(Player);
}
