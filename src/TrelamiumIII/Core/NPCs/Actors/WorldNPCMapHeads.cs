using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace  TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Manages player-rendered map heads for all active WorldNPCs.
///
/// Vanilla's <see cref="Terraria.Graphics.Renderers.MapHeadRenderer"/> indexes
/// its render target pool by <c>Player.whoAmI</c> (0–254). WorldNPC DummyPlayers
/// sit at index 255 (<c>Main.maxPlayers</c>), which is out of bounds. The player
/// drawing loop in <c>Main.DrawMap</c> also only iterates real players, so WorldNPCs
/// are never reached.
///
/// This system sidesteps both problems: it owns a separate pool of
/// <see cref="PlayerHeadDrawRenderTargetContent"/> keyed by NPC index, registers
/// itself in the render target pipeline, and draws into the fullscreen map via
/// <see cref="PostDrawFullscreenMap"/>.
/// </summary>
public class WorldNPCMapSystem : ModSystem, INeedRenderTargetContent
{
    public static WorldNPCMapSystem? Instance { get; private set; }

    // Render Target Pool

    /// <summary>Render target content pool, keyed by NPC.whoAmI (npc array index).</summary>
    private readonly Dictionary<int, PlayerHeadDrawRenderTargetContent> _headPool = new();

    /// <summary>Tracks which NPC indices are active this frame for stale-entry pruning.</summary>
    private readonly HashSet<int> _activeThisFrame = new();

    private bool _dirty;

    public bool IsReady => !_dirty;

    // Cached RasterizerState for scissor clipping

    private static readonly RasterizerState ScissorRasterizer = new()
    {
        ScissorTestEnable = true,
        CullMode = CullMode.None
    };


    public override void Load()
    {
        Instance = this;
        Main.ContentThatNeedsRenderTargets.Add(this);
        On_Main.DrawMap += Hook_DrawMap;
    }

    public override void Unload()
    {
        On_Main.DrawMap -= Hook_DrawMap;
        Main.ContentThatNeedsRenderTargets.Remove(this);
        _headPool.Clear();
        Instance = null;
    }

    /// <summary>
    /// Called by the engine during render target preparation, before any drawing.
    /// Each queued head gets rendered into its own target here.
    /// </summary>
    public void PrepareRenderTarget(GraphicsDevice device, SpriteBatch spriteBatch)
    {
        if (!_dirty)
            return;

        foreach (var content in _headPool.Values)
            content.PrepareRenderTarget(device, spriteBatch);

        _dirty = false;
    }

    /// <summary>Prune render target entries for NPCs that are no longer active.</summary>
    public override void PostUpdateNPCs()
    {
        _activeThisFrame.Clear();

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            if (Main.npc[i].active && Main.npc[i].ModNPC is WorldNPC)
                _activeThisFrame.Add(i);
        }

        List<int> stale = null;

        foreach (int key in _headPool.Keys)
        {
            if (!_activeThisFrame.Contains(key))
            {
                stale ??= [];
                stale.Add(key);
            }
        }

        if (stale != null)
        {
            foreach (int key in stale)
                _headPool.Remove(key);
        }
    }

    public void Reset()
    {
        _headPool.Clear();
        _dirty = false;
    }

    // Render Target Access and Drawing

    private PlayerHeadDrawRenderTargetContent GetOrCreate(int npcIndex)
    {
        if (!_headPool.TryGetValue(npcIndex, out var content))
        {
            content = new PlayerHeadDrawRenderTargetContent();
            _headPool[npcIndex] = content;
        }

        return content;
    }

    /// <summary>
    /// Draws the map head icon at <paramref name="screenPos"/>.
    /// If the NPC exposes a <see cref="WorldNPC.MapHeadTexture"/>, that sprite
    /// is drawn directly. Otherwise the DummyPlayer head render target pipeline
    /// runs as normal. Returns true if something was drawn.
    /// </summary>
    private bool DrawHead(WorldNPC worldNpc, int npcIndex, Vector2 screenPos, float alpha, float scale)
    {
        string headTexturePath = worldNpc.MapHeadTexture;

        if (headTexturePath != null)
        {
            Texture2D texture = ModContent.Request<Texture2D>(headTexturePath).Value;
            Vector2 origin = texture.Size() * 0.5f;

            Main.spriteBatch.Draw(
                texture,
                screenPos,
                null,
                Color.White * alpha,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f
            );

            return true;
        }

        // Player head render target path.
        var content = GetOrCreate(npcIndex);
        content.UsePlayer(worldNpc.DummyPlayer);
        content.UseColor(Color.White);
        content.Request();
        _dirty = true;

        if (!content.IsReady)
            return false;

        RenderTarget2D target = content.GetTarget();
        Vector2 origin2 = target.Size() * 0.5f;

        Main.spriteBatch.Draw(
            target,
            screenPos,
            null,
            Color.White * alpha,
            0f,
            origin2,
            scale,
            SpriteEffects.None,
            0f
        );

        return true;
    }

    /// <summary>
    /// tML calls this while the fullscreen map's SpriteBatch is active.
    ///
    /// Coordinate derivation from Main.DrawMap (fullscreen path):
    ///   screenX = (npcTileX * mapScale) + offsetX - (10 * mapScale)
    ///   screenY = (npcTileY * mapScale) + offsetY - (10 * mapScale)
    ///
    /// Where offsetX/Y = -(center * scale) + screenHalf + (10 * scale).
    /// The 10-tile padding cancels, leaving:
    ///   screenX = (npcTileX - centerTileX) * scale + screenWidth / 2
    /// </summary>
    public override void PostDrawFullscreenMap(ref string mouseText)
    {
        float mapScale = Main.mapFullscreenScale;
        float mapX = Main.mapFullscreenPos.X;
        float mapY = Main.mapFullscreenPos.Y;
        float halfW = Main.screenWidth * 0.5f;
        float halfH = Main.screenHeight * 0.5f;
        float drawScale = Main.UIScale;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (!npc.active || npc.ModNPC is not WorldNPC worldNpc)
                continue;

            Vector2 center = npc.Center + new Vector2(0f, npc.gfxOffY);
            float tileX = center.X / 16f;
            float tileY = center.Y / 16f;

            float sx = (tileX - mapX) * mapScale + halfW;
            float sy = (tileY - mapY) * mapScale + halfH;

            if (!DrawHead(worldNpc, i, new Vector2(sx, sy), 1f, drawScale))
                continue;

            // Hover detection to match vanilla's bounds for head icons.
            if (_headPool.TryGetValue(i, out var headContent) && headContent.IsReady)
            {
                RenderTarget2D target = headContent.GetTarget();
                float halfSize = target.Width * 0.5f * drawScale;

                if (Main.mouseX >= sx - halfSize && Main.mouseX <= sx + halfSize &&
                    Main.mouseY >= sy - halfSize && Main.mouseY <= sy + halfSize)
                {
                    mouseText = npc.GivenOrTypeName;
                }
            }
        }
    }

    // MINIMAP + OVERLAY, DrawMap detour

    /// <summary>
    /// Wraps Main.DrawMap. After orig completes, draws WorldNPC heads for
    /// minimap and overlay modes. Fullscreen is handled by PostDrawFullscreenMap.
    /// </summary>
    private static void Hook_DrawMap(On_Main.orig_DrawMap orig, Main self, GameTime gameTime)
    {
        orig(self, gameTime);

        if (!Main.mapEnabled || !Main.mapReady || Main.mapFullscreen)
            return;

        Instance?.DrawNonFullscreenHeads();
    }

    private void DrawNonFullscreenHeads()
    {
        if (Main.mapStyle == 1)
            DrawMinimapHeads();
        else if (Main.mapStyle == 2)
            DrawOverlayHeads();
    }

    /// <summary>
    /// Draws WorldNPC heads on the minimap (mapStyle 1).
    ///
    /// After DrawMap returns, the spriteBatch is in Begin() with no params.
    /// We end it, re-enter with the minimap's transform matrix and a scissor
    /// rect clipping to the content area, draw heads, then restore.
    ///
    /// Coordinate derivation from Main.DrawMap (minimap path):
    ///
    ///   mapScale = Main.MapScale (UI zoom factor)
    ///   mmX = Main.miniMapX (already divided by mapScale inside DrawMap)
    ///   mmY = Main.miniMapY (already divided by mapScale inside DrawMap)
    ///   scale = Main.mapMinimapScale
    ///
    ///   centerTileX = floor(screenCenter / 16)
    ///   centerTileY = floor(screenCenter / 16)
    ///   subTileX = -(rawCenter - floor(rawCenter)) * scale   // smooth scrolling
    ///   subTileY = -(rawCenter - floor(rawCenter)) * scale
    ///
    ///   viewW = miniMapWidth / scale
    ///   viewH = miniMapHeight / scale
    ///   tileOriginX = centerTileX - viewW / 2
    ///   tileOriginY = centerTileY - viewH / 2
    ///
    ///   headX = (npcTileX - tileOriginX) * scale + mmX + subTileX
    ///   headY = (npcTileY - tileOriginY) * scale + mmY + subTileY
    ///
    /// The spriteBatch transform is UIScaleMatrix * CreateScale(MapScale),
    /// so coordinates in pre-transform space get scaled to screen pixels.
    /// Since mmX/mmY are already in that space, everything lines up.
    /// </summary>
    private void DrawMinimapHeads()
    {
        var sb = Main.spriteBatch;
        var device = sb.GraphicsDevice;

        float scale = Main.mapMinimapScale;
        float mapScale = Main.MapScale;
        float alpha = Main.mapMinimapAlpha;

        // Head icon scale (matches vanilla's formula for minimap NPC heads).
        float headScale = (scale * 0.25f * 2f + 1f) / 3f;
        if (headScale > 1f)
            headScale = 1f;

        // miniMapX/Y are divided by MapScale during DrawMap.
        // They're in the pre-transform coordinate space.
        int mmX = Main.miniMapX;
        int mmY = Main.miniMapY;
        int mmW = Main.miniMapWidth;
        int mmH = Main.miniMapHeight;

        // View center in tiles (integer for tile alignment, raw for sub-tile offset).
        float rawCenterX = (Main.screenPosition.X + PlayerInput.RealScreenWidth / 2f) / 16f;
        float rawCenterY = (Main.screenPosition.Y + PlayerInput.RealScreenHeight / 2f) / 16f;
        float centerTileX = (float)(int)rawCenterX;
        float centerTileY = (float)(int)rawCenterY;

        // Sub-tile offset for smooth scrolling.
        float subTileX = -(rawCenterX - centerTileX) * scale;
        float subTileY = -(rawCenterY - centerTileY) * scale;

        // Tile bounds of the visible minimap region.
        float viewW = mmW / scale;
        float viewH = mmH / scale;
        float tileOriginX = centerTileX - viewW / 2f;
        float tileOriginY = centerTileY - viewH / 2f;

        // Adjust minimap position for out-of-bounds camera (matches vanilla).
        float adjustX = 0f;
        float adjustY = 0f;
        if (tileOriginX < 10f)
            adjustX = -(tileOriginX - 10f) * scale;
        if (tileOriginY < 10f)
            adjustY = -(tileOriginY - 10f) * scale;

        // Set up spriteBatch with minimap transform and scissor clipping.
        var savedScissor = device.ScissorRectangle;
        sb.End();

        // Scissor rect in screen pixels. The transform scales by MapScale * UIScale,
        // so we apply the same to the content bounds.
        float pixelScale = mapScale * Main.UIScale;
        device.ScissorRectangle = new Rectangle(
            (int)(mmX * pixelScale),
            (int)(mmY * pixelScale),
            (int)(mmW * pixelScale),
            (int)(mmH * pixelScale)
        );

        Matrix transform = Main.UIScaleMatrix * Matrix.CreateScale(mapScale);

        sb.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            ScissorRasterizer,
            null,
            transform
        );

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || npc.ModNPC is not WorldNPC worldNpc)
                continue;

            Vector2 center = npc.Center + new Vector2(0f, npc.gfxOffY);
            float npcTileX = center.X / 16f;
            float npcTileY = center.Y / 16f;

            float sx = (npcTileX - tileOriginX) * scale + mmX + subTileX + adjustX;
            float sy = (npcTileY - tileOriginY) * scale + mmY + subTileY + adjustY;

            // Bounds check matching vanilla's minimap margins.
            if (sx <= mmX + 12 || sx >= mmX + mmW - 16 ||
                sy <= mmY + 10 || sy >= mmY + mmH - 14)
                continue;

            DrawHead(worldNpc, i, new Vector2(sx, sy), alpha, headScale);
        }

        sb.End();

        // Restore previous state.
        device.ScissorRectangle = savedScissor;
        sb.Begin();
    }

    /// <summary>
    /// Draws WorldNPC heads on the overlay map (mapStyle 2).
    ///
    /// Coordinate derivation from Main.DrawMap (overlay path):
    ///
    ///   scale = Main.mapOverlayScale
    ///   centerTileX = (screenPosition.X + screenWidth / 2) / 16
    ///   centerTileY = (screenPosition.Y + screenHeight / 2) / 16
    ///
    ///   offsetX = -centerTileX * scale + screenWidth / 2 + 10 * scale
    ///   offsetY = -centerTileY * scale + screenHeight / 2 + 10 * scale
    ///
    ///   headX = npcTileX * scale + offsetX - 10 * scale
    ///   headY = npcTileY * scale + offsetY - 10 * scale
    ///
    /// The 10-tile padding adds then subtracts, canceling out:
    ///   headX = (npcTileX - centerTileX) * scale + screenWidth / 2
    ///   headY = (npcTileY - centerTileY) * scale + screenHeight / 2
    ///
    /// Overlay uses Matrix.Identity for its transform. coordinates are
    /// raw screen pixels.
    /// </summary>
    private void DrawOverlayHeads()
    {
        var sb = Main.spriteBatch;

        float scale = Main.mapOverlayScale;
        float alpha = Main.mapOverlayAlpha;

        // Head icon scale (matches vanilla's formula for overlay NPC heads).
        float headScale = (scale * 0.2f * 2f + 1f) / 3f;
        if (headScale > 1f)
            headScale = 1f;
        headScale *= Main.UIScale;

        float centerTileX = (Main.screenPosition.X + Main.screenWidth / 2f) / 16f;
        float centerTileY = (Main.screenPosition.Y + Main.screenHeight / 2f) / 16f;

        sb.End();

        sb.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Matrix.Identity
        );

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || npc.ModNPC is not WorldNPC worldNpc)
                continue;

            Vector2 center = npc.Center + new Vector2(0f, npc.gfxOffY);
            float npcTileX = center.X / 16f;
            float npcTileY = center.Y / 16f;

            float sx = (npcTileX - centerTileX) * scale + Main.screenWidth / 2f;
            float sy = (npcTileY - centerTileY) * scale + Main.screenHeight / 2f;

            DrawHead(worldNpc, i, new Vector2(sx, sy), alpha, headScale);
        }

        sb.End();
        sb.Begin();
    }
}