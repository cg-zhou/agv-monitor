using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using Shouldly;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class AgvTrajectoryParserTests
{
    private string CreateTempCsvFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    [Fact]
    public void ParseTrajectory_ValidCsv_ShouldReturnCorrectRecords()
    {
        // Arrange
        var csvContent = @"timestamp,name,X,Y,pitch,loaded,destination,Emergency
0,AGV01,3,1,90,false,,false
1,AGV01,3,2,90,false,,false
2,AGV02,6,1,90,true,EP01,false";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var records = TrajectoryRecordParser.Parse(tempFile);

            // Assert
            records.ShouldNotBeNull();
            records.Count.ShouldBe(3);

            var firstRecord = records.First();
            firstRecord.Timestamp.ShouldBe(0);
            firstRecord.Name.ShouldBe("AGV01");
            firstRecord.X.ShouldBe(3);
            firstRecord.Y.ShouldBe(1);
            firstRecord.Pitch.ShouldBe(Direction.Up);
            firstRecord.Loaded.ShouldBeFalse();
            firstRecord.Destination.ShouldBeEmpty();
            firstRecord.Emergency.ShouldBeFalse();

            var lastRecord = records.Last();
            lastRecord.Name.ShouldBe("AGV02");
            lastRecord.Loaded.ShouldBeTrue();
            lastRecord.Destination.ShouldBe("EP01");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTrajectory_EmptyFile_ShouldReturnEmptyList()
    {
        // Arrange
        var csvContent = "timestamp,name,X,Y,pitch,loaded,destination,Emergency";
        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var records = TrajectoryRecordParser.Parse(tempFile);

            // Assert
            records.ShouldNotBeNull();
            records.Count.ShouldBe(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTrajectory_MissingFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_file.csv";

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => TrajectoryRecordParser.Parse(nonExistentFile));
    }

    [Fact]
    public void ParseTrajectory_InvalidFormat_ShouldHandleGracefully()
    {
        // Arrange
        var csvContent = @"timestamp,name,X,Y,pitch,loaded,destination,Emergency
invalid,AGV01,abc,def,90,false,,false";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act & Assert
            Should.Throw<InvalidDataException>(() => TrajectoryRecordParser.Parse(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTrajectory_BooleanValues_ShouldParseCorrectly()
    {
        // Arrange
        var csvContent = @"timestamp,name,X,Y,pitch,loaded,destination,Emergency
0,AGV01,3,1,90,True,EP01,False
1,AGV02,6,1,90,false,EP02,true";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var records = TrajectoryRecordParser.Parse(tempFile);

            // Assert
            records.Count.ShouldBe(2);
            records[0].Loaded.ShouldBeTrue();
            records[0].Emergency.ShouldBeFalse();
            records[1].Loaded.ShouldBeFalse();
            records[1].Emergency.ShouldBeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseTrajectory_LargeDataset_ShouldPerformEfficiently()
    {
        // Arrange
        var lines = new List<string> { "timestamp,name,X,Y,pitch,loaded,destination,Emergency" };
        for (int i = 0; i < 1000; i++)
        {
            lines.Add($"{i},AGV{i % 10},{i % 20 + 1},{i % 20 + 1},90,false,,false");
        }
        var csvContent = string.Join("\n", lines);

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var records = TrajectoryRecordParser.Parse(tempFile);
            stopwatch.Stop();

            // Assert
            records.Count.ShouldBe(1000);
            stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000); // Should complete within 1 second
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
