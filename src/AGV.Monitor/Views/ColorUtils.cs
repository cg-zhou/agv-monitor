using System;
using System.Windows.Media;

namespace AGV.Monitor.Views;

public static class ColorUtils
{
    /// <summary>
    /// 根据AGV在列表中的索引生成唯一颜色（均匀分布色相）
    /// </summary>
    public static Color GenerateColorFromIndex(int index, int totalCount)
    {
        // 确保总数至少为1，避免除零错误
        totalCount = Math.Max(totalCount, 1);

        // 计算色相，均匀分布在360度色环上
        double hue = 360.0 * index / totalCount % 360;

        // 固定饱和度和亮度，只改变色相
        double saturation = 0.8; // 较高饱和度，颜色鲜艳
        double value = 0.9;      // 较高亮度，避免太暗

        var color = HsvToRgb(hue, saturation, value);
        color.A = 128;
        return color;
    }

    /// <summary>
    /// HSV 转 RGB 颜色空间转换
    /// </summary>
    public static Color HsvToRgb(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = v - c;

        double r, g, b;
        if (h >= 0 && h < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (h >= 60 && h < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (h >= 120 && h < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (h >= 180 && h < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (h >= 240 && h < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }
}
