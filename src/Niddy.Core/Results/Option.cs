using System.Runtime.InteropServices;

namespace Niddy.Results;

/// <summary>
/// A value that may be absent, for when null is ambiguous or easy to forget to check. A value converts to it
/// implicitly (null becomes none), and <c>return Option.None;</c> works for any <see cref="Option{T}" />.
/// <c>default</c> is none.
/// </summary>
/// <example>
/// <code>
/// Option&lt;User&gt; Find(string name) =&gt; _users.TryGetValue(name, out var user) ? user : Option.None;
///
/// var greeting = Find(name).Map(u =&gt; $"Hi {u.DisplayName}").GetValueOrDefault("Hi there");
/// </code>
/// </example>
public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly T _value;

    private readonly bool _hasValue;

    /// <summary>No value.</summary>
    public static Option<T> None => default(Option<T>);

    /// <summary>Whether there is a value.</summary>
    public bool HasValue => _hasValue;

    /// <summary>Whether there is no value.</summary>
    public bool IsNone => !_hasValue;

    /// <summary>The value. Throws <see cref="InvalidOperationException" /> if there is none.</summary>
    public T Value
    {
        get
        {
            if (!_hasValue)
            {
                throw new InvalidOperationException("The option has no value.");
            }
            return _value;
        }
    }

    private Option(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>An option with <paramref name="value" />. Throws if it is null; use the implicit conversion to allow null.</summary>
    public static Option<T> Some(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Option<T>(value);
    }

    /// <summary>Gets the value if there is one.</summary>
    public bool TryGetValue(out T value)
    {
        value = _value;
        return _hasValue;
    }

    /// <summary>The value, or <paramref name="fallback" /> if there is none.</summary>
    public T GetValueOrDefault(T fallback)
    {
        return (T)(_hasValue ? ((object)_value) : ((object)fallback));
    }

    /// <summary>The value, or <c>default</c> if there is none.</summary>
    public T? GetValueOrDefault()
    {
        return _hasValue ? _value : default(T);
    }

    /// <summary>Returns <paramref name="some" /> or <paramref name="none" />'s value, depending on whether there is a value.</summary>
    public TOut Match<TOut>(Func<T, TOut> some, Func<TOut> none)
    {
        return _hasValue ? some(_value) : none();
    }

    /// <summary>Transforms the value if there is one. A null result becomes none.</summary>
    public Option<TOut> Map<TOut>(Func<T, TOut?> map)
    {
        return _hasValue ? ((Option<TOut>)map(_value)) : Option<TOut>.None;
    }

    /// <summary>Runs <paramref name="next" /> with the value if there is one.</summary>
    public Option<TOut> Bind<TOut>(Func<T, Option<TOut>> next)
    {
        return _hasValue ? next(_value) : Option<TOut>.None;
    }

    /// <summary>Keeps the value only if it satisfies <paramref name="predicate" />.</summary>
    public Option<T> Where(Func<T, bool> predicate)
    {
        return (_hasValue && predicate(_value)) ? this : None;
    }

    /// <summary>This option if it has a value; otherwise <paramref name="alternative" />'s.</summary>
    public Option<T> Or(Func<Option<T>> alternative)
    {
        return _hasValue ? this : alternative();
    }

    /// <summary>Runs <paramref name="action" /> with the value if there is one, and returns this option.</summary>
    public Option<T> IfSome(Action<T> action)
    {
        if (_hasValue)
        {
            action(_value);
        }
        return this;
    }

    /// <summary>Success with the value, or failure with <paramref name="error" /> if there is none.</summary>
    public Result<T> ToResult(Error error)
    {
        return _hasValue ? Result<T>.Success(_value) : Result<T>.Failure(error);
    }

    /// <summary>An option with <paramref name="value" />, or none if it is null.</summary>
    public static implicit operator Option<T>(T? value)
    {
        return (value == null) ? None : new Option<T>(value);
    }

    /// <summary>No value.</summary>
    public static implicit operator Option<T>(NoneOption _)
    {
        return None;
    }

    /// <inheritdoc />
    public bool Equals(Option<T> other)
    {
        return _hasValue == other._hasValue && (!_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is Option<T> other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _hasValue ? HashCode.Combine(value1: true, _value) : 0;
    }

    /// <summary>Whether two options are equal.</summary>
    public static bool operator ==(Option<T> left, Option<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>Whether two options differ.</summary>
    public static bool operator !=(Option<T> left, Option<T> right)
    {
        return !left.Equals(right);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _hasValue ? $"Some({_value})" : "None";
    }
}
/// <summary>Creates <see cref="Option{T}" />s.</summary>
public static class Option
{
    /// <summary>No value; converts to any <see cref="Option{T}" />.</summary>
    public static NoneOption None => default(NoneOption);

    /// <summary>An option with <paramref name="value" />. Throws if it is null.</summary>
    public static Option<T> Some<T>(T value)
    {
        return Option<T>.Some(value);
    }

    /// <summary>An option with <paramref name="value" />, or none if it is null.</summary>
    public static Option<T> From<T>(T? value) where T : class
    {
        return value;
    }

    /// <summary>An option with <paramref name="value" />, or none if it is null.</summary>
    public static Option<T> From<T>(T? value) where T : struct
    {
        Option<T> result;
        if (value.HasValue)
        {
            T valueOrDefault = value.GetValueOrDefault();
            result = Option<T>.Some(valueOrDefault);
        }
        else
        {
            result = Option<T>.None;
        }
        return result;
    }

    /// <summary>The value for <paramref name="key" />, or none if the dictionary doesn't have it.</summary>
    public static Option<TValue> GetOption<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
    {
        TValue value;
        return dictionary.TryGetValue(key, out value) ? ((Option<TValue>)value) : Option<TValue>.None;
    }

    /// <summary>The first element, or none if there are no elements.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        using (IEnumerator<T> enumerator = source.GetEnumerator())
        {
            if (enumerator.MoveNext())
            {
                T current = enumerator.Current;
                return current;
            }
        }
        return Option<T>.None;
    }

    /// <summary>The first element that satisfies <paramref name="predicate" />, or none.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        foreach (T item in source)
        {
            if (predicate(item))
            {
                return item;
            }
        }
        return Option<T>.None;
    }
}

/// <summary>The type of <see cref="None" />, which converts to any <see cref="Option{T}" />.</summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly struct NoneOption
{
}
