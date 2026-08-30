namespace TrelamiumIII.Utilities;

public static class RectangleExtensions
{
    /// <summary>
    /// Returns a new centered rectangle inside a parent container
    /// </summary>
    /// <param name="inner"></param>
    /// <param name="outer"></param>
    /// <returns></returns>
    public static Rectangle CenterIn(this Rectangle inner, Rectangle outer)
    {
        int x = outer.X + (outer.Width - inner.Width) / 2;
        int y = outer.Y + (outer.Height - inner.Height) / 2;
        return new Rectangle(x, y, inner.Width, inner.Height);
    }
    /// <summary>
    /// Returns the exact Center Point coordinates of a rectangle
    /// </summary>
    /// <param name="rect"></param>
    /// <returns></returns>
    public static Point GetCenterPoint(this Rectangle rect)
    {
        return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }
}
