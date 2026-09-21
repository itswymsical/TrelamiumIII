using System.Collections.Generic;
using System.Globalization;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace TrelamiumIII.Core.Structures;

/// <summary>Copies structures out of the world and places them back into it.</summary>
public static class StructureWorld
{
    /// <summary>Whether <paramref name="area"/> lies inside the world, one tile clear of its right and bottom edges.</summary>
    public static bool InWorld(Rectangle area)
        => area.Left >= 0 && area.Top >= 0 && area.Right < Main.maxTilesX && area.Bottom < Main.maxTilesY;

    #region Capture
    /// <summary>
    ///     Copies <paramref name="area"/>. <see cref="StructureVoidTile" /> and <see cref="StructureVoidWall" /> become void
    ///     channels, and <see cref="ChestLootEntity" /> rules move onto their chests.
    /// </summary>
    public static StructureData Capture(Rectangle area)
    {
        if (!InWorld(area))
            throw new ArgumentOutOfRangeException(nameof(area), area, "The area is not inside the world.");

        var data = new StructureData(area.Width, area.Height);
        int voidTile = ModContent.TileType<StructureVoidTile>();
        int voidWall = ModContent.WallType<StructureVoidWall>();

        for (int x = 0; x < area.Width; x++)
        {
            for (int y = 0; y < area.Height; y++)
            {
                Tile tile = Main.tile[area.X + x, area.Y + y];
                ref StructureCell cell = ref data[x, y];

                cell = StructureCell.Read(tile);

                if (tile.HasTile && tile.TileType == voidTile)
                    cell.VoidTile();

                if (tile.WallType == voidWall)
                    cell.VoidWall();
            }
        }

        CaptureChests(data, area);
        CaptureSigns(data, area);
        CaptureEntities(data, area);

        return data;
    }

    private static void CaptureChests(StructureData data, Rectangle area)
    {
        foreach (Chest? chest in Main.chest)
        {
            if (chest is null || !Anchored(data, area, chest.x, chest.y))
                continue;

            var record = new StructureChest { X = chest.x - area.X, Y = chest.y - area.Y, Name = chest.name ?? string.Empty };

            for (int slot = 0; slot < Chest.maxItems; slot++)
            {
                if (chest.item[slot] is { IsAir: false } item)
                    record.Items[slot] = item.Clone();
            }

            if (TileEntity.ByPosition.TryGetValue(new Point16(chest.x, chest.y), out TileEntity? entity) && entity is ChestLootEntity loot)
                record.Loot = loot.Rules.ConvertAll(rule => rule.Clone());

            data.Chests.Add(record);
        }
    }

    private static void CaptureSigns(StructureData data, Rectangle area)
    {
        foreach (Sign? sign in Main.sign)
        {
            if (sign is null || !Anchored(data, area, sign.x, sign.y) || !Main.tileSign[Main.tile[sign.x, sign.y].TileType])
                continue;

            data.Signs.Add(new StructureSign(sign.x - area.X, sign.y - area.Y, sign.text ?? string.Empty));
        }
    }

    private static void CaptureEntities(StructureData data, Rectangle area)
    {
        foreach ((Point16 position, TileEntity entity) in TileEntity.ByPosition)
        {
            if (entity is ChestLootEntity || !Anchored(data, area, position.X, position.Y))
                continue;

            var tag = new TagCompound();
            entity.SaveData(tag);

            string type = entity is ModTileEntity modEntity ? modEntity.FullName : entity.type.ToString(CultureInfo.InvariantCulture);
            data.Entities.Add(new StructureEntity(position.X - area.X, position.Y - area.Y, type, tag));
        }
    }

    /// <summary>Whether world tile (<paramref name="i"/>, <paramref name="j"/>) is in the area and its tile channel was captured.</summary>
    private static bool Anchored(StructureData data, Rectangle area, int i, int j)
        => area.Contains(i, j) && !data[i - area.X, j - area.Y].Has(CellFlags.TileVoid);
    #endregion

    #region Place
    /// <summary>
    ///     Writes <paramref name="data" /> with its top-left at <paramref name="origin" />. Void channels keep the world's
    ///     content, and chest loot is rolled with <see cref="WorldGen.genRand" />.
    /// </summary>
    /// <param name="editable">
    ///     Writes void markers instead of keeping the world's content, and keeps loot rules on a
    ///     <see cref="ChestLootEntity" /> instead of rolling them.
    /// </param>
    public static void Place(StructureData data, Point origin, bool editable = false)
    {
        var area = new Rectangle(origin.X, origin.Y, data.Width, data.Height);

        if (!InWorld(area))
            throw new ArgumentOutOfRangeException(nameof(origin), origin, $"A {data.Width}x{data.Height} structure there leaves the world.");

        RemoveAttachments(area, editable ? null : data);

        StructureCell voidTile = StructureCell.Marker((ushort)ModContent.TileType<StructureVoidTile>());
        var voidWall = new StructureCell { WallType = (ushort)ModContent.WallType<StructureVoidWall>() };

        for (int x = 0; x < data.Width; x++)
        {
            for (int y = 0; y < data.Height; y++)
            {
                ref readonly StructureCell cell = ref data[x, y];
                Tile tile = Main.tile[origin.X + x, origin.Y + y];

                if (!cell.Has(CellFlags.TileVoid))
                    cell.WriteTile(tile);
                else if (editable)
                    voidTile.WriteTile(tile);

                if (!cell.Has(CellFlags.WallVoid))
                    cell.WriteWall(tile);
                else if (editable)
                    voidWall.WriteWall(tile);
            }
        }

        PlaceChests(data, origin, editable);
        PlaceSigns(data, origin);
        PlaceEntities(data, origin);

        if (WorldGen.generatingWorld)
            return;

        Frame(area);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendTileSquare(-1, area.X, area.Y, area.Width, area.Height);
    }

    /// <summary>Removes every tile of <paramref name="area"/>, with its chests, signs and tile entities, and reframes the edge.</summary>
    public static void Clear(Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY));

        if (area.IsEmpty)
            return;

        RemoveAttachments(area, keepVoidOf: null);

        for (int x = area.Left; x < area.Right; x++)
        {
            for (int y = area.Top; y < area.Bottom; y++)
                Main.tile[x, y].ClearEverything();
        }

        if (!WorldGen.generatingWorld)
            Frame(area);
    }

    /// <summary>Reframes tiles and walls in <paramref name="area"/> and the ring around it.</summary>
    public static void Frame(Rectangle area)
    {
        for (int i = area.Left - 1; i <= area.Right; i++)
        {
            for (int j = area.Top - 1; j <= area.Bottom; j++)
            {
                if (!WorldGen.InWorld(i, j, 1))
                    continue;

                WorldGen.TileFrame(i, j);
                Framing.WallFrame(i, j);
            }
        }
    }

    /// <summary>Drops the chests, signs and tile entities anchored in <paramref name="area"/>, except on the void tile channels of <paramref name="keepVoidOf"/>.</summary>
    private static void RemoveAttachments(Rectangle area, StructureData? keepVoidOf)
    {
        bool Written(int i, int j) => area.Contains(i, j) && (keepVoidOf is null || !keepVoidOf[i - area.X, j - area.Y].Has(CellFlags.TileVoid));

        for (int k = 0; k < Main.chest.Length; k++)
        {
            if (Main.chest[k] is { } chest && Written(chest.x, chest.y))
                Chest.DestroyChestDirect(chest.x, chest.y, k);
        }

        for (int k = 0; k < Main.sign.Length; k++)
        {
            if (Main.sign[k] is { } sign && Written(sign.x, sign.y))
                Main.sign[k] = null;
        }

        List<TileEntity> entities = [];

        foreach ((Point16 position, TileEntity entity) in TileEntity.ByPosition)
        {
            if (Written(position.X, position.Y))
                entities.Add(entity);
        }

        lock (TileEntity.EntityCreationLock)
        {
            foreach (TileEntity entity in entities)
            {
                TileEntity.ByID.Remove(entity.ID);
                TileEntity.ByPosition.Remove(entity.Position);
            }
        }
    }

    private static void PlaceChests(StructureData data, Point origin, bool editable)
    {
        foreach (StructureChest record in data.Chests)
        {
            int x = origin.X + record.X;
            int y = origin.Y + record.Y;
            int index = Chest.CreateChest(x, y);

            if (index < 0)
            {
                TrelamiumIII.Instance.Logger.Warn($"No free chest slot for the structure chest at ({x}, {y}).");
                continue;
            }

            Chest chest = Main.chest[index];
            chest.name = record.Name;

            for (int slot = 0; slot < Chest.maxItems; slot++)
                chest.item[slot] = record.Items[slot]?.Clone() ?? new Item();

            if (record.Loot.Count == 0)
                continue;

            if (editable)
            {
                ChestLootEntity.Attach(x, y, record.Loot);
                continue;
            }

            int next = 0;

            foreach (LootRule rule in record.Loot)
            {
                if (!rule.Roll(chest, ref next, WorldGen.genRand))
                    break;
            }
        }

        EnsureChests(data, origin);
    }

    /// <summary>Gives every container the structure writes a chest entry, which files from older formats lack.</summary>
    private static void EnsureChests(StructureData data, Point origin)
    {
        for (int x = 0; x < data.Width; x++)
        {
            for (int y = 0; y < data.Height; y++)
            {
                ref readonly StructureCell cell = ref data[x, y];

                if (!cell.Has(CellFlags.HasTile) || !Main.tileContainer[cell.TileType])
                    continue;

                int i = origin.X + x;
                int j = origin.Y + y;

                if (TileObjectData.IsTopLeft(i, j) && Chest.FindChest(i, j) < 0)
                    Chest.CreateChest(i, j);
            }
        }
    }

    private static void PlaceSigns(StructureData data, Point origin)
    {
        foreach (StructureSign record in data.Signs)
        {
            int index = Sign.ReadSign(origin.X + record.X, origin.Y + record.Y, CreateIfMissing: true);

            if (index >= 0)
                Sign.TextSign(index, record.Text);
        }
    }

    private static void PlaceEntities(StructureData data, Point origin)
    {
        foreach (StructureEntity record in data.Entities)
        {
            if (!TryResolveEntity(record.Type, out int type))
            {
                TrelamiumIII.Instance.Logger.Warn($"Unknown tile entity '{record.Type}' in a structure; skipped.");
                continue;
            }

            var position = new Point16(origin.X + record.X, origin.Y + record.Y);
            TileEntity.PlaceEntityNet(position.X, position.Y, type);

            if (TileEntity.ByPosition.TryGetValue(position, out TileEntity? entity))
                entity.LoadData(record.Data);
        }
    }

    internal static bool TryResolveEntity(string name, out int type)
    {
        if (int.TryParse(name, NumberStyles.Integer, CultureInfo.InvariantCulture, out type))
            return true;

        if (ModContent.TryFind(name, out ModTileEntity entity))
        {
            type = entity.Type;
            return true;
        }

        type = -1;
        return false;
    }
    #endregion
}
