using System.Collections.Generic;
using System.Linq;
using ReLogic.Content;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.UI.World;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The marker over an NPC with work going: bobs above their head, and opens their
///     <see cref="JobBoardPanel" /> when clicked.
/// </summary>
/// <remarks>
///     The click only opens the board — it no longer starts anything. Accepting happens behind a
///     confirmation inside the panel, so walking past a townsfolk cannot commit the player to a job.
/// </remarks>
internal sealed class JobIndicator : WorldIndicator
{
    /// <summary>How far above the NPC's head the icon floats, in world pixels.</summary>
    private static readonly Vector2 HeadOffset = new(0f, -40f);

    private readonly NPC _npc;
    private readonly Texture2D _icon;

    public override AnimationType AnimationStyle => AnimationType.Wag;

    private JobIndicator(NPC npc, Texture2D icon) : base(npc.Top, icon.Width, icon.Height, AnimationType.Wag)
    {
        _npc = npc;
        _icon = icon;

        TrackEntity(npc, HeadOffset);
        OnClick += OpenBoard;
    }

    /// <summary>
    ///     Builds an indicator for an NPC, or returns null when there is nothing to mark. Callers
    ///     register the result with <see cref="WorldUISystem" /> and hold onto it themselves.
    /// </summary>
    public static JobIndicator? TryCreate(NPC npc)
    {
        if (Main.dedServ || npc is not { active: true } || !AvailableJobs(npc).Any())
            return null;

        var icon = ModContent
            .Request<Texture2D>(Assets.Textures.UI.Jobs.JobAvailable.KEY, AssetRequestMode.ImmediateLoad)
            .Value;

        return icon == null ? null : new JobIndicator(npc, icon);
    }

    private static IEnumerable<Job> AvailableJobs(NPC npc)
        => Main.LocalPlayer.GetModPlayer<JobPlayer>().AvailableJobsFrom(npc.type);

    protected override void PostUpdate()
    {
        // Nothing left on offer: the job was taken, or its provider changed. Retire rather than
        // hide, since an NPC that has run out of work has no reason to be marked again.
        if (!AvailableJobs(_npc).Any())
        {
            IsActive = false;
            return;
        }

        // Step aside for dialogue rather than disappearing, so the icon comes back when it ends.
        if (DialogueManager.Instance.IsAnyActive())
            Hide();
        else
            Show();
    }

    private void OpenBoard()
    {
        List<Job> jobs = [.. AvailableJobs(_npc)];

        if (jobs.Count == 0)
        {
            IsActive = false;
            return;
        }

        WorldUISystem.OpenPanelState(new JobBoardPanel(_npc, jobs));
    }

    protected override void DrawIcon(SpriteBatch spriteBatch, Vector2 uiPosition, float opacity)
    {
        spriteBatch.Draw(
            _icon,
            uiPosition,
            null,
            (IsHovering ? Color.White : Color.White * 0.8f) * opacity,
            GetAnimationRotation(),
            _icon.Size() / 2f,
            IconScale,
            SpriteEffects.None,
            0f);
    }
}
