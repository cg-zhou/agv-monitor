using System;
using System.Collections.Generic;

namespace AGV.Monitor.Geometry;

/// <summary>
/// AGV路径规划算法工具类
/// </summary>
public static class PathPlanning
{
    public const int MoveCost = 1;      // 移动一格耗时1秒
    public const int TurnCost = 1;      // 转向耗时1秒
    public const int LoadUnloadTime = 1;
    public static readonly (int Width, int Height) GridSize = (21, 21);

    /// <summary>
    /// 计算曼哈顿距离
    /// </summary>
    public static int Manhattan(PointEx p1, PointEx p2)
    {
        return Math.Abs(p1.X - p2.X) + Math.Abs(p1.Y - p2.Y);
    }

    /// <summary>
    /// A*路径规划算法（考虑朝向和转弯成本）
    /// </summary>
    public static List<PointEx> AStarWithOrientation(
        PointEx start,
        PointEx goal,
        Direction orientation,
        HashSet<PointEx> obstacles,
        (int Width, int Height)? gridSize = null)
    {
        var size = gridSize ?? GridSize;

        // 获取邻居节点（考虑朝向）
        IEnumerable<(PointEx Position, Direction Orientation, int MoveCost)> GetNeighbors(PointEx pos, Direction currentOrientation)
        {
            var directions = new[] {
                (1, 0, Direction.Right),    // 向右, 朝向0
                (-1, 0, Direction.Left), // 向左, 朝向180
                (0, 1, Direction.Up),   // 向上, 朝向90
                (0, -1, Direction.Down)  // 向下, 朝向270
            };

            foreach (var (dx, dy, orientation) in directions)
            {
                int nx = pos.X + dx;
                int ny = pos.Y + dy;

                if (nx >= 1 && nx <= size.Width &&
                    ny >= 1 && ny <= size.Height &&
                    !obstacles.Contains(new PointEx(nx, ny)))
                {
                    // 计算移动成本：移动1步 + 可能的转弯成本
                    int turnCost = orientation != currentOrientation ? TurnCost : 0;
                    int totalCost = MoveCost + turnCost;

                    yield return (new PointEx(nx, ny), orientation, totalCost);
                }
            }
        }

        var frontier = new SimplePriorityQueue<(int priority, int Cost, PointEx Position, Direction Orientation, List<PointEx> Path), int>();
        frontier.Enqueue((Manhattan(start, goal), 0, start, orientation, new List<PointEx> { start }), Manhattan(start, goal));
        var visited = new HashSet<(PointEx, Direction)>();

        while (frontier.Count > 0)
        {
            var (_, cost, currentPos, currentOrientation, path) = frontier.Dequeue();

            if (currentPos.Equals(goal))
                return path;

            var currentState = (currentPos, currentOrientation);
            if (visited.Contains(currentState))
                continue;

            visited.Add(currentState);

            foreach (var (nextPos, nextOrientation, moveCost) in GetNeighbors(currentPos, currentOrientation))
            {
                var nextState = (nextPos, nextOrientation);
                if (!visited.Contains(nextState))
                {
                    var newCost = cost + moveCost;
                    var newPath = new List<PointEx>(path) { nextPos };
                    var priority = newCost + Manhattan(nextPos, goal);

                    frontier.Enqueue((priority, newCost, nextPos, nextOrientation, newPath), priority);
                }
            }
        }

        return new List<PointEx>(); // 无路径
    }

    /// <summary>
    /// 计算路径上每个点的累计时间（包含转向时间）
    /// </summary>
    public static List<PathTimePoint> CalculatePathTiming(List<PointEx> path, Direction initialPitch)
    {
        if (path.Count == 0)
        {
            return new List<PathTimePoint>();
        }

        var result = new List<PathTimePoint>();
        var currentTime = 0;
        var currentPitch = initialPitch;

        // 起始点
        result.Add(new PathTimePoint(path[0], currentTime));

        for (var i = 1; i < path.Count; i++)
        {
            var from = path[i - 1];
            var to = path[i];
            var newPitch = from.GetPitchToNeighbour(to);

            // 如果需要转向
            if (newPitch != currentPitch)
            {
                currentTime += TurnCost;
                currentPitch = newPitch;
            }

            // 移动到下一个位置
            currentTime += MoveCost;
            result.Add(new PathTimePoint(to, currentTime));
        }

        return result;
    }
}
