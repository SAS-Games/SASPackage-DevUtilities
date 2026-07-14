#if ENABLE_DEBUG
public sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;

    private int _writeIndex;
    private int _count;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public bool Add(T item, out T overwritten)
    {
        bool wasFull = _count == _buffer.Length;
        overwritten = wasFull ? _buffer[_writeIndex] : default;
        _buffer[_writeIndex] = item;

        _writeIndex = (_writeIndex + 1) % _buffer.Length;

        if (_count < _buffer.Length)
            _count++;

        return wasFull;
    }

    public ref T GetRecent(int index)
    {
        int actual =
            (_writeIndex - 1 - index + _buffer.Length)
            % _buffer.Length;

        return ref _buffer[actual];
    }

    public int Count => _count;
}
#endif
