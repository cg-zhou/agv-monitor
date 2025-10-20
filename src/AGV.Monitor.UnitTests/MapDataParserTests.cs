using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using Shouldly;
using System.IO;
using System.Linq;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class MapDataParserTests
{
    private string CreateTempCsvFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    [Fact]
    public void ParseMapData_ValidCsv_ShouldReturnCorrectElements()
    {
        // Arrange
        var csvContent = @"type,name,x,y,pitch
StartPoint,SP01,1,6,
EndPoint,EP01,6,4,
Agv,AGV01,3,1,90";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var elements = MapElementParser.Parse(tempFile);

            // Assert
            elements.ShouldNotBeNull();
            elements.Count.ShouldBe(3);

            var startPoint = elements.First(e => e.Type == MapElementType.StartPoint);
            startPoint.Name.ShouldBe("SP01");
            startPoint.X.ShouldBe(1);
            startPoint.Y.ShouldBe(6);
            startPoint.Pitch.ShouldBeNull();

            var endPoint = elements.First(e => e.Type == MapElementType.EndPoint);
            endPoint.Name.ShouldBe("EP01");
            endPoint.X.ShouldBe(6);
            endPoint.Y.ShouldBe(4);

            var agv = elements.First(e => e.Type == MapElementType.Agv);
            agv.Name.ShouldBe("AGV01");
            agv.X.ShouldBe(3);
            agv.Y.ShouldBe(1);
            agv.Pitch.ShouldBe(Direction.Up);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseMapData_EmptyFile_ShouldReturnEmptyList()
    {
        // Arrange
        var csvContent = "type,name,x,y,pitch";
        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var elements = MapElementParser.Parse(tempFile);

            // Assert
            elements.ShouldNotBeNull();
            elements.Count.ShouldBe(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseMapData_InvalidElementType_ShouldThrowArgumentException()
    {
        // Arrange
        var csvContent = @"type,name,x,y,pitch
invalid_type,InvalidElement,1,1,90";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act & Assert
            Should.Throw<InvalidDataException>(() => MapElementParser.Parse(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseMapData_OptionalPitchField_ShouldHandleCorrectly()
    {
        // Arrange
        var csvContent = @"type,name,x,y,pitch
StartPoint,SP01,1,6,
Agv,AGV01,3,1,90
EndPoint,EP01,6,4,0";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var elements = MapElementParser.Parse(tempFile);

            // Assert
            elements.Count.ShouldBe(3);

            var startPoint = elements.First(e => e.Name == "SP01");
            startPoint.Pitch.ShouldBeNull();

            var agv = elements.First(e => e.Name == "AGV01");
            agv.Pitch.ShouldBe(Direction.Up);

            var endPoint = elements.First(e => e.Name == "EP01");
            endPoint.Pitch.ShouldBe(Direction.Right);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseMapData_CoordinateValidation_ShouldParseValidCoordinates()
    {
        // Arrange
        var csvContent = @"type,name,x,y,pitch
StartPoint,SP01,1,20,
StartPoint,SP04,20,1,
Agv,AGV01,10,10,90";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var elements = MapElementParser.Parse(tempFile);

            // Assert
            elements.Count.ShouldBe(3);
            elements.All(e => e.X >= 1 && e.X <= 20).ShouldBeTrue();
            elements.All(e => e.Y >= 1 && e.Y <= 20).ShouldBeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ParseMapData_MissingFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentFile = "non_existent_map.csv";

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => MapElementParser.Parse(nonExistentFile));
    }

    [Fact]
    public void ParseMapData_MixedElementTypes_ShouldParseAllCorrectly()
    {
        // Arrange
        var csvContent = @"type,name,x,y,pitch
StartPoint,SP01,1,6,
StartPoint,SP02,1,10,
EndPoint,EP01,6,4,
EndPoint,EP02,6,8,
Agv,AGV01,3,1,90
Agv,AGV02,6,1,90";

        var tempFile = CreateTempCsvFile(csvContent);

        try
        {
            // Act
            var elements = MapElementParser.Parse(tempFile);

            // Assert
            elements.Count.ShouldBe(6);
            elements.Count(e => e.Type == MapElementType.StartPoint).ShouldBe(2);
            elements.Count(e => e.Type == MapElementType.EndPoint).ShouldBe(2);
            elements.Count(e => e.Type == MapElementType.Agv).ShouldBe(2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
