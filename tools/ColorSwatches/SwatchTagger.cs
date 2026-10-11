using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace ColorSwatches;

[Export(typeof(IViewTaggerProvider))]
[ContentType("CSharp")]
[TagType(typeof(IntraTextAdornmentTag))]
internal sealed class SwatchTaggerProvider : IViewTaggerProvider
{
    public ITagger<T>? CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
        if (textView is not IWpfTextView view || buffer != textView.TextBuffer)
            return null;

        return view.Properties.GetOrCreateSingletonProperty(() => new SwatchTagger(view)) as ITagger<T>;
    }
}

/// <summary>Puts a swatch before every <see cref="ColorLiteral" />; clicking it opens a colour picker that rewrites the arguments.</summary>
internal sealed class SwatchTagger : ITagger<IntraTextAdornmentTag>
{
    private readonly IWpfTextView view;

    public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

    public SwatchTagger(IWpfTextView view)
    {
        this.view = view;
        view.TextBuffer.Changed += OnBufferChanged;
        view.Closed += (_, _) => view.TextBuffer.Changed -= OnBufferChanged;
    }

    public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (spans.Count == 0)
            yield break;

        ITextSnapshot snapshot = spans[0].Snapshot;
        double size = Math.Max(8, Math.Round(view.LineHeight * 0.6));

        foreach (SnapshotSpan span in spans)
        {
            int first = snapshot.GetLineNumberFromPosition(span.Start);
            int last = snapshot.GetLineNumberFromPosition(span.End);

            for (int n = first; n <= last; n++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(n);
                foreach (ColorLiteral literal in ColorLiteral.Find(line.GetText()))
                {
                    int at = line.Start + literal.SwatchAt;
                    if (at < span.Start || at > span.End)
                        continue;

                    var args = new SnapshotSpan(snapshot, line.Start + literal.ArgsStart, literal.ArgsLength);
                    FrameworkElement swatch = Swatch.Create(literal, size);
                    if (literal.Editable)
                        swatch.MouseLeftButtonDown += (_, e) =>
                        {
                            e.Handled = true;
                            _ = swatch.Dispatcher.BeginInvoke(new Action(() => Pick(swatch, literal, args)), DispatcherPriority.Input);
                        };

                    yield return new TagSpan<IntraTextAdornmentTag>(
                        new SnapshotSpan(snapshot, at, 0),
                        new IntraTextAdornmentTag(swatch, null, PositionAffinity.Successor));
                }
            }
        }
    }

    private void Pick(FrameworkElement swatch, ColorLiteral literal, SnapshotSpan args)
    {
        ITextBuffer buffer = view.TextBuffer;
        string original = args.GetText();
        SnapshotSpan current = args.Snapshot.CreateTrackingSpan(args, SpanTrackingMode.EdgeExclusive).GetSpan(buffer.CurrentSnapshot);
        if (current.GetText() != original || buffer.IsReadOnly(current))
            return;

        if (!Swatch.TryPick(swatch, literal, out byte r, out byte g, out byte b))
            return;

        // The dialog is modal but the buffer can still change underneath it (e.g. a file reload).
        current = current.Snapshot.CreateTrackingSpan(current, SpanTrackingMode.EdgeExclusive).GetSpan(buffer.CurrentSnapshot);
        if (current.GetText() == original)
            buffer.Replace(current, literal.Rewrite(r, g, b));
    }

    private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
    {
        foreach (ITextChange change in e.Changes)
        {
            SnapshotPoint start = e.After.GetLineFromPosition(change.NewPosition).Start;
            SnapshotPoint end = e.After.GetLineFromPosition(change.NewEnd).End;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(start, end)));
        }
    }
}
