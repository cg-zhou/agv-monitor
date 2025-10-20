using AGV.Monitor.Parsers;
using AGV.Monitor.Views;
using AGV.Monitor.Geometry;
using Shouldly;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class HeatmapDataTests
{
    [Fact]
    public void TestHeatmapDataBasicFunctionality()
    {
        // Arrange
        var heatmapData = new HeatmapData();
        var trajectoryRecords = new List<TrajectoryRecord>
        {
            new TrajectoryRecord(1, "AGV1", 0, 0, Direction.Up, false, "Point1", false, "Task1"),
            new TrajectoryRecord(2, "AGV1", 0, 0, Direction.Up, false, "Point1", false, "Task1"), // 重复位置
            new TrajectoryRecord(3, "AGV1", 1, 1, Direction.Right, false, "Point2", false, "Task1"),
            new TrajectoryRecord(4, "AGV2", 0, 0, Direction.Down, false, "Point1", false, "Task2"), // 又一次访问(0,0)
            new TrajectoryRecord(5, "AGV2", 2, 2, Direction.Left, false, "Point3", false, "Task2")
        };

        // Act
        heatmapData.BuildFromTrajectoryRecords(trajectoryRecords);

        // Assert
        heatmapData.MaxCount.ShouldBe(3); // 位置(0,0)被访问了3次
        heatmapData.GetCount(0, 0).ShouldBe(3);
        heatmapData.GetCount(1, 1).ShouldBe(1);
        heatmapData.GetCount(2, 2).ShouldBe(1);
        heatmapData.GetCount(5, 5).ShouldBe(0); // 未访问的位置

        heatmapData.GetHeatRatio(0, 0).ShouldBeGreaterThan(0.05); // 最热点，应该明显高于绿色
        heatmapData.GetHeatRatio(1, 1).ShouldBe(0.01, 0.001); // 最小访问次数，应该是绿色（0.01）
        heatmapData.GetHeatRatio(5, 5).ShouldBe(0.0); // 未访问

        var allPositions = heatmapData.GetAllPositions().ToArray();
        allPositions.Length.ShouldBe(3); // 3个不同的位置
        allPositions.ShouldContain(pos => pos.X == 0 && pos.Y == 0 && pos.Count == 3);
        allPositions.ShouldContain(pos => pos.X == 1 && pos.Y == 1 && pos.Count == 1);
        allPositions.ShouldContain(pos => pos.X == 2 && pos.Y == 2 && pos.Count == 1);
    }

    [Fact]
    public void TestHeatmapDataEmpty()
    {
        // Arrange
        var heatmapData = new HeatmapData();
        var emptyTrajectoryRecords = new List<TrajectoryRecord>();

        // Act
        heatmapData.BuildFromTrajectoryRecords(emptyTrajectoryRecords);

        // Assert
        heatmapData.MaxCount.ShouldBe(0);
        heatmapData.GetCount(0, 0).ShouldBe(0);
        heatmapData.GetHeatRatio(0, 0).ShouldBe(0.0);
        heatmapData.GetAllPositions().ShouldBeEmpty();
    }

    [Fact]
    public void TestHeatmapDataClear()
    {
        // Arrange
        var heatmapData = new HeatmapData();
        var trajectoryRecords = new List<TrajectoryRecord>
        {
            new TrajectoryRecord(1, "AGV1", 0, 0, Direction.Up, false, "Point1", false, "Task1")
        };

        heatmapData.BuildFromTrajectoryRecords(trajectoryRecords);
        heatmapData.GetCount(0, 0).ShouldBe(1); // 确保有数据

        // Act
        heatmapData.Clear();

        // Assert
        heatmapData.MaxCount.ShouldBe(0);
        heatmapData.GetCount(0, 0).ShouldBe(0);
        heatmapData.GetAllPositions().ShouldBeEmpty();
    }

    [Fact]
    public void TestHeatmapDataStatistics()
    {
        // Arrange
        var heatmapData = new HeatmapData();
        var trajectoryRecords = new List<TrajectoryRecord>
        {
            new TrajectoryRecord(1, "AGV1", 0, 0, Direction.Up, false, "Point1", false, "Task1"),
            new TrajectoryRecord(2, "AGV1", 0, 0, Direction.Up, false, "Point1", false, "Task1"),
            new TrajectoryRecord(3, "AGV1", 1, 1, Direction.Right, false, "Point2", false, "Task1")
        };

        // Act
        heatmapData.BuildFromTrajectoryRecords(trajectoryRecords);
        var statistics = heatmapData.GetStatistics();

        // Assert
        statistics.ShouldContain("2个位置");
        statistics.ShouldContain("3次访问");
        statistics.ShouldContain("平均1.5次/位置");
        statistics.ShouldContain("最高2次");
    }
}
