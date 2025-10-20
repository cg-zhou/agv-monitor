using AGV.Monitor.Views;
using Shouldly;
using System.Windows.Media;
using Xunit;

namespace AGV.Monitor.UnitTests;

public class ColorUtilsTests
{
    [Fact]
    public void GenerateColorFromIndex_WithValidParameters_ShouldGenerateUniqueColors()
    {
        // Arrange
        const int totalCount = 5;

        // Act
        var color1 = ColorUtils.GenerateColorFromIndex(0, totalCount);
        var color2 = ColorUtils.GenerateColorFromIndex(1, totalCount);
        var color3 = ColorUtils.GenerateColorFromIndex(2, totalCount);

        // Assert
        color1.ShouldNotBe(color2);
        color2.ShouldNotBe(color3);
        color1.ShouldNotBe(color3);

        // Alpha 通道应该固定为 128
        color1.A.ShouldBe((byte)128);
        color2.A.ShouldBe((byte)128);
        color3.A.ShouldBe((byte)128);
    }

    [Fact]
    public void GenerateColorFromIndex_WithZeroTotalCount_ShouldHandleGracefully()
    {
        // Arrange & Act
        var color = ColorUtils.GenerateColorFromIndex(0, 0);

        // Assert
        color.A.ShouldBe((byte)128);
    }

    [Fact]
    public void GenerateColorFromIndex_WithNegativeTotalCount_ShouldHandleGracefully()
    {
        // Arrange & Act
        var color = ColorUtils.GenerateColorFromIndex(0, -5);

        // Assert
        color.A.ShouldBe((byte)128);
    }

    [Fact]
    public void GenerateColorFromIndex_WithSameIndexAndTotalCount_ShouldGenerateSameColor()
    {
        // Arrange
        const int index = 2;
        const int totalCount = 10;

        // Act
        var color1 = ColorUtils.GenerateColorFromIndex(index, totalCount);
        var color2 = ColorUtils.GenerateColorFromIndex(index, totalCount);

        // Assert
        color1.ShouldBe(color2);
    }

    [Fact]
    public void GenerateColorFromIndex_WithLargeIndex_ShouldHandleModuloCorrectly()
    {
        // Arrange
        const int index = 15;
        const int totalCount = 5;

        // Act & Assert - 应该不抛出异常
        var color = ColorUtils.GenerateColorFromIndex(index, totalCount);
        color.A.ShouldBe((byte)128);
    }

    [Theory]
    [InlineData(0, 1.0, 1.0, 255, 0, 0)]     // 纯红色
    [InlineData(0, 0.0, 1.0, 255, 255, 255)] // 无饱和度的白色
    [InlineData(0, 1.0, 0.0, 0, 0, 0)]       // 无亮度的黑色
    [InlineData(120, 1.0, 1.0, 0, 255, 0)]   // 纯绿色
    [InlineData(240, 1.0, 1.0, 0, 0, 255)]   // 纯蓝色
    public void HsvToRgb_WithKnownValues_ShouldReturnExpectedRgb(double h, double s, double v, byte expectedR, byte expectedG, byte expectedB)
    {
        // Act
        var color = ColorUtils.HsvToRgb(h, s, v);

        // Assert
        color.R.ShouldBe(expectedR);
        color.G.ShouldBe(expectedG);
        color.B.ShouldBe(expectedB);
        color.A.ShouldBe((byte)255); // Alpha 应该默认为 255
    }

    [Fact]
    public void HsvToRgb_WithRedHue_ShouldReturnRedColor()
    {
        // Arrange
        const double hue = 0; // 红色
        const double saturation = 1.0;
        const double value = 1.0;

        // Act
        var color = ColorUtils.HsvToRgb(hue, saturation, value);

        // Assert
        color.R.ShouldBe((byte)255);
        color.G.ShouldBe((byte)0);
        color.B.ShouldBe((byte)0);
    }

    [Fact]
    public void HsvToRgb_WithYellowHue_ShouldReturnYellowColor()
    {
        // Arrange
        const double hue = 60; // 黄色
        const double saturation = 1.0;
        const double value = 1.0;

        // Act
        var color = ColorUtils.HsvToRgb(hue, saturation, value);

        // Assert
        color.R.ShouldBe((byte)255);
        color.G.ShouldBe((byte)255);
        color.B.ShouldBe((byte)0);
    }

    [Fact]
    public void HsvToRgb_WithCyanHue_ShouldReturnCyanColor()
    {
        // Arrange
        const double hue = 180; // 青色
        const double saturation = 1.0;
        const double value = 1.0;

        // Act
        var color = ColorUtils.HsvToRgb(hue, saturation, value);

        // Assert
        color.R.ShouldBe((byte)0);
        color.G.ShouldBe((byte)255);
        color.B.ShouldBe((byte)255);
    }

    [Fact]
    public void HsvToRgb_WithMagentaHue_ShouldReturnMagentaColor()
    {
        // Arrange
        const double hue = 300; // 洋红色
        const double saturation = 1.0;
        const double value = 1.0;

        // Act
        var color = ColorUtils.HsvToRgb(hue, saturation, value);

        // Assert
        color.R.ShouldBe((byte)255);
        color.G.ShouldBe((byte)0);
        color.B.ShouldBe((byte)255);
    }

    [Theory]
    [InlineData(0, 0.5, 0.5)]
    [InlineData(60, 0.8, 0.9)]
    [InlineData(120, 0.3, 0.7)]
    [InlineData(180, 0.6, 0.4)]
    [InlineData(240, 0.9, 0.8)]
    [InlineData(300, 0.4, 0.6)]
    public void HsvToRgb_WithVariousInputs_ShouldProduceValidRgbValues(double h, double s, double v)
    {
        // Act
        var color = ColorUtils.HsvToRgb(h, s, v);

        // Assert
        ((int)color.R).ShouldBeInRange(0, 255);
        ((int)color.G).ShouldBeInRange(0, 255);
        ((int)color.B).ShouldBeInRange(0, 255);
        color.A.ShouldBe((byte)255);
    }

    [Fact]
    public void HsvToRgb_WithHueAt360_ShouldBeEquivalentToHueAt0()
    {
        // Arrange
        const double saturation = 0.8;
        const double value = 0.9;

        // Act
        var colorAt0 = ColorUtils.HsvToRgb(0, saturation, value);
        var colorAt360 = ColorUtils.HsvToRgb(360, saturation, value);

        // Assert
        // 由于色环是循环的，360度应该等于0度
        colorAt0.R.ShouldBe(colorAt360.R);
        colorAt0.G.ShouldBe(colorAt360.G);
        colorAt0.B.ShouldBe(colorAt360.B);
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(390)]
    [InlineData(720)]
    public void HsvToRgb_WithHueOutsideNormalRange_ShouldHandleCorrectly(double hue)
    {
        // Act & Assert - 应该不抛出异常
        var color = ColorUtils.HsvToRgb(hue, 0.8, 0.9);
        ((int)color.R).ShouldBeInRange(0, 255);
        ((int)color.G).ShouldBeInRange(0, 255);
        ((int)color.B).ShouldBeInRange(0, 255);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    public void HsvToRgb_WithBoundarySaturationAndValue_ShouldWork(double saturation, double value)
    {
        // Act
        var color = ColorUtils.HsvToRgb(180, saturation, value);

        // Assert
        ((int)color.R).ShouldBeInRange(0, 255);
        ((int)color.G).ShouldBeInRange(0, 255);
        ((int)color.B).ShouldBeInRange(0, 255);
    }

    [Fact]
    public void GenerateColorFromIndex_DistributionTest_ShouldDistributeHuesEvenly()
    {
        // Arrange
        const int totalCount = 6;
        var colors = new Color[totalCount];

        // Act
        for (int i = 0; i < totalCount; i++)
        {
            colors[i] = ColorUtils.GenerateColorFromIndex(i, totalCount);
        }

        // Assert
        // 验证生成的颜色都不相同
        for (int i = 0; i < totalCount; i++)
        {
            for (int j = i + 1; j < totalCount; j++)
            {
                colors[i].ShouldNotBe(colors[j], $"Color at index {i} should be different from color at index {j}");
            }
        }

        // 验证所有颜色的 alpha 值都是 128
        foreach (var color in colors)
        {
            color.A.ShouldBe((byte)128);
        }
    }

    [Fact]
    public void GenerateColorFromIndex_WithIndexEqualToTotalCount_ShouldWrapAround()
    {
        // Arrange
        const int totalCount = 5;

        // Act
        var color1 = ColorUtils.GenerateColorFromIndex(0, totalCount);
        var color2 = ColorUtils.GenerateColorFromIndex(totalCount, totalCount); // 索引等于总数

        // Assert
        // 由于使用了模运算，索引 totalCount 应该等同于索引 0
        color1.ShouldBe(color2);
    }
}
