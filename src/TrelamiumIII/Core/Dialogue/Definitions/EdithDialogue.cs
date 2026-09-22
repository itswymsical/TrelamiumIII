using TrelamiumIII.Content.Reverie.Library;

namespace TrelamiumIII.Core.Dialogue.Definitions;

/// <summary>
///   Edith at Reverie's front desk. Each category choice opens that shelf's catalog once the conversation closes; with
///   books out, she also offers to take them back.
/// </summary>
class EdithDialogue : ModDialogue
{
    private const string Speaker = "Edith";

    protected override void Register()
    {
        Dialogue.Define("Edith.Greeting")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
                .Choice(onSelected: () => LibraryCatalogSystem.Request(LibraryCategory.Philosophy))
                .Choice(onSelected: () => LibraryCatalogSystem.Request(LibraryCategory.History))
                .Choice(onSelected: () => LibraryCatalogSystem.Request(LibraryCategory.Fiction))
                .Choice(dead: true)
            .Register();

        Dialogue.Define("Edith.GreetingLoans")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(18)])
                .Choice(onSelected: () => LibraryCatalogSystem.Request(LibraryCategory.Philosophy))
                .Choice(onSelected: () => LibraryCatalogSystem.Request(LibraryCategory.History))
                .Choice(onSelected: () => LibraryCatalogSystem.Request(LibraryCategory.Fiction))
                .Choice("Edith.Returned", onSelected: () => Main.LocalPlayer.GetModPlayer<LibraryPlayer>().ReturnAll())
                .Choice(dead: true)
            .Register();

        Dialogue.Define("Edith.Returned")
            .Line(Speaker, emote: 0, speed: 1.1f, [new Pause(16)])
            .Register();
    }
}
