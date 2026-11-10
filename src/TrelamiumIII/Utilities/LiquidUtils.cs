using TrelamiumIII.Core.Gas;
using System.Linq;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Utilities;

public static class LiquidUtils
{
    /// <summary>
    /// Attempts to place a specified type of liquid at the player's targeted tile location using the provided item.
    /// </summary>
    /// <remarks>Placement will fail if the player is not allowed to build, the target tile is out of range,
    /// already contains a different liquid, or is occupied by a solid tile that blocks liquid. The method should be
    /// called only when the player is able to use the item and the target tile is valid for liquid placement.</remarks>
    /// <param name="player">The player attempting to place the liquid. Must be the local player and able to build at the target location.</param>
    /// <param name="item">The item used to place the liquid. Its properties may affect placement range and timing.</param>
    /// <param name="liquidType">The type of liquid to place at the target tile. Must match the game's defined liquid types.</param>
    /// <returns>true if the liquid was successfully placed at the target tile; otherwise, false.</returns>
    public static bool PlaceLiquid(Player player, Item item, int liquidType) => PlaceLiquid(player, item, liquidType, condition: null);

    /// <inheritdoc cref="PlaceLiquid(Player, Item, int)" />
    /// <param name="condition">For a liquid with a <see cref="CustomLiquidSet" />, the condition to place; null for its default.</param>
    public static bool PlaceLiquid(Player player, Item item, int liquidType, string? condition)
    {
        // Make sure the player hasn't just dropped an item recently
        if (player.JustDroppedAnItem || player.whoAmI != Main.myPlayer)
            return false;

        if (player.noBuilding ||
            !(player.position.X / 16f - (float)Player.tileRangeX - (float)item.tileBoost <= (float)Player.tileTargetX) ||
            !((player.position.X + (float)player.width) / 16f + (float)Player.tileRangeX + (float)item.tileBoost - 1f >= (float)Player.tileTargetX) ||
            !(player.position.Y / 16f - (float)Player.tileRangeY - (float)item.tileBoost <= (float)Player.tileTargetY) ||
            !((player.position.Y + (float)player.height) / 16f + (float)Player.tileRangeY + (float)item.tileBoost - 2f >= (float)Player.tileTargetY))
        {
            return false;
        }

        if (!Main.GamepadDisableCursorItemIcon)
        {
            player.cursorItemIconEnabled = true;
            Main.ItemIconCacheUpdate(item.type);
        }

        if (!player.ItemTimeIsZero || player.itemAnimation <= 0 || !player.controlUseItem)
            return false;

        Tile tile = Main.tile[Player.tileTargetX, Player.tileTargetY];

        if (tile.LiquidAmount >= 200)
            return false;

        if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
        {
            if (tile.TileType != TileID.Grate)
                return false;
        }

        if (tile.LiquidAmount != 0 && tile.LiquidType != liquidType)
            return false;

        SoundEngine.PlaySound(SoundID.SplashWeak, player.position);

        // Sets the condition too, which a plain write would leave at whatever the tile last held.
        if (CustomLiquidSet.ByType(liquidType) is not null)
        {
            LiquidInternalSystem.Place(Player.tileTargetX, Player.tileTargetY, liquidType, condition, byte.MaxValue);
            player.ApplyItemTime(item);
            return true;
        }

        tile.LiquidType = liquidType;
        tile.LiquidAmount = byte.MaxValue;
        WorldGen.SquareTileFrame(Player.tileTargetX, Player.tileTargetY);

        player.ApplyItemTime(item);

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.sendWater(Player.tileTargetX, Player.tileTargetY);
        }

        return true;
    }

    /// <summary>
    /// Converts the liquid present in the specified gas node into a solid tile at the given tile coordinates.
    /// </summary>
    /// <param name="node">A reference to the gas node containing the liquid to be converted. The node will be updated to reflect the
    /// conversion.</param>
    /// <param name="tileX">The X-coordinate of the tile where the liquid will be converted.</param>
    /// <param name="tileY">The Y-coordinate of the tile where the liquid will be converted.</param>
    /// <param name="tile">The tile object representing the target tile to receive the converted liquid.</param>
    /// <param name="amountLeft">How much water to leave left.</param>
    /// <summary>
    /// Boils a tile's liquid away a little at a time, leaving <paramref name="tileType"/> behind
    /// once it is spent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The field-driven replacement for <see cref="ConvertLiquidIntoTile"/>. That one is called
    /// per node per frame and drains <c>radius * 10</c> at a go, so one node clipping a full tile
    /// empties it in two frames -- water flashes to obsidian the instant anything touches it, with
    /// no sense of the pool fighting back. This drains a fixed amount per call and only converts on
    /// the tick the liquid actually runs out, so a deep pool visibly boils down.
    /// </para>
    /// <para>
    /// World mutation, so it is gated to the server and to singleplayer. The old path ran on every
    /// client independently and each one converted its own copy of the tile, which is a desync the
    /// moment two clients' simulations diverge by a frame.
    /// </para>
    /// </remarks>
    /// <param name="drain">Liquid units removed by this call, out of 255.</param>
    /// <param name="amountLeft">Liquid left in the tile once it converts.</param>
    /// <returns>Whether this call was the one that converted the tile.</returns>
    public static bool BoilLiquidIntoTile(int tileX, int tileY, ushort tileType, int liquidType, int drain, byte amountLeft = 0)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !WorldGen.InWorld(tileX, tileY, 1))
            return false;

        Tile tile = Framing.GetTileSafely(tileX, tileY);

        if (tile.LiquidAmount == 0 || tile.LiquidType != liquidType || tile.LiquidAmount <= amountLeft)
            return false;

        int remaining = tile.LiquidAmount - Math.Max(drain, 1);

        // Still boiling. Nothing is placed until the pool is actually gone, so a tile part way
        // through the process is a shallower pool rather than a half-formed block.
        if (remaining > amountLeft)
        {
            tile.LiquidAmount = (byte)remaining;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.sendWater(tileX, tileY);

            return false;
        }

        tile.LiquidAmount = amountLeft;

        if (!tile.HasTile)
        {
            tile.HasTile = true;
            tile.TileType = tileType;
            WorldGen.SquareTileFrame(tileX, tileY);
        }

        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendTileSquare(-1, tileX, tileY);
            NetMessage.sendWater(tileX, tileY);
        }

        return true;
    }

    /// <summary>
    /// Per-node, single-shot liquid conversion. Superseded by <see cref="BoilLiquidIntoTile"/>;
    /// no longer called by anything.
    /// </summary>
    /// <remarks>
    /// Kept only because it is public. Prefer the field-driven version for anything new: this one
    /// drains <c>radius * 10</c> per call from a per-node hook that fires every frame, so a pool
    /// converts on contact rather than boiling down, and it mutates the world on every client
    /// independently with no sync.
    /// </remarks>
    public static void ConvertLiquidIntoTile(ref FieldNode node, int tileX, int tileY, Tile tile, ushort tileType, int liquidType, byte amountLeft = 0)
    {
        if (tile.LiquidType != liquidType)
            return;
        

        // Calculate the amount of liquid to convert based on the gas node's properties
        float liquidToConvert = Math.Min(tile.LiquidAmount, node.Radius * 10); 

        // Reduce the liquid amount in the gas node
        tile.LiquidAmount -= (byte)liquidToConvert;

        // If the liquid amount drops to zero or below kill the gas
        if (tile.LiquidAmount <= 0)
        {
            tile.active(false);
            node.Life = 0;
            return;
        }
        tile.HasTile = true;
        tile.TileType = tileType;
        WorldGen.SquareTileFrame(tileX, tileY);
        tile.LiquidAmount = amountLeft;
    }

    /// <summary>
    /// Legacy PlayLiquid() method, kept for compatibility with older code. Use the bool-returning PlaceLiquid() method instead for new code.
    /// </summary>
    /// <param name="player"></param>
    /// <param name="item"></param>
    /// <param name="liquidType"></param>
    /// <param name="isBottomless"></param>
    public static void PlaceLiquid(Player player, Item item, int liquidType, bool isBottomless = false)
    {
        if (!player.JustDroppedAnItem)//make sure the player hasn't just dropped an item recently
        {
            if (player.whoAmI != Main.myPlayer) //make sure to only run on clients (we sync multiplayer data later down the line)
            {
                return;
            }
            //We make sure the player is in range to be able to place the liquid tile
            if (player.noBuilding ||
                !(player.position.X / 16f - (float)Player.tileRangeX - (float)item.tileBoost <= (float)Player.tileTargetX) ||
                !((player.position.X + (float)player.width) / 16f + (float)Player.tileRangeX + (float)item.tileBoost - 1f >= (float)Player.tileTargetX) ||
                !(player.position.Y / 16f - (float)Player.tileRangeY - (float)item.tileBoost <= (float)Player.tileTargetY) ||
                !((player.position.Y + (float)player.height) / 16f + (float)Player.tileRangeY + (float)item.tileBoost - 2f >= (float)Player.tileTargetY))
            {
                return;
            }
            //We set the cursor's item to be of the bucket item. To show the player that they are able to place a liquid tile at that coordinate normally
            if (!Main.GamepadDisableCursorItemIcon)
            {
                player.cursorItemIconEnabled = true;
                Main.ItemIconCacheUpdate(item.type);
            }
            //Make sure that the item is being used to place liquid
            if (!player.ItemTimeIsZero || player.itemAnimation <= 0 || !player.controlUseItem)
            {
                return;
            }
            //Get the tile position of the cursor
            Tile tile = Main.tile[Player.tileTargetX, Player.tileTargetY];
            if (tile.LiquidAmount >= 200)
            {
                return;
            }
            //Do additional tile check to make sure the target tile isn't a grate
            if (tile.HasUnactuatedTile)
            {
                bool[] tileSolid = Main.tileSolid;
                if (tileSolid[tile.TileType])
                {
                    bool[] tileSolidTop = Main.tileSolidTop;
                    if (!tileSolidTop[tile.TileType])
                    {
                        if (tile.TileType != TileID.Grate)
                        {
                            return;
                        }
                    }
                }
            }
            //We then check if the liquid at the position is ours and if the liquid amount isn't zero
            if (tile.LiquidAmount != 0)
            {
                if (tile.LiquidType != liquidType)
                {
                    return;
                }
            }

            //After all of that, we are able to place the liquid 
            //In which we...
            SoundEngine.PlaySound(SoundID.SplashWeak, player.position); //...play a sound
            tile.LiquidType = liquidType; //...create a liquid tile...
            tile.LiquidAmount = byte.MaxValue; //...at full liquid capacity
            WorldGen.SquareTileFrame(Player.tileTargetX, Player.tileTargetY); //...frame the tile to update the liquid
            if (!isBottomless)
            {
                item.stack--; //...remove the item's count
                player.PutItemInInventoryFromItemUsage(ItemID.EmptyBucket, player.selectedItem); //...create a bucket item
            }
            player.ApplyItemTime(item); //...do item usetime

            if (Main.netMode == NetmodeID.MultiplayerClient)//...lastly, if the game is MP, then sync the liquid plavement
            {
                NetMessage.sendWater(Player.tileTargetX, Player.tileTargetY);
            }
        }
    }
}
