using System.Collections.Generic;
using TrelamiumIII.Core.Components.NPCs;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Draws an NPC as a <see cref="TerrarianModel"/> in place of its sprite. How a vanilla NPC gets a player model without
/// a subclass, the way <see cref="Dialogue.NarrativeChat.Register"/> gives it narrative chat.
///
/// <para>
/// Instantiated only on types a <see cref="TerrarianModelProfile"/> names. The model is built the first time it is
/// needed rather than in <see cref="SetDefaults"/>, which also runs for content samples and bestiary dummies, and is
/// then updated after the NPC's AI and drawn in place of its sprite.
/// </para>
///
/// <para>
/// Bestiary dummies keep the vanilla sprite: the icon draws them in UI space, which a world-space player render
/// can't follow (see <see cref="TerrarianEntryIcon"/>). Disable the component to hand the NPC its sprite back.
/// </para>
/// </summary>
public sealed class TerrarianModelComponent : NPCComponent
{
    private static readonly Dictionary<int, TerrarianModelProfile> profiles = [];

    /// <summary>Set in <see cref="SetDefaults"/>, before anything can ask for the model.</summary>
    private NPC npc = null!;
    private TerrarianModelProfile profile = null!;
    private TerrarianModel? model;

    /// <summary><c>ai[0]</c> as of the last tick, and how many ticks it has held.</summary>
    private float lastState = float.NaN;
    private int stateTicks;

    /// <summary>The model, built and dressed on first use.</summary>
    public TerrarianModel Model => model ??= Build();

    internal static void Register(TerrarianModelProfile profile)
    {
        if (!profiles.TryAdd(profile.NPCType, profile))
            throw new InvalidOperationException($"NPC type {profile.NPCType} already has a {nameof(TerrarianModelProfile)}: {profiles[profile.NPCType].GetType().Name}.");
    }

    public override void Unload() => profiles.Clear();

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => profiles.ContainsKey(entity.type);

    public override void SetDefaults(NPC entity)
    {
        npc = entity;
        profile = profiles[entity.type];
        Enabled = true;
    }

    private TerrarianModel Build()
    {
        var built = new TerrarianModel(npc);
        profile.Dress(built);

        return built;
    }

    public override void PostAI(NPC npc)
    {
        if (!Enabled)
            return;

        if (npc.ai[0] == lastState)
        {
            stateTicks++;
        }
        else
        {
            lastState = npc.ai[0];
            stateTicks = 0;
        }

        profile.Pose(Model, stateTicks);

        // Last, so the pose reflects this tick's final state and a use started this tick shows its first frame.
        Model.Puppet.Update();
    }

    public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => !Shows(npc);

    public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        if (!Shows(npc))
            return;

        Model.Draw(spriteBatch);
        Model.DrawDebug(spriteBatch);
    }

    private bool Shows(NPC npc) => Enabled && !npc.IsABestiaryIconDummy;
}
