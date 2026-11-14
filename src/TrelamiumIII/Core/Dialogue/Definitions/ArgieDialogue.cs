using TrelamiumIII.Content.NPCs.WorldNPCs.Argie;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Argie's conversations. Flow lives here; text and pause anchors live in <c>Localization/DialogueLibrary/Argie</c>.
///   Test in game with <c>/dialogue play Argie.Greeting</c>, or replay the meeting with <c>/argie reset</c>.
/// </summary>
class ArgieDialogue : ModDialogue
{
    private const string Speaker = "Argie";

    protected override void Register()
    {
        RegisterMeeting();
        RegisterFungusHunt();
        RegisterSporeSurvey();
    }

    /// <summary>
    ///   Both greetings lead into <see cref="ArgieMeeting.BrighterKey" />, where the mushrooms brighten around the player
    ///   and she offers the fungus hunt. The letter greeting takes Silvian's letter once the conversation completes.
    /// </summary>
    private static void RegisterMeeting()
    {
        Dialogue.Define(ArgieNPC.GreetingKey)
            .Line(Speaker, emote: 0, speed: 1.15f, [new Pause(14), new Pause(14), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(12), new Pause(28), new Pause(14)])
                .Choice(ArgieMeeting.BrighterKey)
                .Choice(ArgieMeeting.BrighterKey)
            .Register();

        Dialogue.Define(ArgieNPC.LetterGreetingKey)
            .Line(Speaker, emote: 0, speed: 1.15f, [new Pause(14), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1f)
                .Choice("Argie.Letter")
                .Choice("Argie.Letter")
            .Register();

        Dialogue.Define("Argie.Letter")
            .Line(Speaker, emote: 0, speed: 0.95f, [new Pause(30), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 0.9f, [new Pause(30), new Pause(18)])
                .Choice(ArgieMeeting.BrighterKey)
                .Choice(ArgieMeeting.BrighterKey)
            .OnComplete(ArgieMeeting.TakeLetter)
            .Register();

        Dialogue.Define(ArgieMeeting.BrighterKey)
            .Line(Speaker, emote: 0, speed: 0.85f, [new Pause(40), new Pause(30)])
            .Line(Speaker, emote: 0, speed: 1.05f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.15f, [new Pause(14), new Pause(16), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18), new Pause(14), new Pause(12)])
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(18), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.15f, new Pause(20))
            .UnlockJob(JobID.MycologyHunt)
            .Register();

        // Met her before Silvian handed the letter over.
        Dialogue.Define("Argie.LateLetter")
            .Line(Speaker, emote: 0, speed: 0.95f, [new Pause(30), new Pause(20)])
            .Line(Speaker, emote: 0, speed: 0.9f, [new Pause(30), new Pause(18), new Pause(16)])
            .OnComplete(ArgieMeeting.TakeLetter)
            .Register();

        // For a player who cut the meeting short before it offered the job. See ArgieNPC.GetEncounter.
        Dialogue.Define("Argie.JobOffer")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(14), new Pause(12), new Pause(12), new Pause(16)])
            .UnlockJob(JobID.MycologyHunt)
            .Register();
    }

    private static void RegisterFungusHunt()
    {
        Dialogue.Define("Argie.MycologyHunt_Greeting")
            .Line(Speaker, emote: 0, speed: 1.15f, [new Pause(12), new Pause(12), new Pause(16), new Pause(16), new Pause(10)])
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(16))
            .Register();

        Dialogue.Define("Argie.MycologyHunt_HandIn")
            .Line(Speaker, emote: 0, speed: 1.15f, [new Pause(14), new Pause(10)])
                .HandIn(JobID.MycologyHunt)
            .Register();

        // Ends on the spore survey offer. Declining or cutting it short leaves Argie.SurveyOffer to ask again.
        Dialogue.Define("Argie.MycologyHunt_Complete")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(16), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 0.95f, new Pause(24))
            .Line(Speaker, emote: 0, speed: 0.95f, new Pause(20))
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16), new Pause(12), new Pause(10), new Pause(10), new Pause(18)])
                .Choice("Argie.SurveyOffer_Yes")
                .Choice("Argie.SurveyOffer_No", dead: true)
            .Register();
    }

    private static void RegisterSporeSurvey()
    {
        Dialogue.Define("Argie.SurveyOffer")
            .Line(Speaker, emote: 0, speed: 1.05f, new Pause(18))
                .Choice("Argie.SurveyOffer_Yes")
                .Choice("Argie.SurveyOffer_No", dead: true)
            .Register();

        Dialogue.Define("Argie.SurveyOffer_Yes")
            .Line(Speaker, emote: 0, speed: 1.1f, new Pause(18))
            .UnlockJob(JobID.SporeSurvey)
            .Register();

        Dialogue.Define("Argie.SurveyOffer_No")
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(18), new Pause(20)])
            .Register();

        Dialogue.Define("Argie.SporeSurvey_Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(18)])
            .Register();

        Dialogue.Define("Argie.SporeSurvey_HandIn")
            .Line(Speaker, emote: 0, speed: 1.15f, new Pause(12))
                .HandIn(JobID.SporeSurvey)
            .Register();

        // The spore itself comes in the job's reward bag.
        Dialogue.Define("Argie.SporeSurvey_Complete")
            .Line(Speaker, emote: 0, speed: 0.95f, [new Pause(24), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 0.95f, [new Pause(24), new Pause(18), new Pause(24)])
            .Line(Speaker, emote: 0, speed: 1.05f, [new Pause(18), new Pause(18)])
            .Line(Speaker, emote: 0, speed: 1f, [new Pause(20), new Pause(14)])
            .Line(Speaker, emote: 0, speed: 1.05f, new Pause(18))
            .Register();

        // After the survey, whenever the player has no spore and Fungore isn't up. See ArgieMeeting.SporeDue.
        Dialogue.Define("Argie.SporeAgain")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(14), new Pause(20), new Pause(14)])
            .OnComplete(ArgieMeeting.GiveSpore)
            .Register();
    }
}
