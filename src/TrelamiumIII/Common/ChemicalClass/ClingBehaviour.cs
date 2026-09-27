using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>One element clinging to an NPC. Owned by <see cref="ChemicalGlobalNPC" />; behaviours keep their state here.</summary>
public struct ClingState
{
    public ClingBehaviour? Behaviour;

    public ChemicalDamageType Element;

    /// <summary>The gas the cling is made of, for visuals and anything it sheds. Null draws nothing.</summary>
    public ChemicalGasField? Medium;

    /// <summary>What the cling emits: the chemistry it came from, derived under <see cref="Scope" /> and never exposing the host.</summary>
    public ChemicalPayload? Payload;

    /// <summary>Ends when the cling detaches, which stops <see cref="Payload" /> applying anywhere.</summary>
    public ChemicalScope? Scope;

    public int Owner;

    public int TicksRemaining;

    public int MaxTicks;

    /// <summary>Ticks since the cling attached.</summary>
    public int Age;

    public readonly bool IsActive => Behaviour is not null && TicksRemaining > 0;

    /// <summary>1 until the last second of the cling, then fading to 0.</summary>
    public readonly float Fade => MathHelper.Clamp(TicksRemaining / 60f, 0f, 1f);
}

/// <summary>
///     What a clinging chemical does to its host. Instances are shared and stateless; per-cling state lives on the
///     <see cref="ClingState" />. Hooks run only on the machine that owns the cling (see <see cref="ChemicalPayload.IsAuthority" />).
/// </summary>
[CloneByReference]
public abstract class ClingBehaviour
{
    /// <summary>Rides the host as cosmetic gas and keeps its affliction running for the cling's duration.</summary>
    public static readonly ClingBehaviour Default = new DefaultCling();

    /// <summary>Ticks a cling attaching at <paramref name="concentration" /> lasts.</summary>
    public virtual int Duration(float concentration) => ChemicalTuning.ClingTicks(concentration);

    public virtual void OnAttach(NPC host, ref ClingState cling) { }

    /// <summary>Runs every tick while attached. Defaults to shedding cosmetic gas of the medium over the host.</summary>
    public virtual void Update(NPC host, ref ClingState cling) => cling.Medium?.ShedOnto(host, cling.Fade);

    /// <param name="hostKilled">True when the host died with the cling on; false when it wore off or was replaced.</param>
    /// <remarks>The cling's scope ends right after this, so anything meant to outlive it needs its own derivation.</remarks>
    public virtual void OnDetach(NPC host, ref ClingState cling, bool hostKilled) { }

    private sealed class DefaultCling : ClingBehaviour { }
}
