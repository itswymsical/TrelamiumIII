using TrelamiumIII.Core.Authoring;

int failures = 0;

void Check(string name, object? actual, object? expected)
{
    bool ok = Equals(actual?.ToString(), expected?.ToString());

    if (!ok) failures++;

    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}" + (ok ? "" : $"\n         got      <{actual}>\n         expected <{expected}>"));
}

Console.WriteLine("-- typing and caret --");
var b = new TextBuffer();
foreach (char c in "hello") b.Insert(c.ToString(), coalesce: true);
Check("text after typing", b.Text, "hello");
Check("caret at end", b.Caret, 5);
Check("no selection", b.HasSelection, false);

Console.WriteLine("-- undo coalesces a typing run --");
b.Undo();
Check("one undo clears the whole word", b.Text, "");

Console.WriteLine("-- undo breaks at whitespace --");
b = new TextBuffer();
foreach (char c in "hello world") b.Insert(c.ToString(), coalesce: true);
Check("typed", b.Text, "hello world");
b.Undo();
Check("undo 1 drops second word", b.Text, "hello ");
b.Undo();
Check("undo 2 drops the space", b.Text, "hello");
b.Undo();
Check("undo 3 empties", b.Text, "");
b.Redo();
Check("redo restores", b.Text, "hello");

Console.WriteLine("-- paste is never coalesced --");
b = new TextBuffer();
foreach (char c in "abc") b.Insert(c.ToString(), coalesce: true);
b.Insert("XYZ");
Check("pasted", b.Text, "abcXYZ");
b.Undo();
Check("undo removes only the paste", b.Text, "abc");

Console.WriteLine("-- selection replace --");
b = new TextBuffer("hello world");
b.SelectRange(6, 11);
Check("selected text", b.SelectedText, "world");
b.Insert("there");
Check("replaced", b.Text, "hello there");
Check("caret after insert", b.Caret, 11);

Console.WriteLine("-- reversed selection --");
b = new TextBuffer("hello world");
b.SelectRange(11, 6);
Check("reversed selection reads forward", b.SelectedText, "world");
Check("start", b.SelectionStart, 6);
Check("end", b.SelectionEnd, 11);

Console.WriteLine("-- backspace and delete --");
b = new TextBuffer("abc");
b.MoveTo(3, false);
b.Backspace();
Check("backspace", b.Text, "ab");
b.MoveTo(0, false);
b.Delete();
Check("forward delete", b.Text, "b");
b.Backspace();
Check("backspace at 0 is a no-op", b.Text, "b");
b.MoveTo(1, false);
b.Delete();
Check("delete at end is a no-op", b.Text, "b");

Console.WriteLine("-- arrow collapses a selection instead of stepping --");
b = new TextBuffer("hello world");
b.SelectRange(2, 7);
b.MoveByChar(-1, extend: false);
Check("left collapses to start", b.Caret, 2);
b.SelectRange(2, 7);
b.MoveByChar(1, extend: false);
Check("right collapses to end", b.Caret, 7);

Console.WriteLine("-- shift-arrow extends --");
b = new TextBuffer("hello");
b.MoveTo(0, false);
b.MoveByChar(1, extend: true);
b.MoveByChar(1, extend: true);
Check("extended selection", b.SelectedText, "he");
Check("anchor held", b.Anchor, 0);

Console.WriteLine("-- word movement --");
b = new TextBuffer("the quick  brown");
b.MoveTo(0, false);
b.MoveByWord(1, false);
Check("word right 1", b.Caret, 3);
b.MoveByWord(1, false);
Check("word right 2 skips double space", b.Caret, 9);
b.MoveTo(16, false);
b.MoveByWord(-1, false);
Check("word left from end", b.Caret, 11);
b.MoveTo(0, false);
b.MoveByWord(-1, false);
Check("word left at 0 stays", b.Caret, 0);

Console.WriteLine("-- select word at --");
b = new TextBuffer("alpha beta gamma");
b.SelectWordAt(7);
Check("double-click selects word", b.SelectedText, "beta");

Console.WriteLine("-- cut --");
b = new TextBuffer("hello world");
b.SelectRange(0, 6);
Check("cut returns text", b.CutSelection(), "hello ");
Check("cut removes it", b.Text, "world");
Check("cut with no selection", b.CutSelection(), "");

Console.WriteLine("-- select all --");
b = new TextBuffer("abc");
b.SelectAll();
Check("all selected", b.SelectedText, "abc");
b.Insert("z");
Check("typing replaces all", b.Text, "z");

Console.WriteLine("-- new edit clears redo --");
b = new TextBuffer();
b.Insert("one");
b.Undo();
Check("can redo", b.CanRedo, true);
b.Insert("two");
Check("redo cleared by new edit", b.CanRedo, false);

Console.WriteLine("-- reset clears history --");
b = new TextBuffer("abc");
b.Insert("d");
b.Reset("fresh");
Check("reset text", b.Text, "fresh");
Check("reset clears undo", b.CanUndo, false);

Console.WriteLine();
Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");

failures += BufTest.LayoutTests.Run();
failures += BufTest.ExportTests.Run();
failures += BufTest.CaseTests.Run();
failures += BufTest.FormatTests.Run();
failures += BufTest.BufferFormatTests.Run();
return failures == 0 ? 0 : 1;
