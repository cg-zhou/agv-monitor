namespace AGV.Monitor.Geometry;

/// <summary>
/// 矩形
/// </summary>
public readonly struct RectEx
{
    public int Left { get; }
    public int Top { get; }
    public int Right { get; }
    public int Bottom { get; }

    public RectEx(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }
}
