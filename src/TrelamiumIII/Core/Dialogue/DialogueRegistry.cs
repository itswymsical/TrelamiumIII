using System.Collections.Generic;

namespace TrelamiumIII.Core.Dialogue;

/// <summary>
///  Central registry for code-defined dialogue sequences. Each sequence is a list of lines, and each line.
///  <para>Note: loading is handled via ILoadable so this is mostly plain data</para>
/// </summary>
public static class DialogueRegistry
{
    private static readonly Dictionary<string, DialogueSequence> sequences = new(StringComparer.OrdinalIgnoreCase);

    public static void Add(DialogueSequence sequence) => sequences[sequence.Key] = sequence;

    /// <summary>Returns the registered sequence for the key, or null if none (use the hjson fallback).</summary>
    public static DialogueSequence? Get(string key) =>
        key != null && sequences.TryGetValue(key, out var sequence) ? sequence : null;

    public static bool Has(string key) => key != null && sequences.ContainsKey(key);

    public static void Unload() => sequences.Clear();
}
