using Terraria.Localization;
using TrelamiumIII.Core.Graphics.Cinematics.BossIntros;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.EyeOfCthulhu;

public class EyeOfCthulhuIntro : GlobalNPC
{    
    /// <summary>Client-side; the entrance is cosmetic and is never synced.</summary>
    private bool introStarted;
    public override bool InstancePerEntity => true;
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        => entity.type == NPCID.EyeofCthulhu;
    public override void AI(NPC npc)
    {
        base.AI(npc);

        if (Main.dedServ || introStarted)
            return;

        introStarted = true;
        
        BossIntroSystem.Play(CreateIntro());
        
        // BossIntroSystem.Settle();
    }
    private static BossIntro CreateIntro() => new()
    {
        Name = Lang.GetNPCNameValue(NPCID.EyeofCthulhu),
        Epithet = Language.GetTextValue($"Mods.{nameof(TrelamiumIII)}.NPCs.EyeOfCthulhu.Epithet"),

        TrackTitle = "Boss 1",
        TrackArtist = "Scott Lloyd Shelly",

        Accent = Color.IndianRed,
        LineRamp = 0,
        LineDensity = 0,
        LineIntensity = 0,
        MaxLength = TimeConversion.SetTime(secs: 3, ticks: 30),
    };
}
