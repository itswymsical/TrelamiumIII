using System.Collections.Generic;
using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.ExperientialSystem;
using TrelamiumIII.Core.ExperientialSystem.Skills;
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

        SkillTreeBatch.Scope scope = SkillTreeBatch.Point(spriteBatch);

        DrawUtils.DrawPanel(spriteBatch, bounds, new Color(18, 16, 34) * 0.92f, opacity: fade);

        DrawHeader(spriteBatch, bounds, fade);

        SkillTreeBatch.Restore(spriteBatch, scope);
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

        Utils.DrawBorderString(spriteBatch, meter, right, Color.Goldenrod * fade, 0.85f);
    }
}

/// <summary>
/// The pannable node field. Rows are Threshold bands from <see cref="SkillRegistry.Bands"/>, so the tree
/// reads top to bottom as the order the player unlocks it in.
/// </summary>
/// <remarks>Positions are in field space, origin at the top centre of the content; <c>_pan</c> scrolls it.</remarks>
internal sealed class SkillTreeField : UIElement
{
    #region layout

    /// <summary>Vertical pixels between Threshold bands.</summary>
    private const float BAND_SPACING = 124f;

    /// <summary>Horizontal pixels between two nodes in the same band.</summary>
    private const float NODE_SPACING = 92f;

    /// <summary>Drawn diameter of a node socket. Matches the socket texture, which is drawn point-sampled at 1:1.</summary>
    private const float NODE_SIZE = 36f;

    /// <summary>Click radius around a node's centre, a little wider than the socket itself.</summary>
    private const float HIT_RADIUS = 22f;

    /// <summary>Pixels of empty field kept above the first band and below the last.</summary>
    private const float EDGE_MARGIN = 54f;

    /// <summary>Blob radius a held node contributes to the backdrop.</summary>
    private const float BLOB_RADIUS = NODE_SIZE * 2.6f;

    #endregion

    #region motion

    /// <summary>Per-frame decay of the flick left over from a drag.</summary>
    private const float GLIDE_DECAY = 0.9f;

    /// <summary>Glide speed under which the field is treated as stopped.</summary>
    private const float GLIDE_CUTOFF = 0.15f;

    /// <summary>Field pixels scrolled per wheel notch.</summary>
    private const float WHEEL_STEP = 0.35f;

    /// <summary>Pixels of travel past which a press counts as a drag and not a click.</summary>
    private const float DRAG_SLOP = 5f;

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

    private readonly record struct PlacedSkill(Skill Skill, Vector2 Position);

    private readonly List<PlacedSkill> _nodes = [];

    private readonly SkillGlyphFlash _flash = new();
    private readonly SkillTreeBackdrop _backdrop = new();

    private readonly SkillBlob[] _blobs = new SkillBlob[SkillTreeBackdrop.BLOB_SLOTS];
    private readonly float[] _blobDistances = new float[SkillTreeBackdrop.BLOB_SLOTS];

    private float _contentHeight;
    private float _contentWidth;

    private Vector2 _pan;
    private Vector2 _glide;

    private bool _dragging;
    private bool _dragged;
    private Vector2 _pressOrigin;
    private Vector2 _panOrigin;
    private Vector2 _lastPan;

    private Skill? _hovered;

    /// <summary>Rebuilds the layout each time the tree opens, so a reload or a new skill is picked up.</summary>
    public void OnOpened()
    {
        BuildLayout();

        _pan = Vector2.Zero;
        _glide = Vector2.Zero;
        _dragging = false;
        _hovered = null;

        _flash.Reset();
        _backdrop.Reset();

        // Opens on the lowest band the player has not cleared, rather than at the top of a long tree.
        int threshold = Main.LocalPlayer.GetModPlayer<ExperientialPlayer>().Threshold;
        ReadOnlySpan<int> bands = SkillRegistry.Bands;

        for (int i = 0; i < bands.Length; i++)
        {
            if (bands[i] <= threshold)
                _pan.Y = -i * BAND_SPACING;
        }

        ClampPan();
    }

    private void BuildLayout()
    {
        _nodes.Clear();

        ReadOnlySpan<int> bands = SkillRegistry.Bands;

        _contentHeight = bands.Length == 0 ? 0f : (bands.Length - 1) * BAND_SPACING + NODE_SIZE;
        _contentWidth = 0f;

        for (int i = 0; i < bands.Length; i++)
        {
            IReadOnlyList<Skill> skills = SkillRegistry.AtThreshold(bands[i]);

            float width = (skills.Count - 1) * NODE_SPACING;
            _contentWidth = MathF.Max(_contentWidth, width + NODE_SIZE);

            for (int j = 0; j < skills.Count; j++)
            {
                var position = new Vector2(j * NODE_SPACING - width * 0.5f, i * BAND_SPACING);

                _nodes.Add(new PlacedSkill(skills[j], position));
            }
        }
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

        ClampPan();

        if (SkillTreeSystem.Interactive)
            _hovered = ContainsPoint(Main.MouseScreen) && !_dragging ? NodeAt(Main.MouseScreen) : null;

        ObserveNodes();
    }

    /// <summary>Ticks every node's hover and burst animations. Runs from UpdateUI, so it keeps going under autopause.</summary>
    private void ObserveNodes()
    {
        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

        foreach (PlacedSkill node in _nodes)
            _flash.Observe(node.Skill.ID, skills.Has(node.Skill), ReferenceEquals(_hovered, node.Skill));
    }

    /// <summary>Keeps the content reachable: a field larger than the view is centred, otherwise it scrolls edge to edge.</summary>
    private void ClampPan()
    {
        CalculatedStyle view = GetDimensions();

        float slackY = MathF.Max(_contentHeight + EDGE_MARGIN * 2f - view.Height, 0f);
        float slackX = MathF.Max(_contentWidth + EDGE_MARGIN * 2f - view.Width, 0f);

        _pan.Y = Math.Clamp(_pan.Y, -slackY, 0f);
        _pan.X = Math.Clamp(_pan.X, -slackX * 0.5f, slackX * 0.5f);
    }

    public override void ScrollWheel(UIScrollWheelEvent evt)
    {
        base.ScrollWheel(evt);

        if (!SkillTreeSystem.Interactive)
            return;

        _pan.Y += evt.ScrollWheelValue * WHEEL_STEP;
        _glide = Vector2.Zero;

        ClampPan();
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
        Vector2 origin = FieldOrigin();

        foreach (PlacedSkill node in _nodes)
        {
            if (Vector2.DistanceSquared(origin + node.Position, point) <= HIT_RADIUS * HIT_RADIUS)
                return node.Skill;
        }

        return null;
    }

    /// <summary>Screen position of field-space (0, 0): the top centre of the content, panned.</summary>
    private Vector2 FieldOrigin()
    {
        CalculatedStyle view = GetDimensions();

        return new Vector2(view.X + view.Width * 0.5f, view.Y + EDGE_MARGIN) + _pan;
    }

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

        Vector2 origin = FieldOrigin();
        var skills = Main.LocalPlayer.GetModPlayer<SkillPlayer>();

        int blobs = CollectBlobs(origin, skills, view.Center.ToVector2());

        _backdrop.Draw(spriteBatch, view, _pan, fade, _blobs.AsSpan(0, blobs));

        if (_nodes.Count == 0)
        {
            DrawEmptyNotice(spriteBatch, fade);
        }
        else
        {
            int threshold = Main.LocalPlayer.GetModPlayer<ExperientialPlayer>().Threshold;

            DrawSpine(spriteBatch, origin, threshold, fade);

            foreach (PlacedSkill node in _nodes)
                DrawNode(spriteBatch, origin + node.Position, node, StateOf(node.Skill, skills), fade);
        }

        SkillTreeBatch.Restore(spriteBatch, scope);
    }

    /// <summary>
    /// Fills <see cref="_blobs"/> with the held nodes nearest <paramref name="focus"/> and returns how many
    /// landed. The backdrop carries a fixed number of slots, so the rest of the tree cannot contribute.
    /// </summary>
    private int CollectBlobs(Vector2 origin, SkillPlayer skills, Vector2 focus)
    {
        int count = 0;

        foreach (PlacedSkill node in _nodes)
        {
            if (!skills.Has(node.Skill))
                continue;

            Vector2 at = origin + node.Position;
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

            float strength = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.7f + node.Position.Y * 0.03f);

            _blobs[slot] = new SkillBlob(at, BLOB_RADIUS, strength, ColorOf(node.Skill.Group));
            _blobDistances[slot] = distance;

            count = Math.Min(count + 1, SkillTreeBackdrop.BLOB_SLOTS);
        }

        return count;
    }

    private void DrawEmptyNotice(SpriteBatch spriteBatch, float fade)
    {
        CalculatedStyle view = GetDimensions();

        const string notice = "No skills are registered.";

        Vector2 size = FontAssets.MouseText.Value.MeasureString(notice) * 0.9f;
        var position = new Vector2(view.X + (view.Width - size.X) * 0.5f, view.Y + view.Height * 0.4f);

        Utils.DrawBorderString(spriteBatch, notice, position, Color.Gray * fade, 0.9f);
    }

    /// <summary>The line the bands hang off, with each band's Threshold labelled beside its row.</summary>
    private void DrawSpine(SpriteBatch spriteBatch, Vector2 origin, int threshold, float fade)
    {
        ReadOnlySpan<int> bands = SkillRegistry.Bands;

        // A segment brightens once the band below it is open, so the spine reads as a climbed ladder.
        for (int i = 0; i + 1 < bands.Length; i++)
        {
            Color tone = threshold >= bands[i + 1] ? new Color(158, 140, 232) : new Color(90, 84, 140);

            spriteBatch.DrawLine(
                origin + new Vector2(0f, i * BAND_SPACING),
                origin + new Vector2(0f, (i + 1) * BAND_SPACING),
                tone * (0.55f * fade),
                2);
        }

        // Pinned to the left edge and scrolled only vertically, so the ladder stays readable as a ruler
        // however far the field is panned sideways.
        float labelX = GetDimensions().X + 8f;

        for (int i = 0; i < bands.Length; i++)
        {
            bool open = threshold >= bands[i];

            var position = new Vector2(labelX, origin.Y + i * BAND_SPACING - 10f);

            Utils.DrawBorderString(spriteBatch, $"Threshold {bands[i]}", position,
                (open ? Color.Goldenrod : Color.Gray * 0.8f) * fade, 0.8f);
        }
    }

    private void DrawNode(SpriteBatch spriteBatch, Vector2 centre, in PlacedSkill node, NodeState state, float fade)
    {
        Texture2D socket = Assets.Textures.UI.Elements.Empty.Asset.Value;
        Texture2D glow = SkillGlyphFlash.Glow(socket);
        Texture2D mask = SkillGlyphFlash.Mask(socket);

        string id = node.Skill.ID;
        Color tone = ColorOf(node.Skill.Group);
        float scale = _flash.ScaleOf(id);
        Vector2 at = centre + _flash.OffsetOf(id);

        // Held nodes breathe and ready ones pulse, so what is bought and what is affordable read apart
        // at a glance; the rest sit still.
        float ambient = state switch
        {
            NodeState.Held => 0.55f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + node.Position.Y),
            NodeState.Ready => 0.30f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + node.Position.X * 0.05f),
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

        DrawUtils.DrawPanel(spriteBatch, new Rectangle((int)position.X, (int)position.Y, (int)width, (int)height),
            new Color(16, 14, 30) * 0.95f, opacity: fade);

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
        SkillPurchaseResult.AlreadyOwned => Color.SkyBlue,
        SkillPurchaseResult.Success => Color.LightGreen,
        _ => Color.Gray,
    };

    #endregion
}
