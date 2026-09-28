using System.Collections.Immutable;

namespace FancyEnumGenerator.Internal;

internal static class EquatableArrayExtensions
{
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T>
    {
        return new EquatableArray<T>(source);
    }

    public static EquatableImmutableArray<T> ToEquatableImmutableArray<T>(this IEnumerable<T> source)
        where T : IEquatable<T>
    {
        return new EquatableImmutableArray<T>(source);
    }

    public static EquatableImmutableArray<T> ToEquatableImmutableArray<T>(this ImmutableArray<T> source)
        where T : IEquatable<T>
    {
        return new EquatableImmutableArray<T>(source);
    }
}
