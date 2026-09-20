using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.ExperientialSystem;
using TrelamiumIII.Core.ExperientialSystem.Skills;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.SkillTree;

/// <summary>The skill tree's frame: a header reading the meter, over the node field.</summary>
internal sealed class SkillTreePanel : UIElement
{
    private const float HEADER_HEIGHT = 46f;
    private const float BODY_INSET = 14f;

    private SkillTreeField _field = null!;

    public override void OnInitialize()
    {
        _field = new SkillTreeField
        {
            Left = StyleDimension.FromPixels(BODY_INSET),
            Top = StyleDimension.FromPixels(HEADER_HEIGHT),
            Width = StyleDimension.FromPixelsAndPercent(-BODY_INSET * 2f, 1f),
            Height = StyleDimension.FromPixelsAndPercent(-(HEADER_HEIGHT + BODY_INSET), 1f),
        };

        _field.SetPadding(0f);

        Append(_field);
    }

    public void OnOpened() => _field.OnOpened();

    public void DrawTooltip(SpriteBatch spriteBatch) => _field.DrawTooltip(spriteBatch);

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        // Stops a click meant for a node from also swinging the held item or hitting a tile behind it.
        // Held through the close fade too, while the panel is still under the cursor.
        if (SkillTreeSystem.Openness > 0f && ContainsPoint(Main.MouseScreen))
            Main.LocalPlayer.mouseInterface = true;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        float fade = SkillTreeSystem.Fade;

        if (fade <= 0f)
            return;

        Rectangle bounds = GetDimensions().ToRectangle();

        // Point-sampled for the frame's pixel art only; the header text keeps the interface's own sampler.
        SkillTreeBatch.Scope scope = SkillTreeBatch.Point(spriteBatch);

        DrawUtils.DrawPanel(spriteBatch, bounds, new Color(10, 16, 34) * 0.92f, opacity: fade);

        SkillTreeBatch.Restore(spriteBatch, scope);

        DrawHeader(spriteBatch, bounds, fade);
    }

    private static void DrawHeader(SpriteBatch spriteBatch, Rectangle bounds, float fade)
    {
        var experiential = Main.LocalPlayer.GetModPlayer<ExperientialPlayer>();

        var title = new Vector2(bounds.X + BODY_INSET + 6f, bounds.Y + 12f);

        Utils.DrawBorderString(spriteBatch, "Experiential Skills", title, Color.White * fade, 0.95f);

        string meter = experiential.AtMaxThreshold
            ? $"Threshold {experiential.Threshold} - {experiential.Exp} Exp banked"
            : $"Threshold {experiential.Threshold} - {experiential.Exp} / {experiential.Capacity} Exp";

        Vector2 size = FontAssets.MouseText.Value.MeasureString(meter) * 0.85f;
        var right = new Vector2(bounds.Right - BODY_INSET - 6f - size.X, bounds.Y + 14f);

        Utils.DrawBorderString(spriteBatch, meter, right, new Color(68, 177, 242) * fade, 0.85f);
    }
}

/// <summary>
/// The pannable, zoomable node field. Nodes sit at the tree-space coordinates
/// <see cref="SkillTreeLayout"/> resolves, and the lines between them run inward by Threshold.
/// </summary>
internal sealed class SkillTreeField : UIElement
{
    #region layout

    /// <summary>Field pixels per tree-space unit at zoom 1.</summary>
    private const float UNIT = 62f;

    /// <summary>Drawn diameter of a node socket. Matches the socket texture, which is drawn point-sampled at 1:1.</summary>
    private const float NODE_SIZE = 36f;

    /// <summary>Click radius around a node's centre at zoom 1, a little wider than the socket itself.</summary>
    private const float HIT_RADIUS = 22f;

    /// <summary>Pixels of empty field kept past the outermost node.</summary>
    private const float EDGE_MARGIN = 72f;

    /// <summary>Blob radius a held node contributes to the backdrop, at zoom 1.</summary>
    private const float BLOB_RADIUS = NODE_SIZE * 2.6f;

    #endregion

    #region motion

    /// <summary>Per-frame decay of the flick left over from a drag.</summary>
    private const float GLIDE_DECAY = 0.9f;

    /// <summary>Glide speed under which the field is treated as stopped.</summary>
    private const float GLIDE_CUTOFF = 0.15f;

    /// <summary>Pixels of travel past which a press counts as a drag and not a click.</summary>
    private const float DRAG_SLOP = 5f;

    /// <summary>Zoom steps a wheel notch moves between. 0.5, 1 and 2 land the socket on whole pixels.</summary>
    private static readonly float[] ZoomLevels = [0.5f, 0.75f, 1f, 1.5f, 2f];

    private const int DEFAULT_ZOOM_LEVEL = 2;

    /// <summary>Share of the remaining zoom distance covered per frame.</summary>
    private const float ZOOM_EASE = 0.22f;

    #endregion

    private enum NodeState
    {
        /// <summary>Bought.</summary>
        Held,

        /// <summary>Open and affordable.</summary>
        Ready,

        /// <summary>Open, but the meter does not hold its cost.</summary>
        Costly,

        /// <summary>Its Threshold has not been reached, or its own condition is unmet.</summary>
        Shut,
    }

    private readonly SkillTreeLayout _layout = new();
    private readonly SkillGlyphFlash _flash = new();

    private readonly SkillBlob[] _blobs = new SkillBlob[SkillTreeBackdrop.BLOB_SLOTS];
    private readonly float[] _blobDistances = new float[SkillTreeBackdrop.BLOB_SLOTS];
    private int _blobCount;

    private Vector2 _pan;
    private Vector2 _glide;

    private float _zoom = 1f;
    private int _zoomLevel = DEFAULT_ZOOM_LEVEL;
    private Vector2 _zoomAnchor;

    private bool _dragging;
    private bool _dragged;
    private Vector2 _pressOrigin;
    private Vector2 _panOrigin;
    private Vector2 _lastPan;

    private Skill? _hovered;

    /// <summary>Rebuilds the layout each time the tree opens, so a reload or a new skill is picked up.</summary>
    public void OnOpened()
    {
        _layout.Build();

        _pan = Vector2.Zero;
        _glide = Vector2.Zero;
        _dragging = false;
        _hovered = null;

        _zoomLevel = DEFAULT_ZOOM_LEVEL;
        _zoom = ZoomLevels[DEFAULT_ZOOM_LEVEL];

        _flash.Reset();

        ClampPan();
    }

    #region interaction

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (!SkillTreeSystem.Interactive)
        {
            _dragging = false;
            _hovered = null;
        }
        else if (_dragging)
        {
            if (Main.mouseLeft)
            {
                _lastPan = _pan;
                _pan = _panOrigin + (Main.MouseScreen - _pressOrigin);

                if (Vector2.DistanceSquared(Main.MouseScreen, _pressOrigin) > DRAG_SLOP * DRAG_SLOP)
                    _dragged = true;
            }
            else
            {
                _dragging = false;
                _glide = _pan - _lastPan;
            }
        }
        else if (_glide.LengthSquared() > GLIDE_CUTOFF * GLIDE_CUTOFF)
        {
            _pan += _glide;
            _glide *= GLIDE_DECAY;
        }

        EaseZoom();
        ClampPan();

        if (SkillTreeSystem.Interactive)
            _hovered = ContainsPoint(Main.MouseScreen) && !_dragging ? NodeAt(Main.MouseScreen) : null;

        ObserveNodes();

        _blobCount = CollectBlobs();

        SkillTreeRenderer.Submit(GetDimensions().ToRectangle(), _pan, _blobs.AsSpan(0, _blobCount));
    }

    /// <summary>Walks the zoom toward its step, holding whatever was under the cursor when the wheel turned.</summary>
    private void EaseZoom()
    {
        float target = ZoomLevels[_zoomLevel];

        if (MathF.Abs(_zoom - target) < 0.0005f)
        {
            _zoom = target;

            return;
        }

        float previous = _zoom;
        _zoom = MathHelper.Lerp(_zoom, target, ZOOM_EASE);

        Vector2 offset = _zoomAnchor - Centre();

        _pan = offset - (offset - _pan) * (_zoom / previous);
    }

    /// <summary>Ticks every node's hover and burst animations. Runs from UpdateUI, so it keeps going under autopause.</summary>
    private void ObserveNodes()
    {
        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

        foreach (PlacedSkill node in _layout.Nodes)
            _flash.Observe(node.Skill.ID, skills.Has(node.Skill), ReferenceEquals(_hovered, node.Skill));
    }

    /// <summary>Keeps the content reachable: the layout is centred on the origin, so the slack is symmetric.</summary>
    private void ClampPan()
    {
        CalculatedStyle view = GetDimensions();

        Vector2 content = _layout.Extent * (UNIT * _zoom) * 2f + new Vector2(NODE_SIZE + EDGE_MARGIN * 2f);

        float slackX = MathF.Max(content.X - view.Width, 0f) * 0.5f;
        float slackY = MathF.Max(content.Y - view.Height, 0f) * 0.5f;

        _pan.X = Math.Clamp(_pan.X, -slackX, slackX);
        _pan.Y = Math.Clamp(_pan.Y, -slackY, slackY);
    }

    public override void ScrollWheel(UIScrollWheelEvent evt)
    {
        base.ScrollWheel(evt);

        if (!SkillTreeSystem.Interactive || evt.ScrollWheelValue == 0)
            return;

        _zoomLevel = Math.Clamp(_zoomLevel + MathF.Sign(evt.ScrollWheelValue), 0, ZoomLevels.Length - 1);
        _zoomAnchor = Main.MouseScreen;
        _glide = Vector2.Zero;
    }

    public override void LeftMouseDown(UIMouseEvent evt)
    {
        base.LeftMouseDown(evt);

        if (!SkillTreeSystem.Interactive)
            return;

        _dragging = true;
        _dragged = false;
        _pressOrigin = evt.MousePosition;
        _panOrigin = _pan;
        _lastPan = _pan;
        _glide = Vector2.Zero;
    }

    public override void LeftClick(UIMouseEvent evt)
    {
        base.LeftClick(evt);

        // A click that panned the field was a drag; buying on it would be an accident.
        if (!SkillTreeSystem.Interactive || _dragged || NodeAt(evt.MousePosition) is not { } skill)
            return;

        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

        if (skills.Has(skill))
            return;

        SkillPurchaseResult result = skills.TryUnlock(skill);

        if (result != SkillPurchaseResult.Success)
            _flash.Deny(skill.ID);

        SoundEngine.PlaySound(
            result == SkillPurchaseResult.Success ? SoundID.Unlock : SoundID.MenuTick);
    }

    public override void RightClick(UIMouseEvent evt)
    {
        base.RightClick(evt);

        if (!SkillTreeSystem.Interactive || NodeAt(evt.MousePosition) is not { } skill)
            return;

        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

        if (!skills.Has(skill))
            return;

        skills.Refund(skill);

        SoundEngine.PlaySound(SoundID.Grab);
    }

    private Skill? NodeAt(Vector2 point)
    {
        float radius = HIT_RADIUS * _zoom;

        foreach (PlacedSkill node in _layout.Nodes)
        {
            if (Vector2.DistanceSquared(ScreenOf(node.Space), point) <= radius * radius)
                return node.Skill;
        }

        return null;
    }

    /// <summary>Screen position of tree-space (0, 0): the middle of the view, panned.</summary>
    private Vector2 Centre()
    {
        CalculatedStyle view = GetDimensions();

        return new Vector2(view.X + view.Width * 0.5f, view.Y + view.Height * 0.5f);
    }

    private Vector2 ScreenOf(Vector2 space) => Centre() + _pan + space * (UNIT * _zoom);

    #endregion

    #region drawing

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        float fade = SkillTreeSystem.Fade;

        if (fade <= 0f)
            return;

        Rectangle view = GetDimensions().ToRectangle();

        // Nodes are drawn here rather than as children, so the field clips them itself.
        SkillTreeBatch.Scope scope = SkillTreeBatch.Point(spriteBatch, GetClippingRectangle(spriteBatch));

        DrawBackdrop(spriteBatch, view, fade);

        if (_layout.Nodes.Count > 0)
        {
            var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

            DrawLinks(spriteBatch, skills, fade);

            foreach (PlacedSkill node in _layout.Nodes)
                DrawNode(spriteBatch, node, StateOf(node.Skill, skills), fade);
        }

        SkillTreeBatch.Restore(spriteBatch, scope);

        if (_layout.Nodes.Count == 0)
            DrawEmptyNotice(spriteBatch, fade);
    }

    /// <summary>Blits the pixelated backdrop through the bloom pass. Nothing is drawn until it has been built.</summary>
    private static void DrawBackdrop(SpriteBatch spriteBatch, Rectangle view, float fade)
    {
        if (SkillTreeRenderer.Result is not { IsDisposed: false } target || SkillTreeRenderer.Bloom() is not { } bloom)
            return;

        SpriteBatchState restore = SpriteBatchState.Capture(spriteBatch);

        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Immediate, restore.BlendState, restore.SamplerState,
            restore.DepthStencilState, restore.RasterizerState, null, restore.Transform);

        bloom.Apply();

        spriteBatch.Draw(target, view, Color.White * fade);

        spriteBatch.End();
        restore.Begin(spriteBatch);
    }

    private void DrawEmptyNotice(SpriteBatch spriteBatch, float fade)
    {
        CalculatedStyle view = GetDimensions();

        const string notice = "No skills are registered.";

        Vector2 size = FontAssets.MouseText.Value.MeasureString(notice) * 0.9f;
        var position = new Vector2(view.X + (view.Width - size.X) * 0.5f, view.Y + view.Height * 0.4f);

        Utils.DrawBorderString(spriteBatch, notice, position, Color.Gray * fade, 0.9f);
    }

    /// <summary>Each node's line to the nearest skill below it in Threshold, brightening as that path is bought.</summary>
    private void DrawLinks(SpriteBatch spriteBatch, SkillPlayer skills, float fade)
    {
        foreach (SkillLink link in _layout.Links)
        {
            PlacedSkill from = _layout.Nodes[link.From];
            PlacedSkill to = _layout.Nodes[link.To];

            bool reached = skills.Has(to.Skill);

            Color tone = reached && skills.Has(from.Skill)
                ? new Color(120, 190, 255) * 0.8f
                : reached
                    ? new Color(68, 120, 190) * 0.6f
                    : new Color(46, 58, 96) * 0.55f;

            spriteBatch.DrawLine(ScreenOf(from.Space), ScreenOf(to.Space), tone * fade,
                Math.Max(1, (int)MathF.Round(2f * _zoom)));
        }
    }

    /// <summary>
    /// Fills <see cref="_blobs"/> with the held nodes nearest the middle of the view and returns how many
    /// landed. The backdrop carries a fixed number of slots, so the rest of the tree cannot contribute.
    /// </summary>
    private int CollectBlobs()
    {
        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();
        Vector2 focus = Centre();
        int count = 0;

        foreach (PlacedSkill node in _layout.Nodes)
        {
            if (!skills.Has(node.Skill))
                continue;

            Vector2 at = ScreenOf(node.Space);
            float distance = Vector2.DistanceSquared(at, focus);
            int slot = count;

            // Insertion into a four-slot array; far too short for a sort to pay off.
            while (slot > 0 && distance < _blobDistances[slot - 1])
            {
                if (slot < SkillTreeBackdrop.BLOB_SLOTS)
                {
                    _blobs[slot] = _blobs[slot - 1];
                    _blobDistances[slot] = _blobDistances[slot - 1];
                }

                slot--;
            }

            if (slot >= SkillTreeBackdrop.BLOB_SLOTS)
                continue;

            float strength = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + node.Space.Y);

            _blobs[slot] = new SkillBlob(at, BLOB_RADIUS * _zoom, strength, ColorOf(node.Skill.Group));
            _blobDistances[slot] = distance;

            count = Math.Min(count + 1, SkillTreeBackdrop.BLOB_SLOTS);
        }

        return count;
    }

    private void DrawNode(SpriteBatch spriteBatch, in PlacedSkill node, NodeState state, float fade)
    {
        Texture2D socket = Assets.Textures.UI.Elements.Empty.Asset.Value;
        Texture2D glow = SkillGlyphFlash.Glow(socket);
        Texture2D mask = SkillGlyphFlash.Mask(socket);

        string id = node.Skill.ID;
        Color tone = ColorOf(node.Skill.Group);
        float scale = _flash.ScaleOf(id) * _zoom;
        Vector2 at = ScreenOf(node.Space) + _flash.OffsetOf(id);

        // Held nodes breathe and ready ones pulse, so what is bought and what is affordable read apart
        // at a glance; the rest sit still.
        float ambient = state switch
        {
            NodeState.Held => 0.55f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + node.Space.Y),
            NodeState.Ready => 0.30f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + node.Space.X),
            NodeState.Costly => 0.12f,
            _ => 0f,
        };

        if (ambient > 0f)
        {
            spriteBatch.Draw(glow, at, null, Additive(tone, ambient * fade), 0f, glow.Size() * 0.5f, scale,
                SpriteEffects.None, 0f);
        }

        _flash.DrawUnder(spriteBatch, socket, id, at, scale, tone, fade);

        Color rim = state switch
        {
            NodeState.Held => Color.White,
            NodeState.Ready => new Color(235, 235, 240),
            NodeState.Costly => new Color(150, 150, 162),
            _ => new Color(92, 92, 104),
        };

        spriteBatch.Draw(socket, at, null, rim * fade, 0f, socket.Size() * 0.5f, scale, SpriteEffects.None, 0f);

        float hue = state switch
        {
            NodeState.Held => 0.55f,
            NodeState.Ready => 0.34f,
            NodeState.Costly => 0.16f,
            _ => 0f,
        };

        if (hue > 0f)
        {
            spriteBatch.Draw(mask, at, null, Additive(tone, hue * fade), 0f, mask.Size() * 0.5f, scale,
                SpriteEffects.None, 0f);
        }

        _flash.DrawOver(spriteBatch, socket, id, at, scale, tone, fade, state == NodeState.Held);
    }

    private static NodeState StateOf(Skill skill, SkillPlayer skills) => skills.CanUnlock(skill) switch
    {
        SkillPurchaseResult.AlreadyOwned => NodeState.Held,
        SkillPurchaseResult.Success => NodeState.Ready,
        SkillPurchaseResult.CannotAfford => NodeState.Costly,
        _ => NodeState.Shut,
    };

    private static Color ColorOf(SkillGroup group) => group switch
    {
        SkillGroup.Locomotion => new Color(120, 200, 255),
        SkillGroup.Construction => new Color(226, 180, 110),
        SkillGroup.Excavation => new Color(240, 200, 90),
        SkillGroup.Ballistics => new Color(232, 110, 100),
        SkillGroup.Alchemy => new Color(130, 226, 150),
        SkillGroup.Husbandry => new Color(236, 150, 200),
        SkillGroup.Communion => new Color(186, 140, 246),
        _ => new Color(200, 200, 210),
    };

    /// <summary>Zero-alpha colours add in the premultiplied AlphaBlend UI batch.</summary>
    private static Color Additive(Color color, float opacity) => (color * opacity) with { A = 0 };

    /// <summary>Called by the state after the field has drawn, so the card is not clipped by it.</summary>
    public void DrawTooltip(SpriteBatch spriteBatch)
    {
        float fade = SkillTreeSystem.Fade;

        if (_hovered is not { } skill || fade <= 0f)
            return;

        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

        string name = skill.Name;
        string description = skill.Description;
        string footer = FooterFor(skill, skills);

        DynamicSpriteFont font = FontAssets.MouseText.Value;

        float width = MathF.Max(
            MathF.Max(font.MeasureString(name).X * 0.95f, font.MeasureString(description).X * 0.85f),
            font.MeasureString(footer).X * 0.85f) + 24f;

        const float height = 82f;

        Vector2 position = Main.MouseScreen + new Vector2(20f, 20f);

        CalculatedStyle screen = UserInterface.ActiveInstance.GetDimensions();

        position.X = MathF.Min(position.X, screen.Width - width - 6f);
        position.Y = MathF.Min(position.Y, screen.Height - height - 6f);

        // Frame point-sampled, text left on the interface's own sampler.
        SkillTreeBatch.Scope scope = SkillTreeBatch.Point(spriteBatch);

        DrawUtils.DrawPanel(spriteBatch, new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height),
            new Color(10, 16, 34) * 0.95f, opacity: fade);

        SkillTreeBatch.Restore(spriteBatch, scope);

        Utils.DrawBorderString(spriteBatch, name, position + new Vector2(12f, 10f), ColorOf(skill.Group) * fade, 0.95f);
        Utils.DrawBorderString(spriteBatch, description, position + new Vector2(12f, 34f), Color.LightGray * fade, 0.85f);
        Utils.DrawBorderString(spriteBatch, footer, position + new Vector2(12f, 56f), FooterColor(skill, skills) * fade, 0.85f);
    }

    private static string FooterFor(Skill skill, SkillPlayer skills) => skills.CanUnlock(skill) switch
    {
        SkillPurchaseResult.AlreadyOwned => $"Held - right click to refund {skill.RefundValue} Exp",
        SkillPurchaseResult.Success => $"{skill.Cost} Exp - left click to unlock",
        SkillPurchaseResult.CannotAfford => $"{skill.Cost} Exp - not enough banked",
        SkillPurchaseResult.ThresholdTooLow => $"Opens at Threshold {skill.UnlockThreshold} - {skill.Cost} Exp",
        _ => $"Unavailable - {skill.Cost} Exp",
    };

    private static Color FooterColor(Skill skill, SkillPlayer skills) => skills.CanUnlock(skill) switch
    {
        SkillPurchaseResult.AlreadyOwned => new Color(68, 177, 242),
        SkillPurchaseResult.Success => Color.LightGreen,
        _ => Color.Gray,
    };

    #endregion
}
