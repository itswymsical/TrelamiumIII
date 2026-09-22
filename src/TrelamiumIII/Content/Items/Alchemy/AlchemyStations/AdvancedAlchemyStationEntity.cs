using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.Alchemy.AlchemyStations;

/// <summary>
/// Per-station state at the base's top-left tile: contents and installed segments. The tile renders from it and the
/// UI binds its slots to these arrays, so edits show live on the tile.
/// </summary>
public class AdvancedAlchemyStationEntity : ModTileEntity
{
    public const int ComponentSocketCount = 3;

    // Item[] (not Item) so the UI's BasicItemSlots can bind directly to these arrays.
    public readonly Item[] Components = new Item[ComponentSocketCount];
    public readonly Item[] Input = new Item[1];
    public readonly Item[] Output = new Item[1];

    /// <summary>Empty bottles returned by brews whose recipe returns them. Take-only in the UI.</summary>
    public readonly Item[] BottleReturn = new Item[1];

    public AlchemyStationMode Mode;

    public bool HasChamber;

    public readonly bool[] ComponentPresent = new bool[ComponentSocketCount];

    /// <summary>Ticks in <see cref="Update"/> independently of the UI.</summary>
    public bool IsBrewing;

    /// <summary>True once a brew finishes; the result waits in <see cref="Output"/> until the player collects it.</summary>
    public bool ResultReady;

    /// <summary>Ticks elapsed and total for the active brew, plus how many batch units it makes.</summary>
    public int BrewTimer;
    public int BrewDuration;
    public int BrewCount;

    /// <summary>Whether completing the active brew adds <see cref="BrewCount" /> bottles to <see cref="BottleReturn" />.</summary>
    public bool BrewReturnsBottles;

    /// <summary>Items the input slot gives up per batch unit, locked in when the brew starts.</summary>
    public int BrewInputCost = 1;

    /// <summary>
    /// Units each component socket gives up per batch unit, locked in when the brew starts. Persisted and synced because
    /// completion can't re-ask the recipe once the UI is gone.
    /// </summary>
    public readonly int[] BrewComponentCost = new int[ComponentSocketCount];

    /// <summary>0..1 progress of the active brew. Drives the heat plate animation, progress bar, and chamber shake.</summary>
    public float BrewProgress => BrewDuration > 0 ? MathHelper.Clamp((float)BrewTimer / BrewDuration, 0f, 1f) : 0f;

    public AdvancedAlchemyStationEntity()
    {
        for (int k = 0; k < ComponentSocketCount; k++)
        {
            Components[k] = new Item();
            BrewComponentCost[k] = 1;
        }
        Input[0] = new Item();
        Output[0] = new Item();
        BottleReturn[0] = new Item();
    }

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<AdvancedAlchemyStationBase>();
    }

    /// <summary>
    /// Begins a brew that then runs on its own. <paramref name="result"/> is the locked-in output, already scaled by count;
    /// <paramref name="componentCosts"/> aligns to <see cref="Components"/>, and uncovered sockets draw one apiece.
    /// </summary>
    public void StartBrew(Item result, int duration, int count, int inputCost, int[] componentCosts, bool returnsBottles)
    {
        Output[0] = result.Clone();
        BrewDuration = duration;
        BrewCount = count;
        BrewInputCost = Math.Max(1, inputCost);
        BrewReturnsBottles = returnsBottles;
        BrewTimer = 0;
        IsBrewing = true;
        ResultReady = false;

        for (int k = 0; k < ComponentSocketCount; k++)
        {
            int cost = k < componentCosts.Length ? componentCosts[k] : 1;
            BrewComponentCost[k] = Math.Max(1, cost);
        }
    }

    /// <summary>
    /// Advances the brew every tick whether or not the UI is open. Completion is authoritative: it runs in single-player
    /// and on the server, and clients display the synced state.
    /// </summary>
    public override void Update()
    {
        if (!IsBrewing)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        BrewTimer++;

        if (BrewTimer >= BrewDuration)
            CompleteBrew();
        else if (Main.netMode == NetmodeID.Server && BrewTimer % 30 == 0)
            SyncToClients(); // keep remote clients' progress roughly live
    }

    private void CompleteBrew()
    {
        int count = BrewCount;

        // Output is already locked in; this pays for it.
        if (!Input[0].IsAir)
        {
            Input[0].stack -= BrewInputCost * count;
            if (Input[0].stack <= 0) Input[0].TurnToAir();
        }

        if (BrewReturnsBottles)
            StationBottles.Deposit(BottleReturn, count);

        for (int k = 0; k < ComponentSocketCount; k++)
        {
            if (Components[k].IsAir) continue;
            Components[k].stack -= Math.Max(1, BrewComponentCost[k]) * count;
            if (Components[k].stack <= 0) Components[k].TurnToAir();
        }

        IsBrewing = false;
        ResultReady = true;
        BrewTimer = BrewDuration;

        // only flag real recipe outputs, not fallback brews.
        if (!Output[0].IsAir && AlmanacIndex.HasRecipeFor(Output[0].type))
            AlmanacDiscoverySystem.Unlock(Output[0].type);

        // The server can't play client-side sounds; single-player plays here, MP clients on the synced transition.
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            SoundEngine.PlaySound(SoundID.ResearchComplete);
            SoundEngine.PlaySound(Assets.SFX.BrewingComplete.Asset);
        }

        SyncToClients();
    }

    public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        // The placement hook may pass any cell of the object; normalise to top-left so lookups agree.
        Point16 topLeft = WalkToTopLeft(i, j);

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            NetMessage.SendTileSquare(Main.myPlayer, topLeft.X, topLeft.Y, 3, 3);
            NetMessage.SendData(MessageID.TileEntityPlacement, -1, -1, null, topLeft.X, topLeft.Y, Type);
            return -1;
        }

        return Place(topLeft.X, topLeft.Y);
    }

    public override void OnNetPlace()
    {
        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }

    public override void SaveData(TagCompound tag)
    {
        for (int k = 0; k < ComponentSocketCount; k++)
        {
            tag[$"comp{k}"] = Components[k];
            tag[$"present{k}"] = ComponentPresent[k];
            tag[$"brewCost{k}"] = BrewComponentCost[k];
        }
        tag["vial"] = Input[0];   // key predates the input slot rename
        tag["output"] = Output[0];
        tag["bottleReturn"] = BottleReturn[0];
        tag["mode"] = (byte)Mode;
        tag["hasChamber"] = HasChamber;

        tag["isBrewing"] = IsBrewing;
        tag["resultReady"] = ResultReady;
        tag["brewTimer"] = BrewTimer;
        tag["brewDuration"] = BrewDuration;
        tag["brewCount"] = BrewCount;
        tag["brewReturnsBottles"] = BrewReturnsBottles;
        tag["brewInputCost"] = BrewInputCost;
    }

    public override void LoadData(TagCompound tag)
    {
        for (int k = 0; k < ComponentSocketCount; k++)
        {
            Components[k] = tag.Get<Item>($"comp{k}") ?? new Item();
            ComponentPresent[k] = tag.GetBool($"present{k}");
            // Absent on stations saved before solute quantities existed; one apiece was the old rule.
            BrewComponentCost[k] = Math.Max(1, tag.GetInt($"brewCost{k}"));
        }
        Input[0] = tag.Get<Item>("vial") ?? new Item();
        Output[0] = tag.Get<Item>("output") ?? new Item();
        BottleReturn[0] = tag.TryGet("bottleReturn", out Item bottles) ? bottles : new Item();
        Mode = (AlchemyStationMode)tag.GetByte("mode");
        HasChamber = tag.GetBool("hasChamber");

        IsBrewing = tag.GetBool("isBrewing");
        ResultReady = tag.GetBool("resultReady");
        BrewTimer = tag.GetInt("brewTimer");
        BrewDuration = tag.GetInt("brewDuration");
        BrewCount = tag.GetInt("brewCount");
        BrewReturnsBottles = tag.GetBool("brewReturnsBottles");
        BrewInputCost = Math.Max(1, tag.GetInt("brewInputCost"));
    }

    public override void NetSend(BinaryWriter writer)
    {
        byte flags = 0;
        if (HasChamber) flags |= 1;
        for (int k = 0; k < ComponentSocketCount; k++)
            if (ComponentPresent[k]) flags |= (byte)(1 << (k + 1));
        if (IsBrewing) flags |= 1 << 4;
        if (ResultReady) flags |= 1 << 5;
        if (BrewReturnsBottles) flags |= 1 << 6;
        writer.Write(flags);
        writer.Write((byte)Mode);

        for (int k = 0; k < ComponentSocketCount; k++)
            ItemIO.Send(Components[k], writer, writeStack: true);
        ItemIO.Send(Input[0], writer, writeStack: true);
        ItemIO.Send(Output[0], writer, writeStack: true);
        ItemIO.Send(BottleReturn[0], writer, writeStack: true);

        writer.Write(BrewTimer);
        writer.Write(BrewDuration);
        writer.Write(BrewCount);
        writer.Write(BrewInputCost);
        for (int k = 0; k < ComponentSocketCount; k++)
            writer.Write(BrewComponentCost[k]);
    }

    public override void NetReceive(BinaryReader reader)
    {
        byte flags = reader.ReadByte();
        HasChamber = (flags & 1) != 0;
        for (int k = 0; k < ComponentSocketCount; k++)
            ComponentPresent[k] = (flags & (1 << (k + 1))) != 0;
        IsBrewing = (flags & (1 << 4)) != 0;
        ResultReady = (flags & (1 << 5)) != 0;
        BrewReturnsBottles = (flags & (1 << 6)) != 0;
        Mode = (AlchemyStationMode)reader.ReadByte();

        for (int k = 0; k < ComponentSocketCount; k++)
            Components[k] = ItemIO.Receive(reader, readStack: true);
        Input[0] = ItemIO.Receive(reader, readStack: true);
        Output[0] = ItemIO.Receive(reader, readStack: true);
        BottleReturn[0] = ItemIO.Receive(reader, readStack: true);

        BrewTimer = reader.ReadInt32();
        BrewDuration = reader.ReadInt32();
        BrewCount = reader.ReadInt32();
        BrewInputCost = Math.Max(1, reader.ReadInt32());
        for (int k = 0; k < ComponentSocketCount; k++)
            BrewComponentCost[k] = Math.Max(1, reader.ReadInt32());
    }

    /// <summary>Pushes current state to other clients in multiplayer (call after the UI edits contents).</summary>
    public void SyncToClients()
    {
        if (Main.netMode != NetmodeID.SinglePlayer)
            NetMessage.SendData(MessageID.TileEntitySharing, -1, -1, null, ID, Position.X, Position.Y);
    }

    // ---- host resolution -------------------------------------------------

    /// <summary>Resolves the host entity from any cell of the base.</summary>
    public static bool TryGetHost(int i, int j, out AdvancedAlchemyStationEntity entity)
    {
        entity = null;
        if (!TryFindBaseTopLeft(i, j, out Point16 topLeft))
            return false;

        if (ByPosition.TryGetValue(topLeft, out TileEntity te) && te is AdvancedAlchemyStationEntity found)
        {
            entity = found;
            return true;
        }
        return false;
    }

    public static bool TryFindBaseTopLeft(int i, int j, out Point16 topLeft)
    {
        topLeft = default;
        Tile here = Main.tile[i, j];
        if (!here.HasTile || here.TileType != ModContent.TileType<AdvancedAlchemyStationBase>())
            return false;

        topLeft = WalkToTopLeft(i, j);
        return true;
    }

    /// <summary>Walks the multitile's frame coordinates back to its top-left cell, for any coordinate width or padding.</summary>
    private static Point16 WalkToTopLeft(int i, int j)
    {
        int left = i;
        int top = j;
        while (left > 0 && Main.tile[left, top].TileFrameX > 0)
            left--;
        while (top > 0 && Main.tile[left, top].TileFrameY > 0)
            top--;
        return new Point16(left, top);
    }
}
