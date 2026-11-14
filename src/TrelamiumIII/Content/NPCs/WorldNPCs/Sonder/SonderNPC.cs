using System.Collections.Generic;
using System.IO;
using Terraria.ModLoader.IO;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Sonder;

public class SonderNPC : TerrarianNPC, INarrativeSpeaker
{
    public override bool CanDespawn => false;
    public override bool HiddenOnMapUntilMet => true;

    public override List<string> SetNPCNameList() => ["Sonder"];

    protected override void SetAppearanceDefaults()
    {
        Male = false;
        ClothingStyle = PlayerClothingStyleID.MaleDefault;
        HairType = HairstyleID.Bald;
        HairColor = new Color(0, 0, 0);

        SkinColor = new Color(128, 128, 128);
        EyeColor = new Color(69, 0, 138);

        ShirtColor = new Color(128, 128, 128);
        UnderShirtColor = new Color(128, 128, 128);
        PantsColor = new Color(128, 128, 128);

        ShoeColor = new Color(0, 0, 0);

        SetHeadSlot(ItemID.TimelessTravelerHood);
        SetBodySlot(ItemID.TimelessTravelerRobe);

        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (NPC.AnyNPCs(Type))
            return 0f;

        bool inForestSurface = spawnInfo.Player.ZoneOverworldHeight && spawnInfo.Player.ZoneForest;

        bool appearance1 = inForestSurface && !Main.dayTime &&
            WorldTimeSystem.TotalDaysElapsed == 0 && !SonderWorldSystem.HasMetSonder;

        bool appearance2 = inForestSurface && Main.dayTime &&
            WorldTimeSystem.TotalDaysElapsed == 1 && SonderWorldSystem.HasMetSonder;

        return (appearance1 || appearance2) ? 0.45f : 0f;
    }

    public ChatEncounter GetEncounter(Player player)
    {
        bool hasMet = SonderWorldSystem.HasMetSonder;
        return ChatEncounter.Play(hasMet ? "Sonder.SonderReunion" : "Sonder.SonderGreeting");
    }

    // Todo: despawn after dialogue, set Met condition to true on dialogue activation.
    // Needs to be handled with other systems; DialogueSystem, WorldTimeSystem, and SonderPlayer.

    public override void PostAI()
    {
        base.PostAI();

        if (Main.netMode != NetmodeID.MultiplayerClient && HasLowerSlotDuplicate())
            Despawn();

        if (!cinematicDespawning)
            return;

        NPC.alpha = Math.Min(NPC.alpha + 8, 255);

        if (NPC.alpha >= 255)
        {
            NPC.active = false;
            VFX();
        }

    }

    private void VFX()
    {
        int j = Main.rand.Next(18, 20);
        for (int i = 0; i < j; i++)
        {
            float angle = i * (MathHelper.TwoPi / j);

            Vector2 direction = angle.ToRotationVector2();
            float speed = 4f;
            Vector2 velocity = direction * speed;

            ParticleSystem.Spawn<OusiaParticle>(NPC.Center, velocity, 120);
        }
    }

    private bool cinematicDespawning;

    /// <summary>
    /// Starts fading Sonder out before despawning.
    /// </summary>
    public static void CinematicDespawn()
    {
        foreach (NPC npc in Main.npc)
        {
            if (npc.active && npc.ModNPC is SonderNPC sonder)
            {
                sonder.cinematicDespawning = true;
                npc.dontTakeDamage = true;
                npc.velocity = Vector2.Zero;
                npc.netUpdate = true;
            }
        }
    }

    private bool HasLowerSlotDuplicate()
    {
        foreach (NPC other in Main.ActiveNPCs)
        {
            if (other.whoAmI >= NPC.whoAmI)
                return false;

            if (other.type == Type)
                return true;
        }

        return false;
    }
}

public class SonderWorldSystem : ModSystem
{
    public static bool HasMetSonder;

    public override void ClearWorld()
    {
        HasMetSonder = false;
    }

    public override void SaveWorldData(TagCompound tag)
    {
        tag["HasMetSonder"] = HasMetSonder;
    }

    public override void LoadWorldData(TagCompound tag)
    {
        HasMetSonder = tag.GetBool("HasMetSonder");
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(HasMetSonder);
    }

    public override void NetReceive(BinaryReader reader)
    {
        HasMetSonder = reader.ReadBoolean();
    }
}