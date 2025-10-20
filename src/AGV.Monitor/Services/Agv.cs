using AGV.Monitor.Geometry;
using System.Collections.Generic;

namespace AGV.Monitor.Services;

public class Agv
{
    public Agv(string name, PointEx position, Direction pitch)
    {
        Name = name;
        Position = position;
        Pitch = pitch;
    }

    public string Name { get; private set; }
    public PointEx Position { get; private set; }
    public Direction Pitch { get; private set; }

    public bool IsLoaded { get; private set; }
    public TaskEx LoadedTask { get; private set; }

    public void Load(TaskEx task, int timestamp)
    {
        IsLoaded = true;
        task.LoadBy(this, timestamp);
        LoadedTask = task;
    }

    public void Unload(int timestamp)
    {
        PathTimePoints = new List<PathTimePoint>();
        IsLoaded = false;
        LoadedTask.Unload(timestamp);
        LoadedTask = null;
    }

    public bool CanUnload()
    {
        return IsLoaded && Position.IsNeighbour(LoadedTask.EndPosition);
    }

    public override string ToString()
    {
        return $"[AGV] {Name} {Position} {Pitch}";
    }

    public List<PathTimePoint> PathTimePoints { get; set; } = new List<PathTimePoint>();

    public bool ShouldTurn()
    {
        return PathTimePoints.Count > 1 && Position.GetPitchToNeighbour(PathTimePoints[1].Position) != Pitch;
    }

    public void Turn(Direction? specifiedPitch = null)
    {
        if (specifiedPitch.HasValue)
        {
            Pitch = specifiedPitch.Value;
            return;
        }

        if (PathTimePoints.Count > 1)
        {
            Pitch = Position.GetPitchToNeighbour(PathTimePoints[1].Position);
            for (var i = 1; i < PathTimePoints.Count; i++)
            {
                PathTimePoints[i].TimeCost -= 1;
            }
        }
    }

    public bool ShouldMove()
    {
        return PathTimePoints.Count > 1 && Position.GetPitchToNeighbour(PathTimePoints[1].Position) == Pitch;
    }

    public void Move()
    {
        if (PathTimePoints.Count > 1)
        {
            Position = PathTimePoints[1].Position;
            foreach (var pathTimePoint in PathTimePoints)
            {
                pathTimePoint.TimeCost -= 1;
            }
            PathTimePoints.RemoveAt(0);
        }
    }

}
