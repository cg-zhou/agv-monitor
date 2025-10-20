using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using AGV.Monitor.Services;
using AGV.Monitor.Services.Trajectory;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class ScheduleServiceTests
{
    [Fact]
    public void TestScheduleService()
    {
        var context = AgvContext.Create();
        var scheduler = context.Scheduler;
        scheduler.ProcessToComplete();

        scheduler.Timestamp.ShouldBeLessThan(300);

        var tasks = context.GetCompletedTasks();
        tasks.Length.ShouldBe(100);

        var maxTimeCost = tasks.Max(x => x.CompleteTimestamp - x.StartTimestamp);
        var minTimeCost = tasks.Min(x => x.CompleteTimestamp - x.StartTimestamp);
        var averageTimeCost = tasks.Average(x => x.CompleteTimestamp - x.StartTimestamp);
        maxTimeCost.ShouldBeInRange(5, 60);
        minTimeCost.ShouldBeInRange(5, 50);
        averageTimeCost.ShouldBeInRange(5, 50);
    }

    [Fact]
    public void TestRandomOrderTasks()
    {
        var errorDictionary = new ConcurrentDictionary<int, string>();

        var result = Parallel.For(5555, 5557, seed =>
        {
            var taskOrderRandom = new Random(seed);
            var context = AgvContext.Create(taskOrderRandom);

            try
            {
                var scheduler = context.Scheduler;
                scheduler.ProcessToComplete();

                var records = context.TrajectoryRecorder.GetRecords();
                records.Length.ShouldBe((scheduler.Timestamp + 1) * 12);

                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("timestamp,name,X,Y,pitch,loaded,destination,Emergency,TaskId");
                foreach (var record in records)
                {
                    stringBuilder.AppendLine($"{record.Timestamp},{record.Name},{record.X},{record.Y},{(int)record.Pitch}," +
                        $"{record.Loaded.ToString().ToLower()},{record.Destination},{record.Emergency.ToString().ToLower()},{record.TaskId}");
                }

                var path = $"./trajectory_records_{seed}.csv";
                File.WriteAllText(path, stringBuilder.ToString());

                var trajectories = TrajectoryRecordParser.Parse(path);
                var mapData = MapElementParser.ToMapData(context.MapElements);

                var validator = new TrajectoryValidateService(mapData, context.TaskRecords);

                var results = validator.ValidateTrajectories(trajectories);
                var errors = results.Where(x => !x.IsValid).ToArray();
                var errorMessage = string.Join(Environment.NewLine, errors.Select(x => $"{x.Timestamp} {x.AgvName} {x.Message}"));
                if (errors.Any())
                {
                    throw new Exception(errorMessage);
                }

                var score = 0;
                foreach (var task in context.Tasks)
                {
                    var record = records.LastOrDefault(x => x.TaskId == task.TaskId && x.Loaded);
                    if (record != null)
                    {
                        var timestamp = record.Timestamp;
                        var isEmergency = record.Emergency;
                        if (isEmergency)
                        {
                            if (timestamp > task.RemainingTime)
                            {
                                score -= 5;
                            }
                            else
                            {
                                score += 10;
                            }
                        }

                        score += 1;
                    }
                }

                scheduler.Timestamp.ShouldBeLessThan(300);

                var tasks = context.GetCompletedTasks();
                tasks.Length.ShouldBe(100);
                Debug.WriteLine($"RandomSeed={seed}, Timestamp={scheduler.Timestamp}s, Complete {tasks.Length} tasks, Score={score}");
            }
            catch (Exception e)
            {
                var message = $"{seed} {e.Message}";
                errorDictionary[seed] = message;
                Debug.WriteLine(message);

                // 将错误的数据写入CSV文件
                var taskCsvPath = $"./complex_task_csv_{seed}.csv";
                TaskCsvParser.WriteToFile(context.TaskRecords.ToArray(), taskCsvPath);
            }
        });

        if (errorDictionary.Any())
        {
            var message = string.Join(Environment.NewLine, errorDictionary.Select(x => $"Seed={x.Key}; ErrorMessage={x.Value}"));
            throw new Exception(message);
        }
        errorDictionary.Count.ShouldBe(0);
    }

    [Fact]
    public void ValidateTrajectoryCsv()
    {
        var context = AgvContext.Create();
        var scheduler = context.Scheduler;
        scheduler.ProcessToComplete();

        var records = context.TrajectoryRecorder.GetRecords();
        records.Length.ShouldBe((scheduler.Timestamp + 1) * 12);

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("timestamp,name,X,Y,pitch,loaded,destination,Emergency,TaskId");
        foreach (var record in records)
        {
            stringBuilder.AppendLine($"{record.Timestamp},{record.Name},{record.X},{record.Y},{(int)record.Pitch}," +
                $"{record.Loaded.ToString().ToLower()},{record.Destination},{record.Emergency.ToString().ToLower()},{record.TaskId}");
        }

        const string path = "./trajectory_records.csv";
        File.WriteAllText(path, stringBuilder.ToString());

        var trajectories = TrajectoryRecordParser.Parse(path);
        var mapData = MapElementParser.ToMapData(context.MapElements);

        var validator = new TrajectoryValidateService(mapData, context.TaskRecords);

        var results = validator.ValidateTrajectories(trajectories);
        var errors = results.Where(x => !x.IsValid).ToArray();
        var errorMessage = string.Join(Environment.NewLine, errors.Select(x => $"{x.Timestamp} {x.AgvName} {x.Message}"));
        if (errors.Any())
        {
            throw new Exception(errorMessage);
        }
    }

    [Fact]
    public void TestCumulativeTime()
    {
        var context = AgvContext.Create();
        var scheduler = context.Scheduler;
        var tasks = context.GetSortedPendingTasks();
        tasks.Length.ShouldBe(100);

        var cumulativeTime = 0;
        foreach (var task in tasks)
        {
            var pitch = task.PickupPosition.X > 10 ? Direction.Left : Direction.Right;
            var path = PathPlanning.AStarWithOrientation(task.PickupPosition, task.EndPosition, pitch, new HashSet<PointEx>());
            var pathTimePoints = PathPlanning.CalculatePathTiming(path, pitch);
            cumulativeTime += pathTimePoints.Last().TimeCost;
        }

        var averageCost = cumulativeTime / 12;
        averageCost.ShouldBeInRange(100, 300);
    }

    [Fact]
    public void CalculateScore()
    {
        var context = AgvContext.Create();
        var assignmentService = context.Scheduler;
        assignmentService.ProcessToComplete();

        var records = context.TrajectoryRecorder.GetRecords();

        var score = 0;
        foreach (var task in context.Tasks)
        {
            var record = records.LastOrDefault(x => x.TaskId == task.TaskId && x.Loaded);
            if (record != null)
            {
                var timestamp = record.Timestamp;
                var isEmergency = record.Emergency;
                if (isEmergency)
                {
                    if (timestamp > task.RemainingTime)
                    {
                        score -= 5;
                    }
                    else
                    {
                        score += 10;
                    }
                }

                score += 1;
            }
        }

        score.ShouldBe(120);
    }

    [Fact]
    public void CalculateScoreIn300s()
    {
        var context = AgvContext.Create();
        var scheduler = context.Scheduler;
        scheduler.ProcessToComplete();

        var records = context.TrajectoryRecorder.GetRecords()
            .Where(x => x.Timestamp <= 300)
            .ToArray();

        var score = 0;
        foreach (var task in context.TaskRecords)
        {
            var record = records.LastOrDefault(x => x.TaskId == task.TaskId && x.Loaded);
            if (record != null)
            {
                // 如果这条是小车的最后位置，不算分
                if (records.LastOrDefault(x => x.Name == record.Name) == record)
                {
                    continue;
                }

                var timestamp = record.Timestamp;
                var isEmergency = record.Emergency;
                if (isEmergency)
                {
                    if (timestamp > task.RemainingTime)
                    {
                        score -= 5;
                    }
                    else
                    {
                        score += 10;
                    }
                }

                score += 1;
            }
        }

        score.ShouldBe(120);
    }
}
