using System;

namespace AGV.Monitor.Geometry;

/// <summary>
/// 二维坐标点
/// </summary>
public readonly struct PointEx
{
    public int X { get; }
    public int Y { get; }

    public PointEx(int x, int y)
    {
        X = x;
        Y = y;
    }

    public override bool Equals(object obj) => obj is PointEx other && X == other.X && Y == other.Y;
    public static bool operator ==(PointEx left, PointEx right) => left.Equals(right);
    public static bool operator !=(PointEx left, PointEx right) => !left.Equals(right);
    public override int GetHashCode() => Tuple.Create(X, Y).GetHashCode();
    public override string ToString() => $"({X}, {Y})";

    /// <summary>
    /// 四个邻接点
    /// </summary>
    public PointEx[] Neighbours => [LeftNeighbour, RightNeighbour, UpNeighbour, DownNeighbour];

    /// <summary>
    /// 左边邻接点
    /// </summary>
    public PointEx LeftNeighbour => new(X - 1, Y);

    /// <summary>
    /// 右边邻接点
    /// </summary>
    public PointEx RightNeighbour => new(X + 1, Y);

    /// <summary>
    /// 上方邻接点
    /// </summary>
    public PointEx UpNeighbour => new(X, Y - 1);

    /// <summary>
    /// 下方邻接点
    /// </summary>
    public PointEx DownNeighbour => new(X, Y + 1);

    /// <summary>
    /// 根据方向，或者邻接点
    /// </summary>
    /// <param name="direction">方向：上下左右</param>
    /// <returns>邻接点</returns>
    public PointEx GetNeighbour(Direction direction)
    {
        return direction switch
        {
            Direction.Right => RightNeighbour,
            Direction.Up => UpNeighbour,
            Direction.Left => LeftNeighbour,
            Direction.Down => DownNeighbour,
            _ => new PointEx(),
        };
    }
    public bool IsNeighbour(PointEx point)
    {
        return X == point.X && (Y == point.Y + 1 || Y == point.Y - 1)
            || Y == point.Y && (X == point.X + 1 || X == point.X - 1);
    }

    public Direction GetPitchToNeighbour(PointEx neighbourPoint)
    {
        var dX = neighbourPoint.X - X;
        var dY = neighbourPoint.Y - Y;

        if (dY == 0)
        {
            if (dX > 0) return Direction.Right;
            if (dX < 0) return Direction.Left;
        }
        else if (dX == 0)
        {
            if (dY > 0) return Direction.Up;
            if (dY < 0) return Direction.Down;
        }

        throw new Exception($"Failed to calculate pitch from point {this} to point {neighbourPoint}.");
    }
}
