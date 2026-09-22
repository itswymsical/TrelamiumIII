using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.Structures;

/// <summary>A rectangle of tiles copied out of the world, with the chests, signs and tile entities anchored in it.</summary>
/// <remarks>Cells are column-major, <c>x * Height + y</c>. Attachment positions are relative to the top-left.</remarks>
public sealed class StructureData
{
    public int Width { get; }

    public int Height { get; }

    public StructureCell[] Cells { get; }

    public List<StructureChest> Chests { get; } = [];

    public List<StructureSign> Signs { get; } = [];

    public List<StructureEntity> Entities { get; } = [];

    public StructureData(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
        Cells = new StructureCell[width * height];
    }

    public Point16 Size => new(Width, Height);

    public ref StructureCell this[int x, int y] => ref Cells[x * Height + y];

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>A structure that leaves every tile and wall it covers untouched.</summary>
    public static StructureData Void(int width, int height)
    {
        var data = new StructureData(width, height);

        foreach (ref StructureCell cell in data.Cells.AsSpan())
            cell.Flags = CellFlags.TileVoid | CellFlags.WallVoid;

        return data;
    }

    /// <summary>A copy of <paramref name="area"/> (local coordinates, inside this structure), with the attachments anchored in it.</summary>
    public StructureData Crop(Rectangle area)
    {
        var crop = new StructureData(area.Width, area.Height);

        for (int x = 0; x < area.Width; x++)
        {
            for (int y = 0; y < area.Height; y++)
                crop[x, y] = this[area.X + x, area.Y + y];
        }

        foreach (StructureChest chest in Chests)
        {
            if (!area.Contains(chest.X, chest.Y))
                continue;

            crop.Chests.Add(new StructureChest
            {
                X = chest.X - area.X,
                Y = chest.Y - area.Y,
                Name = chest.Name,
                Items = Array.ConvertAll(chest.Items, item => item?.Clone()),
                Loot = chest.Loot.ConvertAll(rule => rule.Clone())
            });
        }

        foreach (StructureSign sign in Signs)
        {
            if (area.Contains(sign.X, sign.Y))
                crop.Signs.Add(sign with { X = sign.X - area.X, Y = sign.Y - area.Y });
        }

        foreach (StructureEntity entity in Entities)
        {
            if (area.Contains(entity.X, entity.Y))
                crop.Entities.Add(entity with { X = entity.X - area.X, Y = entity.Y - area.Y, Data = (TagCompound)entity.Data.Clone() });
        }

        return crop;
    }
}

/// <summary>A chest or dresser, by the top-left tile of its multitile.</summary>
public sealed class StructureChest
{
    public int X;
    public int Y;
    public string Name = string.Empty;

    /// <summary>Fixed contents by slot; null is an empty slot. Sized to the source chest, which 1.4.5 lets vary.</summary>
    public Item?[] Items = new Item?[Chest.DefaultMaxItems];

    /// <summary>Rolled into the slots the fixed contents leave empty, every time the structure is generated.</summary>
    public List<LootRule> Loot = [];
}

public sealed record StructureSign(int X, int Y, string Text);

/// <summary>A tile entity at its position. <see cref="Type"/> is a vanilla entity ID in decimal or a <see cref="ModTileEntity"/> full name.</summary>
public sealed record StructureEntity(int X, int Y, string Type, TagCompound Data);
