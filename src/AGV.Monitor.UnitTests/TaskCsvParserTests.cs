using AGV.Monitor.Parsers;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class TaskCsvParserTests
{
    private string CreateTempCsvFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    [Fact]
    public void ParseTasks_ValidCsv_ShouldReturnCorrectTasks()
    {
        // Arrange
        var csvContent = @"id,start_point,end_point,priority,remaining_time
Task-SP01-1,SP01,EP10,High,150
Task-SP02-1,SP02,EP04,Medium,200
Task-SP03-1,SP03,EP09,Low,";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var tasks = TaskCsvParser.Parse(tempFile);

            // Assert
            tasks.ShouldNotBeNull();
            tasks.Count.ShouldBe(3);

            var firstTask = tasks.First();
            firstTask.TaskId.ShouldBe("Task-SP01-1");
            firstTask.StartPoint.ShouldBe("SP01");
            firstTask.EndPoint.ShouldBe("EP10");
            firstTask.Priority.ShouldBe(TaskPriority.High);
            firstTask.RemainingTime.ShouldBe(150);

            var lastTask = tasks.Last();
            lastTask.TaskId.ShouldBe("Task-SP03-1");
            lastTask.Priority.ShouldBe(TaskPriority.Normal);
            lastTask.RemainingTime.ShouldBeNull();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTasks_EmptyFile_ShouldReturnEmptyList()
    {
        // Arrange
        var csvContent = "id,start_point,end_point,priority,remaining_time";
        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var tasks = TaskCsvParser.Parse(tempFile);

            // Assert
            tasks.ShouldNotBeNull();
            tasks.Count.ShouldBe(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTasks_InvalidData_ShouldThrowException()
    {
        // Arrange
        var csvContent = @"id,start_point,end_point,priority,remaining_time
Task-SP01-1,SP01"; // Incomplete data

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act & Assert
            Should.Throw<InvalidDataException>(() => TaskCsvParser.Parse(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTasks_OptionalRemainingTime_ShouldHandleCorrectly()
    {
        // Arrange
        var csvContent = @"id,start_point,end_point,priority,remaining_time
Task-SP01-1,SP01,EP10,High,150
Task-SP02-1,SP02,EP04,Medium,
Task-SP03-1,SP03,EP09,Low,0";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var tasks = TaskCsvParser.Parse(tempFile);

            // Assert
            tasks.Count.ShouldBe(3);

            var firstTask = tasks.First(t => t.TaskId == "Task-SP01-1");
            firstTask.RemainingTime.ShouldBe(150);

            var secondTask = tasks.First(t => t.TaskId == "Task-SP02-1");
            secondTask.RemainingTime.ShouldBeNull();

            var thirdTask = tasks.First(t => t.TaskId == "Task-SP03-1");
            thirdTask.RemainingTime.ShouldBe(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTasks_MissingFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_task.csv";

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => TaskCsvParser.Parse(nonExistentFile));
    }

    [Fact]
    public void ParseTasks_LargeDataset_ShouldPerformEfficiently()
    {
        // Arrange
        var lines = new List<string> { "id,start_point,end_point,priority,remaining_time" };
        for (int i = 0; i < 1000; i++)
        {
            var priority = i % 3 == 0 ? "High" : i % 3 == 1 ? "Medium" : "Low";
            lines.Add($"Task-{i},Start{i % 10},End{i % 16},{priority},{i * 10}");
        }
        var csvContent = string.Join("\n", lines);

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var tasks = TaskCsvParser.Parse(tempFile);
            stopwatch.Stop();

            // Assert
            tasks.Count.ShouldBe(1000);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should complete within 1 second
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
