using System;
using System.Reflection;
using UnityEngine;

public static class Helpers
{
    public static TComponent AddInitComponent<TComponent>(
        this GameObject self, 
        params object[] args
    ) where TComponent : Component
    {
        var comp = self.AddComponent<TComponent>();
        
        var method = typeof(TComponent).GetMethod(
            "Init",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method != null)
        {
            try
            {
                method.Invoke(comp, args);
            }
            catch (TargetParameterCountException)
            {
                Plugin.Log.Error(
                    $"Init(...) on {typeof(TComponent).Name} expects {method?.GetParameters().Length} parameters, " +
                    $"but {args.Length} were provided."
                );
            }
            catch (Exception ex)
            {
                Plugin.Log.Error($"Init(...) invocation on {typeof(TComponent).Name} failed: {ex}");
            }
        }

        return comp;
    }
    
    public static TComponent AddInitChild<TComponent>(
        this GameObject self, 
        params object[] args
    ) where TComponent : Component
    {
        var childGo = new GameObject(typeof(TComponent).Name);
        childGo.transform.SetParent(self.transform, false);

        return childGo.AddInitComponent<TComponent>(args);
    }
}

public static class Plugin
{
    public static class Log
    {
        public static void Error(string message) => Debug.LogError(message);
    }
    
    public static void Print(string message) => Debug.Log(message);
}

public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;   // Next write position
    private int _count;  // Number of elements currently in buffer

    public int Capacity => _buffer.Length;
    public int Count => _count;

    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0.", nameof(capacity));

        _buffer = new T[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Adds a new item to the buffer. Overwrites the oldest when full.
    /// </summary>
    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % Capacity;

        if (_count < Capacity)
            _count++;
    }

    /// <summary>
    /// Indexer: [0] is the latest item, [Count-1] is the oldest.
    /// </summary>
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new IndexOutOfRangeException();

            // Map index: 0 → latest, Count-1 → oldest
            int actualIndex = (_head - 1 - index + Capacity) % Capacity;
            return _buffer[actualIndex];
        }
    }
}