using System.Collections;
using System.Collections.Immutable;

namespace FancyEnumGenerator.Internal;

internal readonly struct EquatableImmutableArray<T> : IEquatable<EquatableImmutableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableImmutableArray<T> Empty = new(ImmutableArray<T>.Empty);

    private readonly ImmutableArray<T> _array;

    public EquatableImmutableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    public EquatableImmutableArray(IEnumerable<T> source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _array = source is ImmutableArray<T> array ? array : ImmutableArray.CreateRange(source);
    }

    public bool Equals(EquatableImmutableArray<T> other)
    {
        return AsSpan().SequenceEqual(other.AsSpan());
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableImmutableArray<T> other && Equals(other);
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

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return ((IEnumerable<T>)(_array.IsDefault ? ImmutableArray<T>.Empty : _array)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)(_array.IsDefault ? ImmutableArray<T>.Empty : _array)).GetEnumerator();
    }

    public int Count => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => AsSpan()[index];

    public static bool operator ==(EquatableImmutableArray<T> left, EquatableImmutableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableImmutableArray<T> left, EquatableImmutableArray<T> right)
    {
        return !left.Equals(right);
    }

    public static implicit operator EquatableImmutableArray<T>(ImmutableArray<T> array)
    {
        return new EquatableImmutableArray<T>(array);
    }

    public static implicit operator ImmutableArray<T>(EquatableImmutableArray<T> array)
    {
        return array._array;
    }
}
