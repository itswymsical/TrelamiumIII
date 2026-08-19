using System;
using System.Collections.Generic;
using System.Reflection;
using TrelamiumIII.Core.Dialogue.Definitions;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///  Central registry for code-defined dialogue sequences. Each sequence is a list of lines, and each line.
/// </summary>
public static class DialogueRegistry
{
    private static readonly Dictionary<string, DialogueSequence> sequences = new(StringComparer.OrdinalIgnoreCase);

    public static void Add(DialogueSequence sequence) => sequences[sequence.Key] = sequence;

    /// <summary>Returns the registered sequence for the key, or null if none (use the hjson fallback).</summary>
    public static DialogueSequence Get(string key) =>
        key != null && sequences.TryGetValue(key, out var sequence) ? sequence : null;

    public static bool Has(string key) => key != null && sequences.ContainsKey(key);

    //public static void Load()
    //{
    //    Assembly assembly = typeof(DialogueRegistry).Assembly;

    //    foreach (Type type in assembly.GetTypes())
    //    {
    //        MethodInfo? register = type.GetMethod(
    //            "Register",
    //            BindingFlags.Public |
    //            BindingFlags.NonPublic |
    //            BindingFlags.Static);

    //        if (register is null)
    //            continue;

    //        if (register.GetParameters().Length != 0)
    //            continue;

    //        register.Invoke(null, null);
    //    }
    //}

    public static void Unload() => sequences.Clear();
}
