using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace ColorSwatches;

internal static class Swatch
{
    private static readonly Brush Edge = Freeze(new SolidColorBrush(Color.FromArgb(0xA0, 0x80, 0x80, 0x80)));

    private static readonly Brush Checker = CreateChecker();

    /// <summary>Win32 custom-colour slots, kept for the VS session.</summary>
    private static int[] customColors = new int[16];

    public static FrameworkElement Create(ColorLiteral literal, double size)
    {
        literal.GetDisplay(out byte r, out byte g, out byte b, out byte a);

        var fill = new Border { Background = Freeze(new SolidColorBrush(Color.FromArgb(a, r, g, b))) };
        return new Border
        {
            Width = size,
            Height = size,
            Margin = new Thickness(0, 0, 3, 0),
            BorderBrush = Edge,
            BorderThickness = new Thickness(1),
            Background = a < 255 ? Checker : null,
            Child = fill,
            Cursor = literal.Editable ? Cursors.Hand : Cursors.Arrow,
            ToolTip = Describe(literal),
        };
    }

    public static bool TryPick(FrameworkElement owner, ColorLiteral literal, out byte r, out byte g, out byte b)
    {
        literal.GetDisplay(out r, out g, out b, out _);

        using var dialog = new Forms.ColorDialog
        {
            AnyColor = true,
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(r, g, b),
            CustomColors = customColors,
        };

        Forms.IWin32Window? window = PresentationSource.FromVisual(owner) is HwndSource source ? new Win32Window(source.Handle) : null;
        if (dialog.ShowDialog(window) != Forms.DialogResult.OK)
            return false;

        customColors = dialog.CustomColors;
        r = dialog.Color.R;
        g = dialog.Color.G;
        b = dialog.Color.B;
        return true;
    }

    private static string Describe(ColorLiteral literal)
    {
        literal.GetDisplay(out byte r, out byte g, out byte b, out byte a);
        CultureInfo inv = CultureInfo.InvariantCulture;

        var text = new StringBuilder()
            .Append($"#{r:X2}{g:X2}{b:X2}    Rgb({r}, {g}, {b})")
            .AppendLine()
            .Append(string.Format(inv, "Vector3({0:0.###}f, {1:0.###}f, {2:0.###}f)", literal.R, literal.G, literal.B));

        if (literal.Intensity > 1f)
            text.AppendLine().Append(string.Format(inv, "HDR: swatch shows the hue at 1/{0:0.##} intensity", literal.Intensity));

        if (a < 255)
            text.AppendLine().Append(string.Format(inv, "Alpha {0} ({1:0.##})", a, literal.A));

        text.AppendLine().Append(literal.Editable ? "Click to pick a colour" : "Expression arguments: preview only");
        return text.ToString();
    }

    private static Brush CreateChecker()
    {
        var dark = new GeometryDrawing(Brushes.Gray, null, new GeometryGroup
        {
            Children = { new RectangleGeometry(new Rect(0, 0, 1, 1)), new RectangleGeometry(new Rect(1, 1, 1, 1)) },
        });
        var light = new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 2, 2)));

        return Freeze(new DrawingBrush(new DrawingGroup { Children = { light, dark } })
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 6, 6),
            ViewportUnits = BrushMappingMode.Absolute,
        });
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private sealed class Win32Window(IntPtr handle) : Forms.IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
