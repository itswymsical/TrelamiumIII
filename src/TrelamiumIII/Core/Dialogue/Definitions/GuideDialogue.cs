namespace TrelamiumIII.Core.Dialogue.Definitions;

class GuideDialogue : ModDialogue
{
    private const string Speaker = "Guide";

    protected override void Register()
    {

        Dialogue.Define("Guide.HomeForager")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_Occupied")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Repeat")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        // What the room was missing, one per failed housing check. See HousingIssue.
        Dialogue.Define("Guide.HomeForager_NeedsDoor")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NeedsLight")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NeedsTable")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NeedsChair")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_SolidTile")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(30))
            .Register();

        Dialogue.Define("Guide.HomeForager_TooSmall")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        Dialogue.Define("Guide.HomeForager_NotEnclosed")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(24), new Pause(20)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Corrupt")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(24), new Pause(20)])
            .Register();

        Dialogue.Define("Guide.HomeForager_NoValidSpot")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Register();

        // Set transitions, fired as each stage of the job is cleared.
        Dialogue.Define("Guide.HomeForager_Set1")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Set2")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Set3")
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Set4")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(24))
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Register();

        Dialogue.Define("Guide.HomeForager_Greeting")
            .Line(Speaker, emote: 0, speed: 1.4f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(20), new Pause(16)])
            .Line(Speaker, emote: 0, speed: 1.4f, [new Pause(14), new Pause(48), new Pause(36), new Pause(20)])
            .Register();
    }
}