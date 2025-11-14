using System;

namespace VainSabers.Helpers;

public class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

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
    
    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % Capacity;

        if (_count < Capacity)
            _count++;
    }
    
    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= _count)
                throw new IndexOutOfRangeException();
            
            int actualIndex = (_head - 1 - index + Capacity) % Capacity;
            return _buffer[actualIndex];
        }
    }
}