using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Jobs;

public sealed class CuriousSlimeI : Job
{
    public CuriousSlimeI() :
        base(JobID.CuriousSlime1, "CuriousSlime1.Name", "CuriousSlime1.Description",
        taskList: 
        [
            [("CuriousSlime1.Tasks.Set0.obj0", 1)],
            [("CuriousSlime1.Tasks.Set1.obj0", 10)],
            [("CuriousSlime1.Tasks.Set2.obj0", 1)],
        ],
        rewards: [
            new JobReward(ItemID.SilverCoin, 75)
        ],
        ModContent.NPCType<CuriousSlime>()) { }

    public override void OnJobStart()
    {
        base.OnJobStart();
        DialogueManager.Instance.StartDialogue("CuriousSlime.Greeting", true, true);
    }

    public override void OnJobComplete(bool giveRewards = true)
    {
        base.OnJobComplete(giveRewards);
        DialogueManager.Instance.StartDialogue("CuriousSlime.Job1End", false, false);
    }

    public override bool OnMatchedEvent(GameEvent evt, int taskIndex)
    {
        if (CurrentSet == 1 &&
            evt.ItemType.HasValue &&
            (evt.ItemType == ItemID.IronOre ||
             evt.ItemType == ItemID.LeadOre))
        {
            // Mark the item that contributed to this event.
            // This requires the event to expose the actual Item.
            JobItemComponent.MarkForJob(evt.Item);
        }

        if (CurrentSet == 2 &&
            evt.Type == GameEventType.NPCChat)
        {
            JobUtils.TakeMarkedJobItems(Main.LocalPlayer);
        }

        return base.OnMatchedEvent(evt, taskIndex);
    }

    public override bool MatchesEvent(GameEvent evt, int currentSet, int taskIndex)
    {
        var iron = (evt.ItemType == ItemID.IronOre) || (evt.ItemType == ItemID.LeadOre);

        // Set 0, Task 0: Talk to NPC
        if (currentSet == 0 && taskIndex == 0)
        {
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);
        }

        if (currentSet == 1 && taskIndex == 0)
        {
            return evt.Type == GameEventType.ItemPickup
                && evt.ItemType.HasValue && iron;
        }

        if (currentSet == 2 && taskIndex == 0)
        {
            return evt.Type == GameEventType.NPCChat
                && evt.NPCType.HasValue && (evt.NPCType == this.ProviderNPC);
        }

        return false;
    }
}

public class CuriousSlime : ModNPC
{
    public override string Texture => $"Terraria/Images/NPC_{NPCID.BlueSlime}";

    private static readonly int[] VariantPool =
    {
        NPCID.BlueSlime,
        NPCID.GreenSlime,
        NPCID.PurpleSlime,
        NPCID.YellowSlime,
        NPCID.Pinky,
        NPCID.RedSlime,
    };
    private int variantType = NPCID.BlueSlime;
    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 2;
        NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new()
        {
            Velocity = 0f,
            Direction = 1,
        };

        NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
    }
    public override void SetDefaults()
    {
        NPC.width = 24;
        NPC.height = 18;
        NPC.lifeMax = 100;
        NPC.dontTakeDamage = true;
        NPC.dontTakeDamageFromHostiles = true;
        NPC.friendly = true;
        NPC.aiStyle = NPCAIStyleID.Passive;
        AIType = NPCID.TownSlimeYellow;
        AnimationType = NPCID.BlueSlime;
    }

    public override void OnSpawn(IEntitySource source)
    {
        variantType = VariantPool[Main.rand.Next(VariantPool.Length)];
        CopyVariant();
        NPC.netUpdate = true;
    }

    private void CopyVariant()
    {
        NPC reference = new NPC();
        reference.SetDefaults(variantType);

        NPC.alpha = reference.alpha;
        NPC.color = reference.color;
        NPC.width = reference.width;
        NPC.height = reference.height;
    }

    public override void FindFrame(int frameHeight)
    {
        base.FindFrame(frameHeight);
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (NPC.AnyNPCs(Type))
            return 0f;

        if (Main.dayTime)
            return spawnInfo.Player.ZoneOverworldHeight && spawnInfo.Player.ZoneForest ? 0.05f : 0f;

        else return 0f;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Vector2 origin = NPC.frame.Size() / 2f;
        Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

        Color color = drawColor * 0.85f;

        return false;
    }

    public override string GetChat()
    {
        return "blorp...";
    }

    public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        Texture2D texture = TextureAssets.Npc[NPCID.BlueSlime].Value;

        Vector2 origin = NPC.frame.Size() / 2f;
        Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

        SpriteEffects effects = NPC.spriteDirection == 1
            ? SpriteEffects.None
            : SpriteEffects.FlipHorizontally;

        Color finalColor = NPC.color;
        finalColor.R = (byte)((finalColor.R * drawColor.R) / 255);
        finalColor.G = (byte)((finalColor.G * drawColor.G) / 255);
        finalColor.B = (byte)((finalColor.B * drawColor.B) / 255);
        finalColor.A = (byte)175;

        Main.EntitySpriteDraw(texture, drawPos, NPC.frame, finalColor, NPC.rotation,
            origin, NPC.scale, effects);
    }
}