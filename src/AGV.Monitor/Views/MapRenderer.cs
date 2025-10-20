using AGV.Monitor.Geometry;
using AGV.Monitor.Parsers;
using AGV.Monitor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AGV.Monitor.Views;

/// <summary>
/// 地图渲染器，负责绘制地图元素和轨迹
/// </summary>
public class MapRenderer
{
    private readonly Canvas mapCanvas;
    private const double GridSize = 40; // 网格大小
    private const double ElementSize = 30; // 元素大小

    private AgvContext context;

    public MapRenderer(Canvas canvas, AgvContext context)
    {
        mapCanvas = canvas;
        this.context = context;
    }

    /// <summary>
    /// 绘制完整地图
    /// </summary>
    public void DrawMap()
    {
        DrawMapInternal(null);
    }

    /// <summary>
    /// 绘制带热点图的地图
    /// </summary>
    /// <param name="heatmapData">热点图数据</param>
    public void DrawMapWithHeatmap(HeatmapData heatmapData)
    {
        DrawMapInternal(heatmapData);
    }

    /// <summary>
    /// 内部地图绘制方法
    /// </summary>
    /// <param name="heatmapData">热点图数据，如果为null则不绘制热点图</param>
    private void DrawMapInternal(HeatmapData heatmapData)
    {
        var mapElements = context.MapElements;
        var bounds = context.MapBounds;

        if (mapCanvas == null || !mapElements.Any())
        {
            return;
        }

        mapCanvas.Children.Clear();

        var width = (bounds.Right - bounds.Left + 1) * GridSize;
        var height = (bounds.Top - bounds.Bottom + 1) * GridSize;

        // 绘制网格
        DrawGrid(bounds, width, height);

        // 绘制热点图（在地图元素之前绘制，作为背景）
        if (heatmapData != null)
        {
            DrawHeatmap(bounds, heatmapData);
        }

        // 绘制地图元素
        DrawMapElements(mapElements, bounds);
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGrid(RectEx bounds, double width, double height)
    {
        var gridBrush = new SolidColorBrush(Colors.Gray);
        var axisBrush = new SolidColorBrush(Colors.Black);
        var axisMargin = 30; // 坐标轴标签的边距
        var bottomMargin = 50; // 底部额外边距，用于显示AGV名称

        // 设置画布大小以包含坐标轴标签和底部边距
        mapCanvas.Width = width + axisMargin;
        mapCanvas.Height = height + axisMargin + bottomMargin;

        // 绘制垂直网格线
        for (var x = bounds.Left; x <= bounds.Right; x++)
        {
            var xPos = (x - bounds.Left + 1) * GridSize + axisMargin;
            var line = new Line
            {
                X1 = xPos,
                Y1 = axisMargin,
                X2 = xPos,
                Y2 = height + axisMargin,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            mapCanvas.Children.Add(line);
        }

        // 绘制水平网格线
        for (var y = bounds.Bottom; y <= bounds.Top; y++)
        {
            var yPos = (bounds.Top - y + 1) * GridSize + axisMargin;
            var line = new Line
            {
                X1 = axisMargin,
                Y1 = yPos,
                X2 = width + axisMargin,
                Y2 = yPos,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            mapCanvas.Children.Add(line);
        }

        // 添加X轴坐标标签（显示在底部）
        for (var x = bounds.Left; x <= bounds.Right; x++)
        {
            var xPos = (x - bounds.Left + 0.5) * GridSize + axisMargin;
            var xLabel = new TextBlock
            {
                Text = x.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = axisBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(xLabel, xPos - 8);
            Canvas.SetTop(xLabel, height + axisMargin + 5); // 放在底部
            mapCanvas.Children.Add(xLabel);
        }

        // 添加Y轴坐标标签（显示在左侧）
        for (var y = bounds.Bottom; y <= bounds.Top; y++)
        {
            var yPos = (bounds.Top - y + 0.5) * GridSize + axisMargin;
            var yLabel = new TextBlock
            {
                Text = y.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = axisBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Canvas.SetLeft(yLabel, 5);
            Canvas.SetTop(yLabel, yPos - 8);
            mapCanvas.Children.Add(yLabel);
        }

        // 绘制坐标轴边框
        var border = new Rectangle
        {
            Width = width,
            Height = height,
            Stroke = axisBrush,
            StrokeThickness = 2,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(border, axisMargin);
        Canvas.SetTop(border, axisMargin);
        mapCanvas.Children.Add(border);
    }

    /// <summary>
    /// 绘制地图元素
    /// </summary>
    private void DrawMapElements(List<MapElement> mapElements, RectEx bounds)
    {
        var axisMargin = 30; // 坐标轴标签的边距

        foreach (var element in mapElements)
        {
            // 计算元素在格子中心的位置
            var x = (element.X - bounds.Left + 0.5) * GridSize + axisMargin;
            var y = (bounds.Top - element.Y + 0.5) * GridSize + axisMargin;

            var shape = CreateElementShape(element);
            Canvas.SetLeft(shape, x - ElementSize / 2);
            Canvas.SetTop(shape, y - ElementSize / 2);

            mapCanvas.Children.Add(shape);

            // 如果是 AGV 且有朝向信息，绘制朝向指示器
            if (element.Type == MapElementType.Agv && element.Pitch.HasValue)
            {
                var directionLine = CreateDirectionIndicator(x, y, element.Pitch.Value, element.Name);
                mapCanvas.Children.Add(directionLine);
            }

            // 添加标签（居中对齐）
            var label = new TextBlock
            {
                Text = element.Name,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            // 测量文本宽度以实现真正的居中
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = label.DesiredSize.Width;

            Canvas.SetLeft(label, x - textWidth / 2);
            Canvas.SetTop(label, y + ElementSize / 2 + 2);
            mapCanvas.Children.Add(label);
        }
    }

    /// <summary>
    /// 绘制热点图
    /// </summary>
    /// <param name="bounds">地图边界</param>
    /// <param name="heatmapData">热点图数据</param>
    private void DrawHeatmap(RectEx bounds, HeatmapData heatmapData)
    {
        if (heatmapData == null || heatmapData.MaxCount == 0)
            return;

        var axisMargin = 30;

        // 为每个有访问记录的位置绘制热点
        foreach (var (x, y, count) in heatmapData.GetAllPositions())
        {
            // 检查位置是否在地图边界内
            if (x < bounds.Left || x > bounds.Right || y < bounds.Bottom || y > bounds.Top)
                continue;

            var heatRatio = heatmapData.GetHeatRatio(x, y);
            if (heatRatio <= 0) continue;

            // 计算热点在画布上的位置
            var canvasX = (x - bounds.Left + 0.5) * GridSize + axisMargin;
            var canvasY = (bounds.Top - y + 0.5) * GridSize + axisMargin;

            // 创建热点可视化元素
            var heatRect = CreateHeatmapElement(canvasX, canvasY, heatRatio, count);
            mapCanvas.Children.Add(heatRect);
        }
    }

    /// <summary>
    /// 创建热点图元素
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="heatRatio">热度比例 (0.0 - 1.0)</param>
    /// <param name="count">访问次数</param>
    /// <returns>热点图UI元素</returns>
    private UIElement CreateHeatmapElement(double x, double y, double heatRatio, int count)
    {
        // 使用颜色渐变表示热度：蓝色(冷) -> 绿色 -> 黄色 -> 红色(热)
        var color = GetHeatmapColor(heatRatio);

        // 透明度根据热度调整，最低30%，最高80%
        var alpha = (byte)(77 + heatRatio * 127); // 30% + 50% * heatRatio
        color.A = alpha;

        // 创建矩形作为热点显示
        var heatRect = new Rectangle
        {
            Width = GridSize * 0.8, // 稍小于网格大小，避免覆盖网格线
            Height = GridSize * 0.8,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B)),
            StrokeThickness = 1,
            ToolTip = $"访问次数: {count}\n热度: {heatRatio:P1}"
        };

        Canvas.SetLeft(heatRect, x - heatRect.Width / 2);
        Canvas.SetTop(heatRect, y - heatRect.Height / 2);

        return heatRect;
    }

    /// <summary>
    /// 根据热度比例获取颜色
    /// </summary>
    /// <param name="heatRatio">热度比例 (0.0 - 1.0)</param>
    /// <returns>对应的颜色</returns>
    private Color GetHeatmapColor(double heatRatio)
    {
        // 绿色→黄色→红色的平滑渐变
        // heatRatio: 0.0 -> 绿色, 0.5 -> 黄色, 1.0 -> 红色

        if (heatRatio <= 0.0) return Colors.Green;
        if (heatRatio >= 1.0) return Colors.Red;

        byte r, g, b;

        if (heatRatio < 0.5)
        {
            // 从绿色到黄色的渐变 (0.0 - 0.5)
            var t = heatRatio * 2; // 归一化到 0.0 - 1.0
            r = (byte)(255 * t);     // 红色分量逐渐增加
            g = 255;                 // 绿色分量保持最大
            b = 0;                   // 蓝色分量保持为0
        }
        else
        {
            // 从黄色到红色的渐变 (0.5 - 1.0)
            var t = (heatRatio - 0.5) * 2; // 归一化到 0.0 - 1.0
            r = 255;                        // 红色分量保持最大
            g = (byte)(255 * (1 - t));      // 绿色分量逐渐减少
            b = 0;                          // 蓝色分量保持为0
        }

        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// 绘制带时间信息的路径
    /// </summary>
    public List<UIElement> DrawPathWithTiming(Agv agv)
    {
        var pathElements = new List<UIElement>();
        if (agv.PathTimePoints.Count < 2)
        {
            return pathElements;
        }

        var bounds = context.MapBounds;

        var axisMargin = 30;
        var agvColor = GenerateColorFromName(agv.Name);
        var pathBrush = new SolidColorBrush(agvColor);
        var timeBrush = new SolidColorBrush(Color.FromRgb(
            (byte)(agvColor.R * 0.7),
            (byte)(agvColor.G * 0.7),
            (byte)(agvColor.B * 0.7))); // 稍微暗一点的颜色用于文本

        // 绘制路径线段
        for (int i = 0; i < agv.PathTimePoints.Count - 1; i++)
        {
            var from = agv.PathTimePoints[i];
            var to = agv.PathTimePoints[i + 1];

            // 计算屏幕坐标
            var fromScreen = GridToScreen(from.Position.X, from.Position.Y, axisMargin, bounds);
            var toScreen = GridToScreen(to.Position.X, to.Position.Y, axisMargin, bounds);

            var line = new Line
            {
                X1 = fromScreen.X,
                Y1 = fromScreen.Y,
                X2 = toScreen.X,
                Y2 = toScreen.Y,
                Stroke = pathBrush,
                StrokeThickness = 3,
                StrokeEndLineCap = PenLineCap.Round
            };

            pathElements.Add(line);
            mapCanvas.Children.Add(line);
        }

        // 在每个路径点显示时间标签
        foreach (var point in agv.PathTimePoints)
        {
            var screen = GridToScreen(point.Position.X, point.Position.Y, axisMargin, bounds);

            // 创建时间标签
            var timeLabel = new TextBlock
            {
                Text = $"{point.TimeCost}s",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = timeBrush,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Padding = new Thickness(2)
            };

            // 将标签放置在点的右上角，向上调整避免遮住轨迹线
            Canvas.SetLeft(timeLabel, screen.X + 5);
            Canvas.SetTop(timeLabel, screen.Y - 22); // 从-15改为-22，向上移动7像素
            mapCanvas.Children.Add(timeLabel);
            pathElements.Add(timeLabel);

            // 在关键点添加圆点标记
            var marker = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = pathBrush,
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };

            Canvas.SetLeft(marker, screen.X - 3);
            Canvas.SetTop(marker, screen.Y - 3);
            mapCanvas.Children.Add(marker);
            pathElements.Add(marker);
        }

        // 在路径终点添加特殊标记
        if (agv.PathTimePoints.Any())
        {
            var endPoint = agv.PathTimePoints.Last();
            var endScreen = GridToScreen(endPoint.Position.X, endPoint.Position.Y, axisMargin, bounds);

            var endMarker = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.Red),
                Stroke = new SolidColorBrush(Colors.DarkRed),
                StrokeThickness = 2
            };

            Canvas.SetLeft(endMarker, endScreen.X - 5);
            Canvas.SetTop(endMarker, endScreen.Y - 5);
            mapCanvas.Children.Add(endMarker);
            pathElements.Add(endMarker);
        }

        return pathElements;
    }

    /// <summary>
    /// 移除指定的UI元素
    /// </summary>
    public void RemoveElements(IEnumerable<UIElement> elements)
    {
        foreach (var element in elements)
        {
            mapCanvas.Children.Remove(element);
        }
    }

    /// <summary>
    /// 创建鼠标悬浮效果
    /// </summary>
    public Rectangle CreateHoverEffect(int gridX, int gridY, RectEx bounds)
    {
        var axisMargin = 30;
        var x = (gridX - bounds.Left) * GridSize + axisMargin;
        var y = (bounds.Top - gridY) * GridSize + axisMargin;

        var hoverRect = new Rectangle
        {
            Width = GridSize,
            Height = GridSize,
            Fill = new SolidColorBrush(Color.FromArgb(50, 0, 0, 255)), // 半透明蓝色
            Stroke = Brushes.Transparent
        };
        Canvas.SetLeft(hoverRect, x);
        Canvas.SetTop(hoverRect, y);

        mapCanvas.Children.Insert(0, hoverRect); // 插入到最底层
        return hoverRect;
    }

    /// <summary>
    /// 根据元素类型创建形状
    /// </summary>
    private Shape CreateElementShape(MapElement element)
    {
        Shape shape;

        switch (element.Type)
        {
            case MapElementType.StartPoint:
                shape = new Rectangle
                {
                    Width = ElementSize,
                    Height = ElementSize,
                    Fill = new SolidColorBrush(Color.FromRgb(144, 238, 144)), // 浅绿色
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 128, 0)), // 深绿色边框
                    StrokeThickness = 2
                };
                break;

            case MapElementType.EndPoint:
                shape = new Rectangle
                {
                    Width = ElementSize,
                    Height = ElementSize,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 182, 193)), // 浅红色
                    Stroke = new SolidColorBrush(Color.FromRgb(220, 20, 60)), // 深红色边框
                    StrokeThickness = 2
                };
                break;

            case MapElementType.Agv:
                var isLoaded = context.Agvs
                    .FirstOrDefault(x => x.Name == element.Name)?.IsLoaded ?? false;
                var strokeThickness = isLoaded ? 5 : 1;
                var agvColor = GenerateColorFromName(element.Name);
                shape = new Ellipse
                {
                    Width = ElementSize,
                    Height = ElementSize,
                    Fill = new SolidColorBrush(agvColor),
                    Stroke = new SolidColorBrush(Color.FromRgb(
                        (byte)(agvColor.R * 0.6),
                        (byte)(agvColor.G * 0.6),
                        (byte)(agvColor.B * 0.6))), // 更深的边框颜色
                    StrokeThickness = strokeThickness
                };
                break;

            default:
                shape = new Rectangle
                {
                    Width = ElementSize,
                    Height = ElementSize,
                    Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // 浅灰色
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };
                break;
        }

        // 添加工具提示
        shape.ToolTip = $"类型: {element.Type}\n名称: {element.Name}\n坐标: ({element.X}, {element.Y})";
        if (element.Pitch.HasValue)
            shape.ToolTip += $"\n方向: {element.Pitch}°";

        return shape;
    }

    /// <summary>
    /// 创建AGV朝向指示器
    /// </summary>
    private Line CreateDirectionIndicator(double centerX, double centerY, Direction pitch, string agvName)
    {
        double endX = centerX;
        double endY = centerY;
        double length = ElementSize / 2 + 8; // 指示器长度

        // 根据pitch计算朝向终点（注意Y轴翻转）
        double radian = Math.PI * (int)pitch / 180.0;
        endX = centerX + length * Math.Cos(radian);
        endY = centerY - length * Math.Sin(radian); // Y轴向上为负

        // 使用AGV的特定颜色，稍微暗一点作为朝向指示器
        var agvColor = GenerateColorFromName(agvName);
        var indicatorColor = Color.FromRgb(
            (byte)(agvColor.R * 0.8),
            (byte)(agvColor.G * 0.8),
            (byte)(agvColor.B * 0.8));

        return new Line
        {
            X1 = centerX,
            Y1 = centerY,
            X2 = endX,
            Y2 = endY,
            Stroke = new SolidColorBrush(indicatorColor),
            StrokeThickness = 3,
            StrokeEndLineCap = PenLineCap.Triangle
        };
    }

    /// <summary>
    /// 将网格坐标转换为屏幕坐标
    /// </summary>
    private (double X, double Y) GridToScreen(int gridX, int gridY, double axisMargin, RectEx bounds)
    {
        var x = (gridX - bounds.Left + 0.5) * GridSize + axisMargin;
        var y = (bounds.Top - gridY + 0.5) * GridSize + axisMargin;
        return (x, y);
    }

    /// <summary>
    /// 根据AGV名称获取其在AGV列表中的索引
    /// </summary>
    private int GetAgvIndex(string agvName)
    {
        for (int i = 0; i < context.Agvs.Length; i++)
        {
            if (context.Agvs[i].Name == agvName)
            {
                return i;
            }
        }
        return 0; // 如果没找到，返回默认索引
    }

    /// <summary>
    /// 根据AGV名称生成唯一颜色（通过索引分布色相）
    /// </summary>
    private Color GenerateColorFromName(string name)
    {
        int index = GetAgvIndex(name);
        return ColorUtils.GenerateColorFromIndex(index, context.Agvs.Length);
    }
}
