using System.Collections;
using System.Linq;

namespace FancyEnumGenerator.Internal;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[]? _array;

    public EquatableArray(T[] array)
    {
        _array = array;
    }

    public EquatableArray(IEnumerable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _array = source as T[] ?? source.ToArray();
    }

    public bool Equals(EquatableArray<T> other)
    {
        return AsSpan().SequenceEqual(other.AsSpan());
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in AsSpan())
            {
                hash = (hash * 31) + (item?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public ReadOnlySpan<T> AsSpan()
    {
        return _array.AsSpan();
    }

    public T[]? GetArray()
    {
        return _array;
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
    }

    public int Count => _array?.Length ?? 0;

    public T this[int index] => AsSpan()[index];

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }

    public static implicit operator EquatableArray<T>(T[] array)
    {
        return new EquatableArray<T>(array);
    }

    public static implicit operator T[](EquatableArray<T> array)
    {
        return array._array ?? Array.Empty<T>();
    }
}
