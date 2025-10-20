using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class ParserIntegrationTests
{
    private readonly string _testDataPath;

    public ParserIntegrationTests()
    {
        _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
    }

    private string GetTestFilePath(string fileName)
    {
        return Path.Combine(_testDataPath, fileName);
    }

    [Fact]
    public void MapDataParser_ParseTestMapFile_ShouldReturnCorrectStructure()
    {
        // Arrange
        var mapFile = GetTestFilePath("test_map.csv");

        // Act
        var mapElements = MapElementParser.Parse(mapFile);
        var mapData = MapElementParser.ToMapData(mapElements);

        // Assert
        mapElements.Count.ShouldBe(14);

        // Check start points
        mapData.StartPoints.Count.ShouldBe(6);
        mapData.StartPoints.ShouldContainKey("SP01");
        mapData.StartPoints["SP01"].X.ShouldBe(1);
        mapData.StartPoints["SP01"].Y.ShouldBe(6);

        // Check end points
        mapData.EndPoints.Count.ShouldBe(6);
        mapData.EndPoints.ShouldContainKey("EP01");
        mapData.EndPoints["EP01"].X.ShouldBe(6);
        mapData.EndPoints["EP01"].Y.ShouldBe(4);

        // Check AGV positions
        mapData.AgvPositions.Count.ShouldBe(2);
        mapData.AgvPositions.ShouldContainKey("AGV01");
        mapData.AgvPositions["AGV01"].X.ShouldBe(3);
        mapData.AgvPositions["AGV01"].Y.ShouldBe(1);
        mapData.AgvPositions["AGV01"].Pitch.ShouldBe(Direction.Up);

        // Check pickup rules
        mapData.PickupRules.Count.ShouldBe(6);
        mapData.PickupRules.ShouldContainKey("SP01");
        // SP01在左侧，所以取货点应该在右侧 (x+1)
        mapData.PickupRules["SP01"].X.ShouldBe(2);
        mapData.PickupRules["SP01"].Y.ShouldBe(6);
    }

    [Fact]
    public void TaskCsvParser_ParseTestTaskFile_ShouldReturnCorrectTasks()
    {
        // Arrange
        var taskFile = GetTestFilePath("test_tasks.csv");

        // Act
        var tasks = TaskCsvParser.Parse(taskFile);

        // Assert
        tasks.Count.ShouldBe(5);

        // Test specific task details
        var tigerTask = tasks.First(t => t.TaskId == "Task-SP01-1");
        tigerTask.StartPoint.ShouldBe("SP01");
        tigerTask.EndPoint.ShouldBe("EP10");
        tigerTask.Priority.ShouldBe(TaskPriority.High);
        tigerTask.RemainingTime.ShouldBe(150);

        var horseTask = tasks.First(t => t.TaskId == "Task-SP03-1");
        horseTask.RemainingTime.ShouldBeNull();
    }

    [Fact]
    public void AgvTrajectoryParser_ParseErrorTrajectoryFile_ShouldReturnRecordsWithErrors()
    {
        // Arrange
        var trajectoryFile = GetTestFilePath("test_trajectory_errors.csv");

        // Act
        var trajectories = TrajectoryRecordParser.Parse(trajectoryFile);

        // Assert
        trajectories.Count.ShouldBe(11);

        // This trajectory should contain various errors like:
        // - Out of bounds positions
        // - Diagonal movement
        // - Too fast movement
        // - Invalid rotations
        var optimusRecords = trajectories.Where(t => t.Name == "AGV01").ToList();
        optimusRecords.Count.ShouldBeGreaterThan(0);

        // Check for out of bounds record
        var outOfBounds = optimusRecords.Any(r => r.X > 20 || r.Y > 20 || r.X < 1 || r.Y < 1);
        outOfBounds.ShouldBeTrue("Should contain out of bounds positions");
    }

    [Fact]
    public void ParserIntegration_AllFiles_ShouldWorkTogether()
    {
        // Arrange
        var trajectoryFile = GetTestFilePath("test_trajectory.csv");
        var mapFile = GetTestFilePath("test_map.csv");
        var taskFile = GetTestFilePath("test_tasks.csv");

        // Act - Parse all files
        var trajectories = TrajectoryRecordParser.Parse(trajectoryFile);
        var mapElements = MapElementParser.Parse(mapFile);
        var mapData = MapElementParser.ToMapData(mapElements);
        var tasks = TaskCsvParser.Parse(taskFile);

        // Assert - Data consistency checks
        trajectories.ShouldNotBeNull();
        mapData.ShouldNotBeNull();
        tasks.ShouldNotBeNull();

        // Check that all AGVs in trajectory exist in map
        var trajectoryAgvs = trajectories.Select(t => t.Name).Distinct().ToList();
        var mapAgvs = mapData.AgvPositions.Keys.ToList();

        foreach (var agvName in trajectoryAgvs)
        {
            mapAgvs.ShouldContain(agvName, $"AGV {agvName} in trajectory should exist in map");
        }

        // Check that task endpoints exist in map
        var taskEndpoints = tasks.Select(t => t.EndPoint).Distinct().ToList();
        var mapEndpoints = mapData.EndPoints.Keys.ToList();

        foreach (var endpoint in taskEndpoints)
        {
            mapEndpoints.ShouldContain(endpoint, $"Endpoint {endpoint} in tasks should exist in map");
        }

        // Check that task start points exist in map
        var taskStartpoints = tasks.Select(t => t.StartPoint).Distinct().ToList();
        var mapStartpoints = mapData.StartPoints.Keys.ToList();

        foreach (var startpoint in taskStartpoints)
        {
            mapStartpoints.ShouldContain(startpoint, $"Start point {startpoint} in tasks should exist in map");
        }
    }

    [Fact]
    public void MapDataParser_GetBounds_ShouldReturnCorrectBounds()
    {
        // Arrange
        var mapFile = GetTestFilePath("test_map.csv");

        // Act
        var mapElements = MapElementParser.Parse(mapFile);
        var bounds = MapElementParser.GetBounds(mapElements);

        // Assert
        bounds.Left.ShouldBe(1);
        bounds.Bottom.ShouldBe(1);
        bounds.Right.ShouldBe(20);
        bounds.Top.ShouldBe(16);
    }

    [Fact]
    public void MapDataParser_GroupByType_ShouldGroupCorrectly()
    {
        // Arrange
        var mapFile = GetTestFilePath("test_map.csv");

        // Act
        var mapElements = MapElementParser.Parse(mapFile);
        var grouped = MapElementParser.GroupByType(mapElements);

        // Assert
        grouped.ShouldContainKey("StartPoint");
        grouped.ShouldContainKey("EndPoint");
        grouped.ShouldContainKey("Agv");

        grouped["StartPoint"].Count.ShouldBe(6);
        grouped["EndPoint"].Count.ShouldBe(6);
        grouped["Agv"].Count.ShouldBe(2);
    }

    [Fact]
    public void TestDataFiles_ShouldHaveCorrectFormat()
    {
        // Test that all CSV files have proper headers and data format
        var files = new[]
        {
            ("test_map.csv", new[] { "type", "name", "x", "y", "pitch" }),
            ("test_tasks.csv", ["id", "start_point", "end_point", "priority", "remaining_time"]),
            ("test_trajectory.csv", ["timestamp", "name", "X", "Y", "pitch", "loaded", "destination", "Emergency", "id"]),
            ("test_trajectory_errors.csv", ["timestamp", "name", "X", "Y", "pitch", "loaded", "destination", "Emergency", "id"])
        };

        foreach (var (fileName, expectedHeaders) in files)
        {
            var filePath = GetTestFilePath(fileName);
            var lines = File.ReadAllLines(filePath);

            lines.Length.ShouldBeGreaterThan(0, $"File {fileName} should not be empty");

            var headers = lines[0].Split(',');
            headers.Length.ShouldBe(expectedHeaders.Length, $"File {fileName} should have {expectedHeaders.Length} columns");

            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                headers[i].ShouldBe(expectedHeaders[i], $"Header {i} in {fileName} should be {expectedHeaders[i]}");
            }
        }
    }
}
