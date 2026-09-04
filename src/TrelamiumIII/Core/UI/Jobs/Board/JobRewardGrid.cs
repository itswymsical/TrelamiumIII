using System.Linq;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.UI.World;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The reward payout for a job, as a grid of inventory slots that resizes itself to fit however
///     many rewards there are.
/// </summary>
/// <remarks>
///     Read-only on purpose. A <see cref="BasicItemSlot" /> would hand clicks to vanilla's
///     <see cref="ItemSlot" /> and let the player pull the rewards straight out of the preview.
/// </remarks>
internal sealed class JobRewardGrid : UIElement
{
    private const int SLOT_GAP = 2;

    private Job? _job;
    private Layout _layout;

    public Func<float>? Opacity;

    private float Alpha => Opacity?.Invoke() ?? 1f;

    /// <summary>True when there is anything at all to show; the panel hides the header otherwise.</summary>
    public bool HasRewards => _layout.HasRewards;

    public JobRewardGrid(float width)
    {
        Width = StyleDimension.FromPixels(width);
        Height = StyleDimension.FromPixels(0f);
    }

    public void SetJob(Job job)
    {
        _job = job;
        _layout = Measure(job);

        Height = StyleDimension.FromPixels(
            _layout.HasRewards ? _layout.Rows * (_layout.SlotSize + SLOT_GAP) : 0f);

        Recalculate();
    }

    #region Layout
    private struct Layout
    {
        public bool HasRewards;
        public int Total;
        public int Columns;
        public int Rows;
        public int SlotSize;
        public float ItemScale;
    }

    /// <summary>
    ///     Picks a grid shape for the reward count: a handful get large slots, a hoard gets small
    ///     ones packed tighter, so the block stays the same rough size either way.
    /// </summary>
    private static Layout Measure(Job job)
    {
        var layout = new Layout();

        if (job == null)
            return layout;

        layout.Total = job.Rewards.Count(r => r.IsValid);
        layout.HasRewards = layout.Total > 0;

        if (!layout.HasRewards)
            return layout;

        (int perRow, int slot, float scale) = layout.Total switch
        {
            <= 3 => (layout.Total, 32, 1.0f),
            <= 6 => (3, 28, 0.85f),
            <= 12 => (4, 24, 0.7f),
            _ => (5, 20, 0.6f)
        };

        layout.Columns = perRow;
        layout.Rows = (int)Math.Ceiling(layout.Total / (double)perRow);
        layout.SlotSize = slot;
        layout.ItemScale = scale;

        return layout;
    }
    #endregion

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        if (_job == null || !_layout.HasRewards)
            return;

        float alpha = Alpha;
        if (alpha <= 0f)
            return;

        Rectangle bounds = GetDimensions().ToRectangle();
        int spacing = _layout.SlotSize + SLOT_GAP;
        int drawn = 0;

        foreach (JobReward reward in _job.Rewards)
        {
            Item item = reward.GetItem();
            if (item == null || item.type <= ItemID.None)
                continue;

            int x = bounds.X + drawn % _layout.Columns * spacing;
            int y = bounds.Y + drawn / _layout.Columns * spacing;

            DrawSlot(spriteBatch, item, new Rectangle(x, y, _layout.SlotSize, _layout.SlotSize), alpha);
            drawn++;
        }
    }

    private void DrawSlot(SpriteBatch spriteBatch, Item item, Rectangle slot, float alpha)
    {
        spriteBatch.Draw(TextureAssets.InventoryBack.Value, slot, Color.White * (0.8f * alpha));

        Texture2D texture = TextureAssets.Item[item.type].Value;

        // Items vary wildly in texture size, so fit each one to the slot rather than trusting the
        // grid's nominal scale — a tall sword would otherwise spill over its neighbours.
        float fit = Math.Min(
            1f,
            Math.Min(_layout.SlotSize / (float)texture.Width, _layout.SlotSize / (float)texture.Height));

        float scale = _layout.ItemScale * fit;
        var size = new Vector2(texture.Width, texture.Height) * scale;

        spriteBatch.Draw(
            texture,
            new Vector2(slot.X + (slot.Width - size.X) / 2f, slot.Y + (slot.Height - size.Y) / 2f),
            null,
            Color.White * alpha,
            0f,
            Vector2.Zero,
            scale,
            SpriteEffects.None,
            0f);

        if (item.stack > 1)
        {
            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.ItemStack.Value,
                item.stack.ToString(),
                slot.X + 2,
                slot.Y + slot.Height - 14,
                Color.White * alpha,
                Color.Black * alpha,
                Vector2.Zero,
                Math.Max(0.6f, _layout.ItemScale * 0.8f));
        }

        if (!IsMouseHovering || !slot.Contains(WorldUI.MouseUI.ToPoint()))
            return;

        // Vanilla's own hover path, so reward items show the same tooltip they would anywhere else.
        Main.HoverItem = item.Clone();
        Main.hoverItemName = item.Name;
        Main.LocalPlayer.mouseInterface = true;
    }
}
