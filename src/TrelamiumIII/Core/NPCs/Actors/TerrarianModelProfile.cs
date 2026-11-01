namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Puts every NPC of one type - a vanilla one, usually - in a <see cref="TerrarianModel"/> without a subclass. Loaded
/// automatically; <see cref="TerrarianModelComponent"/> does the rest.
///
/// <para>
/// The NPC keeps its own AI, housing, chat and everything else. The model only shows it: dressed once by
/// <see cref="Dress"/>, then posed every tick by <see cref="Pose"/> from whatever state the NPC's AI is in. One
/// profile per type.
/// </para>
/// </summary>
public abstract class TerrarianModelProfile : ILoadable
{
    /// <summary>The NPC type this profile dresses.</summary>
    public abstract int NPCType { get; }

    /// <summary>Hair, skin, clothes, equipment. Called once, when the model is first needed.</summary>
    public abstract void Dress(TerrarianModel model);

    /// <summary>
    /// Poses the model from the NPC's state, every tick before the puppet updates. <paramref name="stateTicks"/> counts
    /// ticks since <c>ai[0]</c> last changed, 0 on the first. Defaults to vanilla's town AI, unarmed.
    /// </summary>
    public virtual void Pose(TerrarianModel model, int stateTicks) => TownPoses.ApplyVanilla(model, stateTicks, weapon: ItemID.None);

    void ILoadable.Load(Mod mod) => TerrarianModelComponent.Register(this);

    void ILoadable.Unload() { }
}
