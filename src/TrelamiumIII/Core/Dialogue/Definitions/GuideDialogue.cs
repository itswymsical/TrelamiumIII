using TrelamiumIII.Core.UI.Jobs;

namespace TrelamiumIII.Core.Dialogue.Definitions;

class GuideDialogue : ModDialogue
{
    private const string Speaker = "Guide";

    protected override void Register()
    {

        Dialogue.Define("Guide.HomeForager")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_Occupied")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Repeat")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
                .Choice("Guide.HomeForager_Name")
                .Choice("Guide.HomeForager_LandOfLight")
                .Choice("Guide.HomeForager_Skip")
            .Register();

        Dialogue.Define("Guide.HomeForager_Name")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f)
                .Choice("Guide.HomeForager_Corruption")
                .Choice("Guide.HomeForager_Corruption")
            .Register();

        Dialogue.Define("Guide.HomeForager_Skip")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f)
                .Choice("Guide.HomeForager_Corruption")
                .Choice("Guide.HomeForager_Corruption")
            .Register();

        Dialogue.Define("Guide.HomeForager_LandOfLight")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.1f)
                .Choice("Guide.HomeForager_Corruption")
                .Choice("Guide.HomeForager_Corruption")
            .Register();

        // Every branch ends here, so this is where the job tracker gets pointed out.
        Dialogue.Define("Guide.HomeForager_Corruption")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .OnComplete(JobTrackerHint.Begin)
            .Register();

        // What the room was missing, one per failed housing check. See HousingIssue.
        Dialogue.Define("Guide.HomeForager_NeedsDoor")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NeedsLight")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NeedsTable")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NeedsChair")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_SolidTile")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(30))
            .Register();

        Dialogue.Define("Guide.HomeForager_TooSmall")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NotEnclosed")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(24), new Pause(20)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Corrupt")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(24), new Pause(20)])
            .Register();

        Dialogue.Define("Guide.HomeForager_NoValidSpot")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        // A room from the Guide's own plans, shown while the player should be building one of their own.
        Dialogue.Define("Guide.HomeForager_Planned")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(20))
            .Register();

        // Set transitions, fired as each stage of the job is cleared. See HomeForager.OnSetAdvanced.
        // Materials gathered: the plans for his house.
        Dialogue.Define("Guide.HomeForager_Set1")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16)])
            .Register();

        // His house staked.
        Dialogue.Define("Guide.HomeForager_Set2")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Register();

        // His house built, and he's moved in: the Merchant's plans.
        Dialogue.Define("Guide.HomeForager_Set3")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(16))
            .Register();

        // The Merchant's house staked.
        Dialogue.Define("Guide.HomeForager_Set4")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16)])
            .Register();

        // The Merchant has arrived: one more house, without plans.
        Dialogue.Define("Guide.HomeForager_Set5")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(20), new Pause(16), new Pause(16)])
            .Register();

        // A house of their own: that's a town.
        Dialogue.Define("Guide.HomeForager_Set6")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(24))
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16)])
            .Register();

        // The hand-in, played in place of the Guide's chat window while the last set is open.
        // See HomeForager.GetChatEncounter.
        Dialogue.Define("Guide.HomeForager_CheckIn")
            .Line(Speaker, emote: 0, speed: 1.2f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.2f, [new Pause(20), new Pause(16), new Pause(24)])
            .Register();
    }
}