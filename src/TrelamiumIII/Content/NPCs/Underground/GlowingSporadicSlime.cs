using ReLogic.Content;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using TrelamiumIII.Content.Items.Quest;
using TrelamiumIII.Content.NPCs.Forest;

namespace TrelamiumIII.Content.NPCs.Underground;

/// <summary>
///     The glowing mushroom biome's <see cref="SporadicSlime" />, in the forms of a black slime, a baby slime and a mother
///     slime. A mother splits into glowing babies when killed. One of the biome's pre-hardmode sources of
///     <see cref="SporeSample" />s.
/// </summary>
public class GlowingSporadicSlime : SporadicSlime
{
    private const int MOTHER_BABIES_MIN = 2;

    private const int MOTHER_BABIES_MAX = 3;

    private static readonly Color GlowColor = new(95, 165, 255);

    private static readonly Vector3 GlowLight = new(0.08f, 0.22f, 0.6f);

    /// <summary>Set around a mother's split so the babies it spawns skip the random pick.</summary>
    private static int? forcedVariant;

    private bool IsMother => variantType == NPCID.MotherSlime;

    public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
    {
        bestiaryEntry.Info.AddRange([BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.UndergroundMushroom,
            new FlavorTextBestiaryInfoElement("Mods.TrelamiumIII.NPCs.GlowingSporadicSlime.Bestiary")
        ]);
    }

    protected override int PickVariant()
    {
        if (forcedVariant is { } forced)
            return forced;

        return Main.rand.Next(20) switch
        {
            < 4 => NPCID.MotherSlime,
            < 12 => NPCID.BlackSlime,
            _ => NPCID.BabySlime
        };
    }

    protected override void CopyVariantStats()
    {
        base.CopyVariantStats();

        var reference = new NPC();
        reference.SetDefaults(variantType);

        NPC.scale = reference.scale;
        NPC.color = GlowColor * 0.6f;

        // Resized about the feet, so a mother doesn't spawn into the floor.
        Vector2 bottom = NPC.Bottom;
        NPC.width = reference.width;
        NPC.height = reference.height;
        NPC.Bottom = bottom;
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        if (!spawnInfo.Player.ZoneGlowshroom || spawnInfo.Water || spawnInfo.PlayerSafe)
            return 0f;

        return spawnInfo.Player.ZoneDirtLayerHeight || spawnInfo.Player.ZoneRockLayerHeight ? 0.3f : 0.12f;
    }

    public override void ModifyNPCLoot(NPCLoot npcLoot)
    {
        npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SporeSample>(), 2, 1, 2));
        npcLoot.Add(ItemDropRule.Common(ItemID.GlowingMushroom, 1, 1, 3));
        npcLoot.Add(ItemDropRule.Common(ItemID.Gel, 2, 1, 4));
    }

    public override void PostAI() => Lighting.AddLight(NPC.Center, GlowLight * NPC.scale);

    protected override Texture2D BodyTexture
    {
        get
        {
            if (!IsMother)
                return base.BodyTexture;

            Main.instance.LoadNPC(NPCID.MotherSlime);

            return TextureAssets.Npc[NPCID.MotherSlime].Value;
        }
    }

    /// <summary>The NPC frames against the blue slime sheet; the mother's sheet has the same two frames at another size.</summary>
    protected override Rectangle BodyFrame(Texture2D texture)
    {
        if (!IsMother)
            return base.BodyFrame(texture);

        int frameHeight = texture.Height / Main.npcFrameCount[NPCID.MotherSlime];
        int frame = NPC.frame.Height > 0 ? NPC.frame.Y / NPC.frame.Height : 0;

        return new Rectangle(0, frame * frameHeight, texture.Width, frameHeight);
    }

    /// <summary>Mostly its own glow, with a little of the light around it.</summary>
    protected override Color BodyColor(Color drawColor) => Color.Lerp(GlowColor, drawColor.MultiplyRGB(GlowColor), 0.25f) * 0.85f;

    protected override Asset<Texture2D>[] ChunkAssets => [Main.Assets.Request<Texture2D>($"Images/Item_{ItemID.GlowingMushroom}")];

    protected override Color ChunkColor(Color drawColor) => Color.Lerp(drawColor, Color.White, 0.7f);

    public override void OnKill()
    {
        base.OnKill();

        if (!IsMother || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int babies = Main.rand.Next(MOTHER_BABIES_MIN, MOTHER_BABIES_MAX + 1);
        forcedVariant = NPCID.BabySlime;

        try
        {
            for (int i = 0; i < babies; i++)
            {
                int index = NPC.NewNPC(NPC.GetSource_Death(), (int)NPC.Center.X + Main.rand.Next(-12, 13), (int)NPC.Bottom.Y, Type);

                if (index >= Main.maxNPCs)
                    continue;

                NPC baby = Main.npc[index];
                baby.velocity = new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -2f));

                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: index);
            }
        }
        finally
        {
            forcedVariant = null;
        }
    }
}
