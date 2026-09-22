using System.Collections.Generic;
using System.Linq;
using ReLogic.Content;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.UI.World;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The marker over an NPC with work going: bobs above their head, stands a
///     <see cref="JobHoverCard" /> up while the cursor rests on it, and opens their
///     <see cref="ProjectedJobBoard" /> when clicked.
/// </summary>
internal sealed class JobIndicator : WorldIndicator
{
    /// <summary>How far above the NPC's head the icon floats, in world pixels.</summary>
    private static readonly Vector2 HeadOffset = new(0f, -40f);

    private readonly NPC _npc;
    private readonly Texture2D _icon;
    private readonly JobHoverCard _card;

    public override AnimationType AnimationStyle => AnimationType.Wag;

    private JobIndicator(NPC npc, Texture2D icon) : base(npc.Top, icon.Width, icon.Height, AnimationType.Wag)
    {
        _npc = npc;
        _icon = icon;
        _card = new JobHoverCard(icon, () => IsActive);

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
        => Main.LocalPlayer.GetModPlayer<JobPlayer>().AvailableJobsFrom(npc);

    protected override void PostUpdate()
    {
        List<Job> jobs = [.. AvailableJobs(_npc)];

        // Nothing left on offer: the job was taken, or its provider changed. Retire rather than
        // hide, since an NPC that has run out of work has no reason to be marked again.
        if (jobs.Count == 0)
        {
            _card.Retire();
            IsActive = false;
            return;
        }

        // Step aside for dialogue and for an open board rather than disappearing, so the icon
        // comes back when they end. The board hangs off the same head, and would sit on top of it.
        if (DialogueManager.Instance.IsAnyActive() || WorldUISystem.HasOpenPanel)
            Hide();
        else
            Show();

        _card.SetJobs(jobs);
        _card.Update(IsHovering && !IsHidden, IconTop());
    }

    /// <summary>The top of the icon as drawn, in raw screen pixels: where the card's tail points.</summary>
    private Vector2 IconTop()
    {
        Vector2 centre = WorldUI.ToScreen(WorldAnchor + Offset);
        float half = Height * 0.5f * GetAnimationScale() * WorldUI.ZoomScale;

        return centre - new Vector2(0f, half);
    }

    private void OpenBoard()
    {
        List<Job> jobs = [.. AvailableJobs(_npc)];

        if (jobs.Count == 0)
        {
            IsActive = false;
            return;
        }

        WorldUISystem.OpenPanelState(new ProjectedJobBoard(_npc, jobs));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        // After the icon, so the card's tail lands on top of it rather than under it.
        _card.Draw(spriteBatch);
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
