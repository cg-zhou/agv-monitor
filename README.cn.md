# AGV Monitor

> [English Documentation](README.md)

[![GitHub license](https://img.shields.io/github/license/cg-zhou/agv-monitor.svg)](https://github.com/cg-zhou/agv-monitor/blob/main/LICENSE)

AGV Monitor是一个基于WPF和C#的开发的AGV轨迹可视化工具。

## 功能特性

- **多AGV轨迹可视化**：实时显示多AGV的运动路径，包含时间成本信息
- **逐秒播放**：逐帧查看轨迹分析运动
- **实时统计**：显示时间戳和已完成任务数
- **键盘快捷键**：高效的键盘控制
- **轻量级**：编译后仅124KB，Windows 10以上无需额外运行时
- **实时地图**：交互式网格地图，AGV位置实时更新
- **任务调度**：支持任务调度与执行
- **热点图**：热点图实时显示
- **路径规划**：自动路线计算与冲突解决

## 技术栈

- **框架**：WPF（Windows Presentation Foundation）
- **语言**：C#（.NET Framework 4.8）
- **架构**：MVVM模式的关注点分离
- **测试**：xUnit单元测试框架
- **兼容性**：Windows 10及以上（无需额外运行时）

## 项目结构

```
AGV.Monitor/
 Services/              # 业务逻辑（AGV、任务、调度器）
 Geometry/              # 路径规划与几何计算
 Parsers/               # CSV数据解析
 Views/                 # UI组件（主窗口、地图渲染、热力图）
 Utils/                 # 工具函数
 EmbeddedResources/     # 数据文件（map_data.csv、task_csv.csv）
 App.xaml               # 应用程序入口

AGV.Monitor.UnitTests/     # 单元测试
```

## 构建与运行

### 前置条件
- Visual Studio 2022（或更高版本），需安装.NET桌面开发工作负载
- .NET Framework 4.8 开发者包

### 编译
```bash
cd src
dotnet build AGV.Monitor.sln -c Release
```

### 运行
```bash
cd src/AGV.Monitor/bin/Release
./AGV\ Monitor.exe
```

## 配置

编辑嵌入式资源文件来配置地图数据和任务：
- EmbeddedResources/map_data.csv - 地图信息：任务起点、终点，AGV起始位置
- EmbeddedResources/task_csv.csv - AGV任务

## 许可证

[MIT 许可证](LICENSE)

## 文档

- [贡献指南](CONTRIBUTING.md)
- [更新日志](CHANGELOG.md)
