using AGV.Monitor.Parsers;
using AGV.Monitor.Services;
using AGV.Monitor.Views;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;

namespace Viewer;

public partial class MainWindow : Window
{
    private const double GridSize = 40; // 网格大小

    // 组件
    private MapRenderer mapRenderer;
    private AgvContext context;
    private HeatmapData heatmapData;

    // 状态
    private Rectangle currentHoverRect;
    private bool isHeatmapVisible = false;
    private bool isTrajectoryVisible = false;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// 窗口加载完成事件
    /// </summary>
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadData();
        InitializeComponents();
        DrawMap();
    }

    /// <summary>
    /// 初始化组件
    /// </summary>
    private void InitializeComponents()
    {
        mapRenderer = new MapRenderer(mapCanvas, context);
        heatmapData = new HeatmapData();
    }

    /// <summary>
    /// 从嵌入资源加载数据
    /// </summary>
    private void LoadData()
    {
        try
        {
            if (statusText != null)
            {
                statusText.Text = "正在加载数据...";
            }

            context = AgvContext.Create();

            if (infoText != null)
            {
                infoText.Text = $"地图元素: {context.MapElements.Count}, 任务: {context.TaskRecords.Count}";
            }

            if (statusText != null)
            {
                statusText.Text = "数据加载完成";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);

            if (statusText != null)
            {
                statusText.Text = "数据加载失败";
            }
        }
    }

    /// <summary>
    /// 绘制地图
    /// </summary>
    private void DrawMap()
    {
        if (mapCanvas == null || mapRenderer == null)
            return;

        if (!context.MapElements.Any())
            return;

        // 使用渲染器绘制地图
        if (isHeatmapVisible && heatmapData != null)
        {
            mapRenderer.DrawMapWithHeatmap(heatmapData);
        }
        else
        {
            mapRenderer.DrawMap();
        }

        // 设置鼠标事件
        mapCanvas.MouseMove += MapCanvas_MouseMove;
        mapCanvas.MouseLeave += MapCanvas_MouseLeave;

        if (statusText != null)
        {
            statusText.Text = "地图绘制完成";
        }
    }

    /// <summary>
    /// 鼠标在地图上移动事件
    /// </summary>
    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (mapRenderer == null)
        {
            return;
        }

        var mapBounds = context.MapBounds;
        var axisMargin = 30;
        var position = e.GetPosition(mapCanvas);

        // 计算鼠标所在的格子坐标
        var gridX = (int)Math.Floor((position.X - axisMargin) / GridSize) + mapBounds.Left;
        var gridY = mapBounds.Top - (int)Math.Floor((position.Y - axisMargin) / GridSize);

        // 检查是否在有效范围内
        if (gridX >= mapBounds.Left && gridX <= mapBounds.Right &&
            gridY >= mapBounds.Bottom && gridY <= mapBounds.Top)
        {
            // 移除之前的悬浮效果
            if (currentHoverRect != null)
            {
                mapCanvas.Children.Remove(currentHoverRect);
            }

            // 创建新的悬浮背景
            currentHoverRect = mapRenderer.CreateHoverEffect(gridX, gridY, mapBounds);

            // 在状态栏显示坐标
            if (statusText != null)
            {
                statusText.Text = $"鼠标位置: ({gridX}, {gridY})";
            }
        }
        else
        {
            MapCanvas_MouseLeave(sender, e);
        }
    }

    /// <summary>
    /// 鼠标离开地图事件
    /// </summary>
    private void MapCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (currentHoverRect != null)
        {
            mapCanvas.Children.Remove(currentHoverRect);
            currentHoverRect = null;
        }
        if (statusText != null)
        {
            statusText.Text = "地图绘制完成";
        }
    }

    /// <summary>
    /// 下一帧按钮点击事件
    /// </summary>
    private void NextSecondButton_Click(object sender, RoutedEventArgs e)
    {
        ProcessNextSecond();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetAll();
    }

    private void ResetAll()
    {
        statusText.Text = "正在重置系统...";

        context = AgvContext.Create();

        // 清空画布
        if (mapCanvas != null)
        {
            mapCanvas.Children.Clear();
        }

        // 重置状态变量
        currentHoverRect = null;

        // 重新加载数据
        LoadData();

        // 重新初始化组件
        InitializeComponents();

        // 重新绘制地图
        DrawMap();

        if (statusText != null)
        {
            statusText.Text = "系统重置完成";
        }
    }

    /// <summary>
    /// 处理下一帧逻辑
    /// </summary>
    private void ProcessNextSecond()
    {
        if (mapRenderer == null)
        {
            return;
        }

        context.Scheduler.Process();

        // 使用 AGV 当前的位置朝向信息，同步地图元素
        var agvs = context.Agvs;
        foreach (var mapElement in context.MapElements.Where(x => x.Type == MapElementType.Agv))
        {
            var agv = agvs.FirstOrDefault(x => x.Name == mapElement.Name);
            if (agv != null)
            {
                mapElement.X = agv.Position.X;
                mapElement.Y = agv.Position.Y;
                mapElement.Pitch = agv.Pitch;
            }
        }

        var completedTaskCount = context.GetCompletedTasks().Length;
        completedTaskText.Text = $"完成任务数：{completedTaskCount}";

        secondText.Text = $"秒数：{context.Scheduler.Timestamp}";

        // 如果显示热点图，自动刷新热点图数据
        if (isHeatmapVisible)
        {
            RefreshHeatmapData();
        }

        DrawMap();
        
        // 绘制轨迹（如果开启了轨迹显示）
        if (isTrajectoryVisible)
        {
            foreach (var agv in agvs)
            {
                mapRenderer.DrawPathWithTiming(agv);
            }
        }
    }

    /// <summary>
    /// 键盘按键事件
    /// </summary>
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            ProcessNextSecond();
        }
        else if (e.Key == Key.Escape)
        {
            ResetAll();
        }
        else if (e.Key == Key.F1)
        {
            ToggleHeatmap();
        }
        else if (e.Key == Key.F2)
        {
            ToggleTrajectory();
        }
    }

    /// <summary>
    /// 热点图开关选中事件
    /// </summary>
    private void HeatmapToggle_Checked(object sender, RoutedEventArgs e)
    {
        ToggleHeatmap();
    }

    /// <summary>
    /// 热点图开关取消选中事件
    /// </summary>
    private void HeatmapToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        ToggleHeatmap();
    }

    private void ToggleHeatmap()
    {
        isHeatmapVisible = !isHeatmapVisible;
        heatmapToggle.Content = isHeatmapVisible ? "隐藏热点图 (F1)" : "显示热点图 (F1)";
        if (isHeatmapVisible)
        {
            RefreshHeatmapData();
        }
        RedrawMap();
    }

    /// <summary>
    /// 刷新热点图数据
    /// </summary>
    private void RefreshHeatmapData()
    {
        if (context?.TrajectoryRecorder != null)
        {
            var trajectoryRecords = context.TrajectoryRecorder.GetRecords();
            heatmapData.BuildFromTrajectoryRecords(trajectoryRecords);

            // 更新状态栏显示热点图统计信息
            var stats = heatmapData.GetStatistics();
            statusText.Text = stats;
        }
    }

    /// <summary>
    /// 重新绘制地图
    /// </summary>
    private void RedrawMap()
    {
        DrawMap();
        
        // 绘制轨迹（如果开启了轨迹显示）
        if (isTrajectoryVisible)
        {
            var agvs = context.Agvs;
            foreach (var agv in agvs)
            {
                mapRenderer.DrawPathWithTiming(agv);
            }
        }
    }

    /// <summary>
    /// 轨迹开关选中事件
    /// </summary>
    private void TrajectoryToggle_Checked(object sender, RoutedEventArgs e)
    {
        ToggleTrajectory();
    }

    /// <summary>
    /// 轨迹开关取消选中事件
    /// </summary>
    private void TrajectoryToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        ToggleTrajectory();
    }

    /// <summary>
    /// 切换轨迹显示状态
    /// </summary>
    private void ToggleTrajectory()
    {
        isTrajectoryVisible = !isTrajectoryVisible;
        trajectoryToggle.Content = isTrajectoryVisible ? "隐藏轨迹 (F2)" : "显示轨迹 (F2)";
        RedrawMap();
    }
}
