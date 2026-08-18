using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.AlchemyStations;

/// <summary>
/// Per-station state anchored at the top-left tile of the <see cref="AdvancedAlchemyStationBase"/>.
/// Owns the renderable + interactive contents and tracks which optional segments have been installed
/// onto the base. The world tile renders from this entity; the UI binds its slots to these arrays
/// so edits show live on the tile.
/// </summary>
public class AdvancedAlchemyStationEntity : ModTileEntity
{
    public const int ComponentSocketCount = 3;

    // Item[] (not Item) so the UI's BasicItemSlots can bind directly to these arrays.
    public readonly Item[] Components = new Item[ComponentSocketCount];
    public readonly Item[] Vial = new Item[1];
    public readonly Item[] Output = new Item[1];

    /// <summary>Whether the chamber segment has been installed onto this base.</summary>
    public bool HasChamber;

    /// <summary>Whether each of the three component segments has been installed onto this base.</summary>
    public readonly bool[] ComponentPresent = new bool[ComponentSocketCount];

    /// <summary>True while this station is brewing. Ticks on its own in <see cref="Update"/>, independent of the UI.</summary>
    public bool IsBrewing;

    /// <summary>True once a brew finishes; the result waits in <see cref="Output"/> until the player collects it.</summary>
    public bool ResultReady;

    /// <summary>Ticks elapsed and total for the active brew, plus how many batch units it makes.</summary>
    public int BrewTimer;
    public int BrewDuration;
    public int BrewCount;

    /// <summary>
    /// Units each component socket gives up per batch unit, the recipe's potency draw on that
    /// slot, locked in when the brew started. Persisted and synced because the brew outlives the
    /// UI that resolved it, so completion can't go back and ask what the recipe wanted.
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
        Vial[0] = new Item();
        Output[0] = new Item();
    }

    public override bool IsTileValidForEntity(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return tile.HasTile && tile.TileType == ModContent.TileType<AdvancedAlchemyStationBase>();
    }

    /// <summary>
    /// Begins a brew. <paramref name="result"/> is the locked-in output (already scaled by count); it sits in
    /// <see cref="Output"/> as a silhouette until the timer fills. Called by the UI; the brew then runs on its own.
    /// <para><paramref name="componentCosts"/> is the recipe's per-socket unit draw, aligned to
    /// <see cref="Components"/>. Sockets it doesn't cover fall back to one apiece.</para>
    /// </summary>
    public void StartBrew(Item result, int duration, int count, int[]? componentCosts = null)
    {
        Output[0] = result.Clone();
        BrewDuration = duration;
        BrewCount = count;
        BrewTimer = 0;
        IsBrewing = true;
        ResultReady = false;

        for (int k = 0; k < ComponentSocketCount; k++)
        {
            int cost = componentCosts != null && k < componentCosts.Length ? componentCosts[k] : 1;
            BrewComponentCost[k] = Math.Max(1, cost);
        }
    }

    /// <summary>
    /// Advances the brew every tick: independent of whether anyone has the UI open, so large batches finish while
    /// the player is away. Completion (consuming ingredients, producing the result) is authoritative: it runs in
    /// single-player and on the server; clients display the synced state.
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

        // The result is already locked into Output; this just pays for it.
        // The solvent is one per unit: a bottle in, a bottle out.
        if (!Vial[0].IsAir)
        {
            Vial[0].stack -= count;
            if (Vial[0].stack <= 0) Vial[0].TurnToAir();
        }

        // Solutes go by the recipe's draw on each socket (its potency), times the batch.
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
        // The placement hook can pass either the top-left or another cell of the placed object depending on
        // anchoring/origin. Normalize to the top-left so Place / ByPosition lookups always agree.
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
        tag["vial"] = Vial[0];
        tag["output"] = Output[0];
        tag["hasChamber"] = HasChamber;

        // Persist the brew so big batches survive a world reload (they resume from the saved timer).
        tag["isBrewing"] = IsBrewing;
        tag["resultReady"] = ResultReady;
        tag["brewTimer"] = BrewTimer;
        tag["brewDuration"] = BrewDuration;
        tag["brewCount"] = BrewCount;
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
        Vial[0] = tag.Get<Item>("vial") ?? new Item();
        Output[0] = tag.Get<Item>("output") ?? new Item();
        HasChamber = tag.GetBool("hasChamber");

        // The brew persists and resumes from where it left off.
        IsBrewing = tag.GetBool("isBrewing");
        ResultReady = tag.GetBool("resultReady");
        BrewTimer = tag.GetInt("brewTimer");
        BrewDuration = tag.GetInt("brewDuration");
        BrewCount = tag.GetInt("brewCount");
    }

    public override void NetSend(BinaryWriter writer)
    {
        byte flags = 0;
        if (HasChamber) flags |= 1;
        for (int k = 0; k < ComponentSocketCount; k++)
            if (ComponentPresent[k]) flags |= (byte)(1 << (k + 1));
        if (IsBrewing) flags |= 1 << 4;
        if (ResultReady) flags |= 1 << 5;
        writer.Write(flags);

        for (int k = 0; k < ComponentSocketCount; k++)
            ItemIO.Send(Components[k], writer, writeStack: true);
        ItemIO.Send(Vial[0], writer, writeStack: true);
        ItemIO.Send(Output[0], writer, writeStack: true);

        writer.Write(BrewTimer);
        writer.Write(BrewDuration);
        writer.Write(BrewCount);
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

        for (int k = 0; k < ComponentSocketCount; k++)
            Components[k] = ItemIO.Receive(reader, readStack: true);
        Vial[0] = ItemIO.Receive(reader, readStack: true);
        Output[0] = ItemIO.Receive(reader, readStack: true);

        BrewTimer = reader.ReadInt32();
        BrewDuration = reader.ReadInt32();
        BrewCount = reader.ReadInt32();
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

    /// <summary>True if (i, j) is part of an Advanced Alchemy Station base; out is the top-left tile.</summary>
    public static bool TryFindBaseTopLeft(int i, int j, out Point16 topLeft)
    {
        topLeft = default;
        Tile here = Main.tile[i, j];
        if (!here.HasTile || here.TileType != ModContent.TileType<AdvancedAlchemyStationBase>())
            return false;

        topLeft = WalkToTopLeft(i, j);
        return true;
    }

    /// <summary>
    /// Walks back along the multitile's frame coordinates to the top-left cell (TileFrameX = TileFrameY = 0).
    /// Robust to any CoordinateWidth / CoordinatePadding combination.
    /// </summary>
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
