using System;
using System.Collections.Generic;

namespace AGV.Monitor.Geometry;

/// <summary>
/// 简单的优先队列实现，用于替代 .NET 6+ 的 priorityQueue
/// </summary>
internal class SimplePriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
{
    private readonly List<(TElement Element, TPriority Priority)> _items = new List<(TElement, TPriority)>();

    public int Count => _items.Count;

    public void Enqueue(TElement element, TPriority priority)
    {
        _items.Add((element, priority));
    }

    public TElement Dequeue()
    {
        if (_items.Count == 0)
            throw new InvalidOperationException("Queue is empty");

        int minIndex = 0;
        for (int i = 1; i < _items.Count; i++)
        {
            if (_items[i].Priority.CompareTo(_items[minIndex].Priority) < 0)
                minIndex = i;
        }

        var item = _items[minIndex];
        _items.RemoveAt(minIndex);
        return item.Element;
    }
}
