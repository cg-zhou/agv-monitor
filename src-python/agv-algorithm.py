"""
AGV (自动导引车) 路径规划与调度

本模块实现了多 AGV 环境下的路径规划、任务调度和轨迹记录功能。
主要功能包括：
1. 基于 A* 算法的路径规划，考虑转弯成本和避障
2. 多 AGV 协同调度和冲突避免
3. 任务优先级管理
4. 轨迹数据记录和导出
5. 基础几何元素的实现：点、矩形、方向等

作者：cg-zhou(https://cg-zhou.top/)
版本：1.0
"""

import csv
from enum import Enum
import heapq
import sys
import io
import os
from typing import Any, Dict, List, Optional, Set, Tuple

# 轨迹文件输出路径
CSV_PATH = os.path.join(os.getcwd(), "agv_trajectory.csv")

class Direction(Enum):
    """
    方向枚举类
    
    定义AGV可能的朝向，使用角度值表示：
    - RIGHT: 0° (向右)
    - UP: 90° (向上)  
    - LEFT: 180° (向左)
    - DOWN: 270° (向下)
    """
    RIGHT = 0
    UP = 90
    LEFT = 180
    DOWN = 270


class PointEx:
    """
    二维坐标点类
    
    表示地图上的一个坐标点，提供邻接点计算、方向判断等功能。
    坐标系：X轴向右为正，Y轴向上为正
    """
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
    
    def __eq__(self, other):
        return isinstance(other, PointEx) and self.x == other.x and self.y == other.y
    
    def __hash__(self):
        return hash((self.x, self.y))
    
    def __str__(self):
        return f"({self.x}, {self.y})"
    
    def __repr__(self):
        return f"PointEx({self.x}, {self.y})"
    
    @property
    def left_neighbour(self) -> 'PointEx':
        """左边邻接点"""
        return PointEx(self.x - 1, self.y)
    
    @property
    def right_neighbour(self) -> 'PointEx':
        """右边邻接点"""
        return PointEx(self.x + 1, self.y)
    
    @property
    def up_neighbour(self) -> 'PointEx':
        """上方邻接点"""
        return PointEx(self.x, self.y - 1)
    
    @property
    def down_neighbour(self) -> 'PointEx':
        """下方邻接点"""
        return PointEx(self.x, self.y + 1)
    
    @property
    def neighbours(self) -> List['PointEx']:
        """四个邻接点"""
        return [self.left_neighbour, self.right_neighbour, self.up_neighbour, self.down_neighbour]
    
    def get_neighbour(self, direction: Direction) -> 'PointEx':
        """根据方向，获取邻接点"""
        if direction == Direction.RIGHT:
            return self.right_neighbour
        elif direction == Direction.UP:
            return self.up_neighbour
        elif direction == Direction.LEFT:
            return self.left_neighbour
        elif direction == Direction.DOWN:
            return self.down_neighbour
        else:
            return PointEx(0, 0)
    
    def is_neighbour(self, point: 'PointEx') -> bool:
        """判断是否是邻接点"""
        return ((self.x == point.x and (self.y == point.y + 1 or self.y == point.y - 1)) or
                (self.y == point.y and (self.x == point.x + 1 or self.x == point.x - 1)))
    
    def get_pitch_to_neighbour(self, neighbour_point: 'PointEx') -> Direction:
        """
        获取到邻接点的方向
        
        计算从当前点到相邻点的移动方向。
        
        Args:
            neighbour_point: 目标邻接点
            
        Returns:
            Direction: 移动方向
            
        Raises:
            Exception: 如果目标点不是邻接点
        """
        dx = neighbour_point.x - self.x
        dy = neighbour_point.y - self.y
        
        if dy == 0:
            if dx > 0:
                return Direction.RIGHT
            if dx < 0:
                return Direction.LEFT
        elif dx == 0:
            if dy > 0:
                return Direction.UP
            if dy < 0:
                return Direction.DOWN
        
        raise Exception(f"Failed to calculate pitch from point {self} to point {neighbour_point}.")


class PathTimePoint:
    """
    路径时间点数据结构
    
    包含位置信息和到达该位置的时间成本。
    用于路径规划中记录每个路径点的时间信息。
    """
    
    def __init__(self, position: PointEx, time_cost: int):
        self.position = position
        self.time_cost = time_cost
    
    def __str__(self):
        return f"{self.position} {self.time_cost}"


class RectEx:
    """矩形边界"""
    
    def __init__(self, left: int, top: int, right: int, bottom: int):
        self.left = left
        self.top = top
        self.right = right
        self.bottom = bottom


class SimplePriorityQueue:
    """简单优先队列实现"""
    
    def __init__(self):
        self._heap = []
        self._index = 0
    
    def enqueue(self, item, priority: int):
        heapq.heappush(self._heap, (priority, self._index, item))
        self._index += 1
    
    def dequeue(self):
        if not self._heap:
            raise IndexError("Priority queue is empty")
        return heapq.heappop(self._heap)[2]
    
    @property
    def count(self) -> int:
        return len(self._heap)


class PathPlanning:
    """
    AGV路径规划算法工具类
    
    提供基于A*算法的路径规划功能，考虑了AGV的朝向、转弯成本等实际约束。
    支持多AGV环境下的路径规划和冲突避免。
    """
    
    # 算法参数配置
    MOVE_COST = 1      # 移动一格的时间成本（秒）
    TURN_COST = 1      # 转向的时间成本（秒）
    LOAD_UNLOAD_TIME = 1  # 装卸货时间（秒）
    GRID_SIZE = (21, 21)  # 地图网格大小 (宽度, 高度)
    
    @staticmethod
    def manhattan(p1: PointEx, p2: PointEx) -> int:
        """计算曼哈顿距离"""
        return abs(p1.x - p2.x) + abs(p1.y - p2.y)
    
    @staticmethod
    def a_star_with_orientation(
        start: PointEx,
        goal: PointEx,
        orientation: Direction,
        obstacles: Set[PointEx],
        grid_size: Optional[Tuple[int, int]] = None
    ) -> List[PointEx]:
        """
        A*路径规划算法（考虑朝向和转弯成本）
        
        实现考虑AGV朝向的A*算法，在路径规划时会计算转弯成本。
        
        Args:
            start: 起始点
            goal: 目标点
            orientation: 起始朝向
            obstacles: 障碍物集合
            grid_size: 地图大小，默认使用GRID_SIZE
            
        Returns:
            List[PointEx]: 路径点列表，如果无路径则返回空列表
        """
        size = grid_size or PathPlanning.GRID_SIZE
        
        def get_neighbors(pos: PointEx, current_orientation: Direction):
            """
            获取邻居节点（考虑朝向）
            
            计算当前位置的所有可达邻居节点，同时考虑转弯成本。
            """
            directions = [
                (1, 0, Direction.RIGHT),    # 向右
                (-1, 0, Direction.LEFT),    # 向左
                (0, 1, Direction.UP),       # 向下
                (0, -1, Direction.DOWN)     # 向上
            ]
            
            for dx, dy, direction in directions:
                nx = pos.x + dx
                ny = pos.y + dy
                
                # 检查边界和障碍物
                if (1 <= nx <= size[0] and
                    1 <= ny <= size[1] and
                    PointEx(nx, ny) not in obstacles):
                    
                    # 计算移动成本：移动1步 + 可能的转弯成本
                    turn_cost = PathPlanning.TURN_COST if direction != current_orientation else 0
                    total_cost = PathPlanning.MOVE_COST + turn_cost
                    
                    yield (PointEx(nx, ny), direction, total_cost)
        
        # A*算法主循环
        frontier = SimplePriorityQueue()
        frontier.enqueue((PathPlanning.manhattan(start, goal), 0, start, orientation, [start]), 
                        PathPlanning.manhattan(start, goal))
        visited = set()
        
        while frontier.count > 0:
            _, cost, current_pos, current_orientation, path = frontier.dequeue()
            
            # 到达目标点
            if current_pos == goal:
                return path
            
            current_state = (current_pos, current_orientation)
            if current_state in visited:
                continue
            
            visited.add(current_state)
            
            # 扩展邻居节点
            for next_pos, next_orientation, move_cost in get_neighbors(current_pos, current_orientation):
                next_state = (next_pos, next_orientation)
                if next_state not in visited:
                    new_cost = cost + move_cost
                    new_path = path + [next_pos]
                    priority = new_cost + PathPlanning.manhattan(next_pos, goal)
                    
                    frontier.enqueue((priority, new_cost, next_pos, next_orientation, new_path), priority)
        
        return []  # 无路径
    
    @staticmethod
    def calculate_path_timing(path: List[PointEx], initial_pitch: Direction) -> List[PathTimePoint]:
        """
        计算路径上每个点的累计时间（包含转向时间）
        
        根据路径和初始朝向，计算到达每个路径点所需的累计时间。
        考虑了移动时间和转向时间。
        
        Args:
            path: 路径点列表
            initial_pitch: 初始朝向
            
        Returns:
            List[PathTimePoint]: 带时间信息的路径点列表
        """
        if not path:
            return []
        
        result = []
        current_time = 0
        current_pitch = initial_pitch
        
        # 起始点
        result.append(PathTimePoint(path[0], current_time))
        
        for i in range(1, len(path)):
            from_point = path[i - 1]
            to_point = path[i]
            new_pitch = from_point.get_pitch_to_neighbour(to_point)
            
            # 如果需要转向，增加转向时间
            if new_pitch != current_pitch:
                current_time += PathPlanning.TURN_COST
                current_pitch = new_pitch
            
            # 移动到下一个位置
            current_time += PathPlanning.MOVE_COST
            result.append(PathTimePoint(to_point, current_time))
        
        return result

class TaskPriority(Enum):
    """
    任务优先级枚举
    
    NORMAL: 普通任务
    HIGH: 紧急任务
    """
    NORMAL = 0
    HIGH = 1


class MapElementType(Enum):
    """
    地图元素类型枚举
    
    定义地图上可能出现的元素类型：
    - START_POINT: 起始点/取货点
    - END_POINT: 终点/送货点  
    - AGV: AGV车辆
    """
    START_POINT = "StartPoint"
    END_POINT = "EndPoint"
    AGV = "Agv"


class TaskRecord:
    """
    任务数据模型
    
    表示一个运输任务的基本信息，包括起始点、终点、优先级等。
    """
    
    def __init__(self):
        self.task_id: str = ""
        self.start_point: str = ""
        self.end_point: str = ""
        self.priority: TaskPriority = TaskPriority.NORMAL
        self.remaining_time: Optional[int] = None


class MapElement:
    """地图元素数据模型"""
    
    def __init__(self):
        self.type: MapElementType = MapElementType.START_POINT
        self.name: str = ""
        self.x: int = 0
        self.y: int = 0
        self.pitch: Optional[Direction] = None
    
    def __str__(self):
        return f"{self.type} {self.name} ({self.x}, {self.y})"


class MapElementParser:
    """地图元素解析器"""
    
    @staticmethod
    def get_bounds(map_elements: List[MapElement]) -> RectEx:
        """获取地图边界"""
        if not map_elements:
            return RectEx(0, 0, 0, 0)
        
        min_x = min(element.x for element in map_elements)
        max_x = max(element.x for element in map_elements)
        min_y = min(element.y for element in map_elements)
        max_y = max(element.y for element in map_elements)
        
        return RectEx(min_x, min_y, max_x, max_y)

class TaskEx(TaskRecord):
    """扩展任务类"""
    
    def __init__(self, task_record: TaskRecord, start_position: PointEx, end_position: PointEx):
        super().__init__()
        self.task_id = task_record.task_id
        self.start_point = task_record.start_point
        self.end_point = task_record.end_point
        self.priority = task_record.priority
        self.remaining_time = task_record.remaining_time
        self.start_position = start_position
        self.end_position = end_position
        
        # 取料点坐标
        self.pickup_position = (start_position.left_neighbour 
                               if start_position.x > 10 
                               else start_position.right_neighbour)
        
        self.agv: Optional['Agv'] = None
        self.start_timestamp: int = 0
        self.complete_timestamp: int = 0
        self.is_completed: bool = False
    
    @property
    def is_pending(self) -> bool:
        return self.agv is None
    
    @property
    def is_running(self) -> bool:
        return not self.is_pending and not self.is_completed
    
    def load_by(self, agv: 'Agv', start_timestamp: int):
        """被AGV装载"""
        self.agv = agv
        self.start_timestamp = start_timestamp
    
    def unload(self, complete_timestamp: int):
        """卸载"""
        self.complete_timestamp = complete_timestamp
        self.is_completed = True
    
    def __str__(self):
        return f"{self.start_point} -> {self.end_point} {self.priority}"


class Agv:
    """AGV类"""
    
    def __init__(self, name: str, position: PointEx, pitch: Direction):
        self.name = name
        self.position = position
        self.pitch = pitch
        self.is_loaded = False
        self.loaded_task: Optional[TaskEx] = None
        self.path_time_points: List[PathTimePoint] = []
    
    def load(self, task: TaskEx, timestamp: int):
        """装载货物"""
        self.is_loaded = True
        task.load_by(self, timestamp)
        self.loaded_task = task
    
    def unload(self, timestamp: int):
        """卸载货物"""
        self.path_time_points = []
        self.is_loaded = False
        if self.loaded_task:
            self.loaded_task.unload(timestamp)
        self.loaded_task = None
    
    def can_unload(self) -> bool:
        """是否可以卸载"""
        return (self.is_loaded and 
                self.loaded_task and 
                self.position.is_neighbour(self.loaded_task.end_position))
    
    def should_turn(self) -> bool:
        """是否应该转向"""
        return (len(self.path_time_points) > 1 and 
                self.position.get_pitch_to_neighbour(self.path_time_points[1].position) != self.pitch)
    
    def turn(self, specified_pitch: Optional[Direction] = None):
        """转向"""
        if specified_pitch is not None:
            self.pitch = specified_pitch
            return
        
        if len(self.path_time_points) > 1:
            self.pitch = self.position.get_pitch_to_neighbour(self.path_time_points[1].position)
            for i in range(1, len(self.path_time_points)):
                self.path_time_points[i].time_cost -= 1
    
    def should_move(self) -> bool:
        """是否应该移动"""
        return (len(self.path_time_points) > 1 and 
                self.position.get_pitch_to_neighbour(self.path_time_points[1].position) == self.pitch)
    
    def move(self):
        """移动"""
        if len(self.path_time_points) > 1:
            self.position = self.path_time_points[1].position
            for path_time_point in self.path_time_points:
                path_time_point.time_cost -= 1
            self.path_time_points.pop(0)
    
    def __str__(self):
        return f"[AGV] {self.name} {self.position} {self.pitch}"

class DataLoader:
    """数据加载器"""
    
    @staticmethod
    def parse_map_data_from_file(file_path: str) -> List[MapElement]:
        """从文件解析地图数据"""
        map_elements = []
        
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"Map file not found: {file_path}")
        
        with open(file_path, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                element = MapElement()
                
                # 解析类型（支持多种格式）
                type_str = row.get('type', row.get('Type', '')).strip()
                if type_str == 'start_point' or type_str == 'StartPoint':
                    element.type = MapElementType.START_POINT
                elif type_str == 'end_point' or type_str == 'EndPoint':
                    element.type = MapElementType.END_POINT
                elif type_str == 'agv' or type_str == 'Agv':
                    element.type = MapElementType.AGV
                else:
                    continue  # 跳过未知类型
                
                element.name = row.get('name', row.get('Name', '')).strip()
                element.x = int(row.get('x', row.get('X', 0)))
                element.y = int(row.get('y', row.get('Y', 0)))
                
                # 解析方向（仅对AGV有效）
                if element.type == MapElementType.AGV:
                    pitch_str = row.get('pitch', row.get('Pitch', '')).strip()
                    if pitch_str == '0':
                        element.pitch = Direction.RIGHT
                    elif pitch_str == '90':
                        element.pitch = Direction.UP
                    elif pitch_str == '180':
                        element.pitch = Direction.LEFT
                    elif pitch_str == '270':
                        element.pitch = Direction.DOWN
                    else:
                        element.pitch = Direction.RIGHT  # 默认方向
                
                map_elements.append(element)
        
        return map_elements
    
    @staticmethod
    def parse_task_data_from_file(file_path: str) -> List[TaskRecord]:
        """从文件解析任务数据"""
        task_records = []
        
        if not os.path.exists(file_path):
            raise FileNotFoundError(f"Task file not found: {file_path}")
        
        with open(file_path, 'r', encoding='utf-8') as f:
            reader = csv.DictReader(f)
            for row in reader:
                task = TaskRecord()
                task.task_id = row.get('task_id', row.get('TaskId', '')).strip()
                task.start_point = row.get('start_point', row.get('StartPoint', '')).strip()
                task.end_point = row.get('end_point', row.get('EndPoint', '')).strip()
                
                # 解析优先级
                priority_str = row.get('priority', row.get('Priority', '')).strip()
                if priority_str == 'High' or priority_str == '1':
                    task.priority = TaskPriority.HIGH
                else:
                    task.priority = TaskPriority.NORMAL
                
                # 解析剩余时间
                remaining_time_str = row.get('remaining_time', row.get('RemainingTime', '')).strip()
                if remaining_time_str and remaining_time_str.isdigit():
                    task.remaining_time = int(remaining_time_str)
                
                task_records.append(task)
        
        return task_records

class TrajectoryRecorderService:
    """轨迹记录服务"""
    
    def __init__(self, agvs: List['Agv']):
        self.agvs = agvs
        self.trajectory_records: List[Dict[str, Any]] = []
        
        # 记录初始状态（时间戳0）
        self.add(0)
    
    def add(self, timestamp: int):
        """添加当前时间戳的所有AGV状态"""
        for agv in self.agvs:
            record = {
                'timestamp': timestamp,
                'name': agv.name,
                'X': agv.position.x,
                'Y': agv.position.y,
                'pitch': agv.pitch.value,  # Direction枚举的数值
                'loaded': 'true' if agv.is_loaded else 'false',
                'destination': agv.loaded_task.end_point if agv.loaded_task else '',
                'Emergency': 'true' if (agv.loaded_task and agv.loaded_task.priority == TaskPriority.HIGH) else 'false',
                'TaskId': agv.loaded_task.task_id if agv.loaded_task else ''
            }
            self.trajectory_records.append(record)
    
    def save_to_csv(self, file_path: str):
        """保存轨迹记录到CSV文件"""
        fieldnames = ['timestamp', 'name', 'X', 'Y', 'pitch', 'loaded', 'destination', 'Emergency', 'TaskId']
        
        # 确保目录存在
        os.makedirs(os.path.dirname(file_path), exist_ok=True)
        
        with open(file_path, 'w', newline='', encoding='utf-8') as csvfile:
            writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
            writer.writeheader()
            for record in self.trajectory_records:
                writer.writerow(record)
        
        print(f"轨迹文件已保存: {file_path}")
        print(f"总记录数: {len(self.trajectory_records)}")
    
    def get_records_count(self) -> int:
        """获取记录数量"""
        return len(self.trajectory_records)

class AgvContext:
    """
    AGV 上下文管理类
    
    包含AGV运行环境的所有信息，包括地图元素、任务列表、AGV列表、
    障碍物信息等。负责初始化运行环境和提供相关查询功能。
    """
    
    def __init__(self, map_elements: List[MapElement], task_records: List[TaskRecord]):
        self.map_elements = map_elements
        self.task_records = task_records
        
        # 创建扩展任务列表（包含位置信息）
        self.tasks = []
        for task in task_records:
            start_position = self._get_position_by_name(MapElementType.START_POINT, task.start_point)
            end_position = self._get_position_by_name(MapElementType.END_POINT, task.end_point)
            self.tasks.append(TaskEx(task, start_position, end_position))
        
        # 创建AGV列表
        self.agvs = []
        for item in map_elements:
            if item.type == MapElementType.AGV:
                agv = Agv(item.name, PointEx(item.x, item.y), item.pitch)
                self.agvs.append(agv)
        
        # 创建固定障碍物集合（起始点和终点）
        obstacles = []
        for item in map_elements:
            if item.type in [MapElementType.START_POINT, MapElementType.END_POINT]:
                obstacles.append(PointEx(item.x, item.y))
        
        # 添加地图边界作为障碍物
        min_x = min(item.x for item in map_elements)
        max_x = max(item.x for item in map_elements)
        min_y = min(item.y for item in map_elements)
        max_y = max(item.y for item in map_elements)
        
        # 在地图四周添加边界障碍物
        for x in range(min_x - 1, max_x + 2):
            obstacles.append(PointEx(x, min_y - 1))
            obstacles.append(PointEx(x, max_y + 1))
        
        for y in range(min_y - 1, max_y + 2):
            obstacles.append(PointEx(min_x - 1, y))
            obstacles.append(PointEx(max_x + 1, y))
        
        self.fixed_map_obstacles = obstacles
        self.map_bounds = MapElementParser.get_bounds(map_elements)
        
        # 创建轨迹记录服务
        self.trajectory_recorder = TrajectoryRecorderService(self.agvs)
    
    def _get_position_by_name(self, element_type: MapElementType, name: str) -> PointEx:
        """根据名称获取位置"""
        for element in self.map_elements:
            if element.type == element_type and element.name == name:
                return PointEx(element.x, element.y)
        raise ValueError(f"Element not found: {element_type} {name}")
    
    def get_completed_tasks(self) -> List[TaskEx]:
        """获取已完成的任务"""
        return [task for task in self.tasks if task.is_completed]
    
    @property
    def all_tasks_completed(self) -> bool:
        """是否所有任务都已完成"""
        return all(task.is_completed for task in self.tasks)
    
    def get_sorted_pending_tasks(self) -> List[TaskEx]:
        """
        获取排序后的待处理任务
        
        按照复合优先级对待处理任务进行排序：
        1. 按起始点分组
        2. 组内按序列顺序
        3. 按任务优先级（紧急任务优先）
        4. 按是否包含紧急任务的队列优先
        5. 按队列长度排序
        
        Returns:
            List[TaskEx]: 排序后的待处理任务列表
        """
        pending_tasks = [task for task in self.tasks if task.is_pending]
        
        # 按起始点分组
        grouped_tasks = {}
        for task in pending_tasks:
            if task.start_point not in grouped_tasks:
                grouped_tasks[task.start_point] = []
            grouped_tasks[task.start_point].append(task)
        
        middleY = 10

        # 复合排序逻辑
        def sort_key(task):
            group = grouped_tasks[task.start_point]
            # 先按序列
            sequence_index = group.index(task)
            # 再按优先级 (HIGH = 1, NORMAL = 0，所以用负值让高优先级排前面)
            priority_value = -task.priority.value
            # 然后根据队列中是否有优先级高的任务
            has_high_priority = any(t.priority == TaskPriority.HIGH for t in group)
            has_high_priority_value = -1 if has_high_priority else 0
            # 然后根据队列长度排序
            queue_length = -len(group)
            # 最后按Y坐标排序，优先选取上方和下方的任务
            y_position = 0 if task.pickup_position.y != middleY else 1
            return (sequence_index, priority_value, has_high_priority_value, queue_length, y_position)

        pending_tasks.sort(key=sort_key)
        return pending_tasks

class SchedulerService:
    """
    AGV调度器服务
    
    负责AGV系统的核心调度逻辑，包括：
    1. 任务分配：将任务分配给合适的AGV
    2. 路径规划：为AGV计算最优路径
    3. 冲突检测：避免AGV之间的碰撞
    4. 协调移动：统一管理AGV的移动和转向
    """
    
    def __init__(self, context: AgvContext):
        self.context = context
        self.timestamp = 0  # 当前时间戳（秒）
    
    def process_to_complete(self):
        """处理直到完成"""
        while not self.context.all_tasks_completed:
            self.process()
    
    def process(self):
        """
        处理一个时间步的调度逻辑
        
        按优先级顺序执行以下操作：
        1. 卸载已到达目的地的AGV
        2. 装载在取货点的AGV
        3. 移动已装载货物的AGV
        4. 转向已装载货物的AGV
        5. 为空闲AGV分配任务和路径
        6. 移动和转向空闲AGV
        7. 记录当前状态到轨迹文件
        """
        # 防止死锁：如果超过最大时间限制，抛出异常
        MAX_TIMESTAMP = 400
        if self.timestamp > MAX_TIMESTAMP:
            raise Exception(f"Failed to complete all tasks after {MAX_TIMESTAMP}s")
        
        self.timestamp += 1

        agvs = self.context.agvs
        handled_agvs = []  # 记录已处理的AGV，避免重复处理
        
        # 1. 处理卸载货物（优先级最高）
        for agv in agvs:
            if agv in handled_agvs:
                continue
            if agv.can_unload():
                agv.unload(self.timestamp)
                handled_agvs.append(agv)
        
        # 2. 处理装载货物
        pending_load_tasks = self.context.get_sorted_pending_tasks()
        for agv in agvs:
            if agv in handled_agvs or agv.is_loaded:
                continue
            
            # 检查AGV是否在取货点
            for pending_load_task in pending_load_tasks:
                if pending_load_task.pickup_position == agv.position:
                    agv.load(pending_load_task, self.timestamp)
                    handled_agvs.append(agv)
                    break
        
        # 3. 已装载货物的AGV移动（批量处理避免冲突）
        loaded_agvs = [agv for agv in agvs if agv.is_loaded]
        self._batch_move_agvs(loaded_agvs, handled_agvs, True, None)
        
        # 4. 已装载货物的AGV转向
        for agv in agvs:
            if agv in handled_agvs or not agv.is_loaded:
                continue
            if agv.should_turn():
                agv.turn()
                handled_agvs.append(agv)
        
        # 5. 为空闲AGV分配任务
        temp_assignments = {}  # 临时任务分配表
        pending_tasks = self.context.get_sorted_pending_tasks()
        idle_agvs = [agv for agv in agvs if agv not in handled_agvs and not agv.is_loaded]
        
        # 任务分配：为每个任务找到最近的空闲AGV
        for task in pending_tasks:
            if not idle_agvs:
                break
            
            assignment_options = []
            for agv in idle_agvs:
                obstacles = self._get_obstacles(agv, agvs)
                path = self._calculate_path_to_pickup_position(agv, task, obstacles)
                path_time_points = PathPlanning.calculate_path_timing(path, agv.pitch)
                assignment_options.append((agv, path_time_points))
            
            # 选择路径最短的AGV
            assignment_options.sort(key=lambda x: x[1][-1].time_cost if x[1] else float('inf'))
            if assignment_options:
                selected_agv, path_time_points = assignment_options[0]
                idle_agvs.remove(selected_agv)
                
                selected_agv.path_time_points = path_time_points
                temp_assignments[selected_agv] = task
        
        # 6. 空闲AGV的移动和转向
        turn_agvs = [agv for agv in temp_assignments.keys() if agv.should_turn()]
        move_agvs = [agv for agv in temp_assignments.keys() if agv.should_move()]
        
        # 先转向
        for agv in turn_agvs:
            agv.turn()
        
        # 再移动（批量处理避免冲突）
        self._batch_move_agvs(move_agvs, handled_agvs, False, temp_assignments)

        # 当待处理的任务，都完成后，将空闲 AGV 移动到地图边缘，防止死锁
        if not pending_tasks:
            for idle_agv in [agv for agv in agvs if agv not in handled_agvs]:
                additional_obstacles = self._get_obstacles(idle_agv, agvs)
                obstacles = self._build_obstacles(additional_obstacles)

                # 找到距离地图边缘最近的坐标
                x = idle_agv.position.x
                y = idle_agv.position.y

                points = []
                loaded_agvs = [agv for agv in agvs if agv.is_loaded]
                
                # 检查上方边缘 (y=20)
                if not any(agv.position.x == x and agv.position.y > y for agv in loaded_agvs):
                    points.append(PointEx(x, 20))
                
                # 检查下方边缘 (y=1)
                if not any(agv.position.x == x and agv.position.y < y for agv in loaded_agvs):
                    points.append(PointEx(x, 1))
                
                # 检查右方边缘 (x=20)
                if not any(agv.position.x > x and agv.position.y == y for agv in loaded_agvs):
                    points.append(PointEx(20, y))
                
                # 检查左方边缘 (x=1)
                if not any(agv.position.x < x and agv.position.y == y for agv in loaded_agvs):
                    points.append(PointEx(1, y))

                if points:
                    # 选择距离最近的边缘点
                    goal = min(points, key=lambda point: abs(point.x - idle_agv.position.x) + abs(point.y - idle_agv.position.y))

                    path = PathPlanning.a_star_with_orientation(idle_agv.position, goal, idle_agv.pitch, obstacles)
                    path_time_points = PathPlanning.calculate_path_timing(path, idle_agv.pitch)
                    idle_agv.path_time_points = path_time_points
                    if idle_agv.should_move():
                        idle_agv.move()
                    elif idle_agv.should_turn():
                        idle_agv.turn()

        print(f"[AGV scheduler] {self.timestamp}s, complete {len(self.context.get_completed_tasks())} tasks")

        # 7. 记录当前时间步的轨迹
        self.context.trajectory_recorder.add(self.timestamp)
    
    def _batch_move_agvs(self, agvs: List[Agv], handled_agvs: List[Agv], 
                        is_loaded: bool, temp_assignments: Optional[Dict[Agv, TaskEx]]):
        """
        批量移动AGV（包含冲突检测和避让）
        
        处理多个AGV的协调移动，避免碰撞和死锁。
        通过复杂的冲突检测算法，决定AGV的移动优先级和避让策略。
        
        Args:
            agvs: 待移动的AGV列表
            handled_agvs: 已处理的AGV列表
            is_loaded: 是否为已装载货物的AGV
            temp_assignments: 临时任务分配表（仅用于空闲AGV）
        """
        moved_items = []  # 记录已移动的AGV及其信息
        
        # 循环处理直到所有可移动的AGV都被处理
        while True:
            is_assigned = False  # 标记本轮是否有AGV被处理
            
            for agv in agvs:
                if agv in handled_agvs or agv.is_loaded != is_loaded:
                    continue
                
                # 获取当前AGV的任务
                current_agv_task = agv.loaded_task if is_loaded else temp_assignments[agv]
                obstacles = self._get_obstacles(agv, self.context.agvs)
                
                # 重新计算路径（考虑当前障碍物）
                if is_loaded:
                    path = self._calculate_path_to_end_point(agv, current_agv_task, obstacles)
                else:
                    path = self._calculate_path_to_pickup_position(agv, current_agv_task, obstacles)
                
                path_time_points = PathPlanning.calculate_path_timing(path, agv.pitch)
                agv.path_time_points = path_time_points
                
                # 检查是否有可移动的路径
                if len(path_time_points) < 2:
                    continue
                
                expect_move_pitch = agv.position.get_pitch_to_neighbour(path_time_points[1].position)
                if expect_move_pitch != agv.pitch:
                    continue
                
                # 复杂的冲突检测和避让逻辑
                should_turn = False
                turn_direction = None
                
                # 检查与已移动AGV的冲突
                for moved_item in moved_items:
                    moved_agv, moved_pos, moved_task = moved_item
                    
                    # 只考虑相同朝向的AGV冲突
                    if moved_agv.pitch == agv.pitch:
                        # 沿X轴移动时的冲突检测
                        if (agv.pitch in [Direction.LEFT, Direction.RIGHT] and
                            moved_pos.x == agv.position.x and
                            moved_pos.y == agv.position.y + 1 and
                            current_agv_task.end_position.y > agv.position.y and
                            moved_task.end_position.y <= moved_agv.position.y):
                            should_turn = True
                            turn_direction = Direction.UP
                            break
                        
                        if (agv.pitch in [Direction.LEFT, Direction.RIGHT] and
                            moved_pos.x == agv.position.x and
                            moved_pos.y == agv.position.y - 1 and
                            current_agv_task.end_position.y < agv.position.y and
                            moved_task.end_position.y >= moved_agv.position.y):
                            should_turn = True
                            turn_direction = Direction.DOWN
                            break
                        
                        # 沿Y轴移动时的冲突检测
                        if (agv.pitch in [Direction.UP, Direction.DOWN] and
                            moved_pos.y == agv.position.y and
                            moved_pos.x == agv.position.x - 1 and
                            current_agv_task.end_position.x < agv.position.x and
                            moved_task.end_position.x >= moved_agv.position.x):
                            should_turn = True
                            turn_direction = Direction.LEFT
                            break
                        
                        if (agv.pitch in [Direction.UP, Direction.DOWN] and
                            moved_pos.y == agv.position.y and
                            moved_pos.x == agv.position.x + 1 and
                            current_agv_task.end_position.x > agv.position.x and
                            moved_task.end_position.x <= moved_agv.position.x):
                            should_turn = True
                            turn_direction = Direction.RIGHT
                            break
                
                # 如果需要避让，则转向而不移动
                if should_turn:
                    handled_agvs.append(agv)
                    agv.turn(turn_direction)
                    agv.path_time_points = []  # 清空路径，下次重新计算
                    is_assigned = True
                    continue
                
                # 执行移动
                moved_items.append((agv, agv.position, current_agv_task))
                handled_agvs.append(agv)
                agv.move()
                is_assigned = True
            
            # 如果本轮没有AGV被处理，退出循环
            if not is_assigned:
                break
    
    def _calculate_path_to_pickup_position(self, agv: Agv, task: TaskEx, 
                                          additional_obstacles: List[PointEx]) -> List[PointEx]:
        """计算AGV到任务取料点的路径"""
        obstacles = self._build_obstacles(additional_obstacles)
        goal = task.pickup_position
        return PathPlanning.a_star_with_orientation(agv.position, goal, agv.pitch, obstacles)
    
    def _calculate_path_to_end_point(self, agv: Agv, task: TaskEx, 
                                    additional_obstacles: List[PointEx]) -> List[PointEx]:
        """计算AGV到任务终点的路径"""
        obstacles = self._build_obstacles(additional_obstacles)
        goal = task.end_position
        obstacles.remove(goal)  # 移除终点障碍
        return PathPlanning.a_star_with_orientation(agv.position, goal, agv.pitch, obstacles)
    
    def _build_obstacles(self, additional_obstacles: List[PointEx]) -> Set[PointEx]:
        """构建障碍物集合"""
        obstacles = self.context.fixed_map_obstacles.copy()
        if additional_obstacles:
            for obstacle in additional_obstacles:
                obstacles.append(obstacle)
        return obstacles
    
    def _get_obstacles(self, agv: Agv, agvs: List[Agv]) -> List[PointEx]:
        """获取AGV周围的障碍物（其他AGV位置）"""
        # 所有 AGV 的位置
        agv_positions = [x.position for x in agvs]

        # 当前 AGV 的邻接点，如果有 AGV 存在，则作为障碍点
        obstacles = [pos for pos in agv.position.neighbours if pos in agv_positions]

        # 将可能出现 "十字互锁" 的点，加入到障碍点，十字互锁的示例：
        # 其中 □ 为取货点；○ 和 箭头表示 AGV
        #   ↓
        # → ○ □
        #   ↑
        # 十字互锁的条件：
        # 1. 一个 AGV #n 周围四个临接点中，有三个障碍点（障碍点是固定障碍，或者其它 AGV #m）
        # 2. 把 AGV #n 的第四个邻接点，作为当前 AGV 移动的障碍点，防止十字互锁
        for agv_item in [x for x in agvs if x != agv]:
            neighbours = agv_item.position.neighbours.copy()
            neighbours = [n for n in neighbours if n not in self.context.fixed_map_obstacles]

            for neighbour_agv in [x for x in agvs if x != agv_item and x.position.is_neighbour(agv_item.position)]:
                neighbours = [n for n in neighbours if n != neighbour_agv.position]

            if (len(neighbours) == 1 and
                neighbours[0] in agv.position.neighbours):
                obstacles.append(neighbours[0])

        return obstacles


def run(agv_position: str, agv_task: str) -> str:
    """
    AGV调度系统主运行函数
    
    执行完整的AGV调度流程：
    1. 加载地图数据和任务数据
    2. 初始化AGV运行环境
    3. 运行调度算法直到所有任务完成
    4. 保存轨迹数据到CSV文件
    
    Args:
        agv_position: 地图数据文件路径
        agv_task: 任务数据文件路径
        
    Returns:
        str: 轨迹文件保存路径
    """
    # 加载地图数据
    map_elements = DataLoader.parse_map_data_from_file(agv_position)
    print(f"Loaded {len(map_elements)} map elements")
    
    # 加载任务数据
    task_records = DataLoader.parse_task_data_from_file(agv_task)
    print(f"Loaded {len(task_records)} tasks")
    
    # 创建AGV上下文
    context = AgvContext(map_elements, task_records)
    print(f"Created context with {len(context.agvs)} AGVs and {len(context.tasks)} tasks")
    
    # 创建调度器
    scheduler = SchedulerService(context)
    
    # 运行调度器直到完成
    print("Starting AGV scheduling...")
    scheduler.process_to_complete()
    
    print(f"All tasks completed in {scheduler.timestamp} seconds")
    
    # 保存轨迹文件
    context.trajectory_recorder.save_to_csv(CSV_PATH)

if __name__ == "__main__":
    """
    程序入口点
    """
    try:
        # 本地运行模式
        agv_position = os.path.join(os.getcwd(), "data", "map_data.csv")
        
        # 默认的任务列表，用于普通测试
        agv_task = os.path.join(os.getcwd(), "data", "task_data.csv")
        run(agv_position, agv_task)
    except Exception as e:
        print(f"Server crashed: {e}", file=sys.stderr)
        sys.exit(1)