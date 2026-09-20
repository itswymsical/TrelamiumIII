namespace TrelamiumIII.Common.Systems;

public class KeybindsSystem : ModSystem
{
    public static KeybindsSystem Instance => ModContent.GetInstance<KeybindsSystem>();

    public static ModKeybind SkipCutsceneKeybind { get; private set; }
    public static ModKeybind FFDialogueKeybind { get; private set; }
    public static ModKeybind SkillTreeKeybind { get; private set; }

    public override void Load()
    {
        SkipCutsceneKeybind = KeybindLoader.RegisterKeybind(Mod, "Skip Cutscene", "Q");
        FFDialogueKeybind = KeybindLoader.RegisterKeybind(Mod, "Fast Forward Dialogue", "Q");
        SkillTreeKeybind = KeybindLoader.RegisterKeybind(Mod, "Skill Tree", "K");
    }

    public override void Unload()
    {
        SkipCutsceneKeybind = null;
        FFDialogueKeybind = null;
        SkillTreeKeybind = null;
    }
}
