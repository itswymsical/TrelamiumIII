using TrelamiumIII.Core.Authoring;

namespace BufTest;

/// <summary>
///     Round-trip cover for the tagged-string codec. Fidelity matters more than anything else here:
///     a codec that loses a foreign tag destroys authored prose that exists nowhere else.
/// </summary>
public static class FormatTests
{
    private static readonly CharFormat Bold = new(true, false, 0f, null);

    public static int Run()
    {
        int failures = 0;

        void Check(string name, object? actual, object? expected)
        {
            bool ok = Equals(actual?.ToString(), expected?.ToString());

            if (!ok) failures++;

            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}"
                              + (ok ? "" : $"\n         got      <{actual}>\n         expected <{expected}>"));
        }

        CharFormat[] All(int count, CharFormat format)
        {
            var formats = new CharFormat[count];

            Array.Fill(formats, format);

            return formats;
        }

        Console.WriteLine("-- plain text is untouched --");
        (string text, CharFormat[] formats) = FormatCodec.Parse("hello world");
        Check("text", text, "hello world");
        Check("one format per char", formats.Length, 11);
        Check("all inherit", Array.TrueForAll(formats, f => f.IsInherit), true);
        Check("round trip is byte-identical", FormatCodec.Serialize(text, formats), "hello world");

        Console.WriteLine("-- a formatted run --");
        (text, formats) = FormatCodec.Parse("say [fmt/b:this] now");
        Check("tag stripped from text", text, "say this now");
        Check("run is bold", formats[4].Bold, true);
        Check("before the run is not", formats[3].Bold, false);
        Check("after the run is not", formats[9].Bold, false);
        Check("round trip", FormatCodec.Serialize(text, formats), "say [fmt/b:this] now");

        Console.WriteLine("-- combined attributes, since nesting is impossible --");
        (text, formats) = FormatCodec.Parse("[fmt/b,i,s1.4,cFF0000:loud]");
        Check("text", text, "loud");
        Check("bold", formats[0].Bold, true);
        Check("italic", formats[0].Italic, true);
        Check("scale", formats[0].Scale, 1.4f);
        Check("ink", formats[0].Ink, 0xFF0000);
        Check("round trip", FormatCodec.Serialize(text, formats), "[fmt/b,i,s1.4,cFF0000:loud]");

        Console.WriteLine("-- option order does not matter --");
        (_, formats) = FormatCodec.Parse("[fmt/cFF0000,s2,i,b:x]");
        Check("bold", formats[0].Bold, true);
        Check("italic", formats[0].Italic, true);
        Check("scale", formats[0].Scale, 2f);
        Check("ink", formats[0].Ink, 0xFF0000);

        Console.WriteLine("-- foreign tags are left as literal text --");
        (text, formats) = FormatCodec.Parse("the [gloss:Tidemark] was there");
        Check("gloss survives verbatim", text, "the [gloss:Tidemark] was there");
        Check("nothing formatted", Array.TrueForAll(formats, f => f.IsInherit), true);
        Check("round trip preserves it", FormatCodec.Serialize(text, formats), "the [gloss:Tidemark] was there");

        Console.WriteLine("-- a foreign tag beside one of ours --");
        (text, formats) = FormatCodec.Parse("[fmt/b:bold] and [hand:a note]");
        Check("text", text, "bold and [hand:a note]");
        Check("bold applied", formats[0].Bold, true);
        Check("hand untouched", formats[10].IsInherit, true);
        Check("round trip", FormatCodec.Serialize(text, formats), "[fmt/b:bold] and [hand:a note]");

        Console.WriteLine("-- brackets inside a run are escaped so they cannot end the tag --");
        string tagged = FormatCodec.Serialize("a [b] c", All(7, Bold));
        Check("escaped on the way out", tagged, "[fmt/b:a \\[b\\] c]");
        (text, formats) = FormatCodec.Parse(tagged);
        Check("and back in", text, "a [b] c");
        Check("still bold throughout", formats[6].Bold, true);

        Console.WriteLine("-- adjacent equal formats collapse into one tag --");
        Check("one tag not four", FormatCodec.Serialize("abcd", All(4, Bold)), "[fmt/b:abcd]");

        Console.WriteLine("-- a run that reverts mid-string --");
        CharFormat[] mixed = [Bold, Bold, CharFormat.Inherit, CharFormat.Inherit];
        Check("two runs", FormatCodec.Serialize("abcd", mixed), "[fmt/b:ab]cd");

        Console.WriteLine("-- empty and malformed input --");
        Check("null", FormatCodec.Parse(null).Text, "");
        Check("unclosed tag stays literal", FormatCodec.Parse("[fmt/b:oops").Text, "[fmt/b:oops");
        Check("no colon stays literal", FormatCodec.Parse("[fmt/b]").Text, "[fmt/b]");
        Check("bare bracket stays literal", FormatCodec.Parse("a [ b").Text, "a [ b");

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "FORMAT: ALL PASS" : $"FORMAT: {failures} FAILURE(S)");

        return failures;
    }
}
