# AGV Monitor

> [中文文档](README.cn.md)

[![GitHub license](https://img.shields.io/github/license/cg-zhou/agv-monitor.svg)](https://github.com/cg-zhou/agv-monitor/blob/main/LICENSE)

A real-time AGV (Automated Guided Vehicle) monitoring and simulation system built with WPF and C#. Designed for warehouse automation.

## Features

- **Multi-AGV Trajectory Visualization**: Visualize movement paths of multiple AGVs with time-cost information
- **Second-by-Second Playback**: Step through trajectories frame-by-frame to analyze each movement
- **Real-time Statistics**: Display current timestamp and completed task count
- **Keyboard Shortcuts**: Efficient keyboard control
- **Lightweight**: Compiled size is only 124KB, no additional runtime required on Windows 10+
- **Live Map**: Interactive grid-based map with real-time AGV position updates
- **Task Scheduling**: Support for task scheduling and execution
- **Heat Map**: Real-time heat map visualization
- **Path Planning**: Automatic route calculation and conflict resolution

## Technology Stack

- **Framework**: WPF (Windows Presentation Foundation)
- **Language**: C# with .NET Framework 4.8
- **Architecture**: MVVM-inspired separation of concerns
- **Testing**: xUnit for unit tests
- **Compatibility**: Windows 10+ (no additional runtime required)

## Project Structure

```
AGV.Monitor/
├── Services/              # Business logic (AGV, Task, Scheduler)
├── Geometry/              # Path planning and geometric calculations
├── Parsers/               # CSV data parsing
├── Views/                 # UI components (MainWindow, MapRenderer, Heat Map)
├── Utils/                 # Utility functions
├── EmbeddedResources/     # Data files (map_data.csv, task_csv.csv)
└── App.xaml               # Application entry point

AGV.Monitor.UnitTests/     # Unit tests
```

## Building and Running

### Prerequisites
- Visual Studio 2022 (or later) with .NET desktop development workload
- .NET Framework 4.8 Developer Pack

### Build
```bash
cd src
dotnet build AGV.Monitor.sln -c Release
```

### Run
```bash
cd src/AGV.Monitor/bin/Release
./AGV\ Monitor.exe
```

## Configuration

Edit embedded resource files to configure map data and tasks:
- EmbeddedResources/map_data.csv - Map information: task start/end points, AGV starting positions
- EmbeddedResources/task_csv.csv - AGV tasks

## License

[MIT License](LICENSE)

## Documentation

- [Contributing Guidelines](CONTRIBUTING.md)
- [Change Log](CHANGELOG.md)