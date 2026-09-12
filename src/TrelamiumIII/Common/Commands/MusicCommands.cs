using System;
using TrelamiumIII.Content.Fungore;
using TrelamiumIII.Content.Menus;
using TrelamiumIII.Core.Audio;

namespace TrelamiumIII.Common.Commands;

/// <summary>Drives <see cref="MusicDirector" /> by hand.</summary>
public class MusicCommand : ModCommand
{
    public override string Command => "music";

    public override CommandType Type => CommandType.Chat;

    public override string Usage =>
        "/music play <fungore|ink> | layer <name> <on|off> [fade] | fire <name> [grid] | beat | stop [fade]";

    public override string Description => "Drives the layered music director.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        // Commands run on the server in multiplayer, which has no music registry.
        if (Main.dedServ)
        {
            caller.Reply("The music director only exists on a client.", Color.Red);

            return;
        }

        if (args.Length == 0)
        {
            caller.Reply($"Usage: {Usage}", Color.Yellow);

            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "play":
                Play(caller, args);
                break;

            case "layer":
                Layer(caller, args);
                break;

            case "fire":
                Fire(caller, args);
                break;

            case "beat":
                Beat(caller);
                break;

            case "stop":
                MusicDirector.Stop(Number(args, 1, 1.5f));
                caller.Reply("Stopping.", Color.Gray);
                break;

            default:
                caller.Reply($"Usage: {Usage}", Color.Yellow);
                break;
        }
    }

    private static void Play(CommandCaller caller, string[] args)
    {
        if (args.Length < 2)
        {
            caller.Reply("Which score? fungore | ink", Color.Yellow);

            return;
        }

        MusicScore? score = args[1].ToLowerInvariant() switch
        {
            "fungore" => FungoreMusic.Create(),
            "ink" => IlluminantInkMusic.Create(),
            _ => null,
        };

        if (score is null)
        {
            caller.Reply($"No score named \"{args[1]}\". Try fungore or ink.", Color.Red);

            return;
        }

        // Copied without the score's release condition, which would end it instantly when no Fungore exists.
        MusicDirector.Play(new MusicScore
        {
            Name = score.Name,
            Layers = score.Layers,
            Bpm = score.Bpm,
            BeatsPerBar = score.BeatsPerBar,
            GridOffset = score.GridOffset,
            LoopSeconds = score.LoopSeconds,
        });

        caller.Reply($"Playing \"{score.Name}\" at {score.Bpm} BPM.", Color.LightGreen);

        foreach (MusicLayer layer in score.Layers)
            caller.Reply($"  {layer.Name} ({layer.Kind}){(layer.StartsOn ? " -- open" : "")}", Color.White);
    }

    private static void Layer(CommandCaller caller, string[] args)
    {
        if (args.Length < 3)
        {
            caller.Reply("Usage: /music layer <name> <on|off> [fade]", Color.Yellow);

            return;
        }

        bool on = args[2].Equals("on", StringComparison.OrdinalIgnoreCase);

        MusicDirector.SetLayer(args[1], on, Number(args, 3, 1.5f));

        caller.Reply($"{args[1]} -> {(on ? "on" : "off")}", Color.LightGreen);
    }

    private static void Fire(CommandCaller caller, string[] args)
    {
        if (args.Length < 2)
        {
            caller.Reply("Usage: /music fire <name> [immediate|beat|halfbar|bar|twobars|fourbars]", Color.Yellow);

            return;
        }

        MusicGrid grid = args.Length > 2 && Enum.TryParse(args[2], ignoreCase: true, out MusicGrid parsed)
            ? parsed
            : MusicGrid.Bar;

        float delay = MusicDirector.Fire(args[1], grid);

        if (delay < 0f)
        {
            caller.Reply($"No cue named \"{args[1]}\" in the running score.", Color.Red);

            return;
        }

        caller.Reply($"{args[1]} lands in {delay:F3}s (on the next {grid}).", Color.LightGreen);
    }

    private static void Beat(CommandCaller caller)
    {
        if (!MusicDirector.IsPlaying)
        {
            caller.Reply("Nothing playing.", Color.Gray);

            return;
        }

        caller.Reply(
            $"{MusicDirector.Playing}: bar {MusicDirector.Bars:F2}, beat {MusicDirector.Beats:F2}, bar phase {MusicDirector.BarPhase:F3}",
            Color.LightGreen
        );
    }

    private static float Number(string[] args, int index, float fallback)
        => args.Length > index && float.TryParse(args[index], out float value) ? value : fallback;
}
