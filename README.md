# AGV Monitor

> [中文文档](README.cn.md)

[![GitHub license](https://img.shields.io/github/license/cg-zhou/agv-monitor.svg)](https://github.com/cg-zhou/agv-monitor/blob/main/LICENSE)

AGV Monitor is an AGV trajectory visualization tool developed based on WPF and C#.

## Features

- **Multi-AGV Trajectory Visualization**: Real-time display of motion paths for multiple AGVs, including time cost information
- **Frame-by-Frame Playback**: View and analyze trajectories frame by frame
- **Real-time Statistics**: Display timestamps and completed task count
- **Keyboard Shortcuts**: Efficient keyboard control
- **Lightweight**: Only 124KB after compilation, no additional runtime required on Windows 10 and above
- **Interactive Map**: Real-time grid map with AGV position updates
- **Task Scheduling**: Support for task scheduling and execution
- **Heatmap**: Real-time heatmap display
- **Path Planning**: Automatic route calculation and conflict resolution

## Technology Stack

- **Framework**: WPF (Windows Presentation Foundation)
- **Language**: C# (.NET Framework 4.8)
- **Architecture**: MVVM pattern with separation of concerns
- **Testing**: xUnit unit testing framework
- **Compatibility**: Windows 10 and above (no additional runtime required)

## Project Structure

```
AGV.Monitor/
 Services/              # Business logic (AGV, Tasks, Scheduler)
 Geometry/              # Path planning and geometric calculations
 Parsers/               # CSV data parsing
 Views/                 # UI components (Main window, Map renderer, Heatmap)
 Utils/                 # Utility functions
 EmbeddedResources/     # Data files (map_data.csv, task_csv.csv)
 App.xaml               # Application entry point

AGV.Monitor.UnitTests/     # Unit tests
```

## Build and Run

### Prerequisites
- Visual Studio 2022 (or higher) with .NET desktop development workload installed
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
- EmbeddedResources/map_data.csv - Map information: task start point, end point, AGV starting positions
- EmbeddedResources/task_csv.csv - AGV tasks

## License

[MIT License](LICENSE)

## Documentation

- [Contributing Guide](CONTRIBUTING.md)
- [Changelog](CHANGELOG.md)

