namespace Niddy.Core.Results;

/// <summary>
/// A value that may be absent, for when null is ambiguous or easy to forget to check. A value converts to it
/// implicitly (null becomes none), and <c>return Option.None;</c> works for any <see cref="Option{T}" />.
/// <c>default</c> is none.
/// </summary>
/// <example>
/// <code>
/// Option&lt;User&gt; Find(string name) =&gt; _users.TryGetValue(name, out User? user) ? user : Option.None;
///
/// string greeting = Find(name).Map(u =&gt; $"Hi {u.DisplayName}").GetValueOrDefault("Hi there");
/// </code>
/// </example>
public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly T _value;

    private readonly bool _hasValue;

    /// <summary>No value.</summary>
    public static Option<T> None => default;

    /// <summary>Whether there is a value.</summary>
    public bool HasValue => this._hasValue;

    /// <summary>Whether there is no value.</summary>
    public bool IsNone => !this._hasValue;

    /// <summary>The value. Throws <see cref="InvalidOperationException" /> if there is none.</summary>
    public T Value => this._hasValue ? this._value : throw new InvalidOperationException("The option has no value.");

    private Option(T value)
    {
        this._value = value;
        this._hasValue = true;
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
        value = this._value;
        return this._hasValue;
    }

    /// <summary>The value, or <paramref name="fallback" /> if there is none.</summary>
    public T GetValueOrDefault(T fallback) => this._hasValue ? this._value : fallback;

    /// <summary>The value, or <c>default</c> if there is none.</summary>
    public T? GetValueOrDefault() => this._hasValue ? this._value : default;

    /// <summary>Returns <paramref name="some" /> or <paramref name="none" />'s value, depending on whether there is a value.</summary>
    public TOut Match<TOut>(Func<T, TOut> some, Func<TOut> none) => this._hasValue ? some(this._value) : none();

    /// <summary>Transforms the value if there is one. A null result becomes none.</summary>
    public Option<TOut> Map<TOut>(Func<T, TOut?> map) => this._hasValue ? map(this._value) : Option<TOut>.None;

    /// <summary>Runs <paramref name="next" /> with the value if there is one.</summary>
    public Option<TOut> Bind<TOut>(Func<T, Option<TOut>> next) => this._hasValue ? next(this._value) : Option<TOut>.None;

    /// <summary>Keeps the value only if it satisfies <paramref name="predicate" />.</summary>
    public Option<T> Where(Func<T, bool> predicate) => this._hasValue && predicate(this._value) ? this : None;

    /// <summary>This option if it has a value; otherwise <paramref name="alternative" />'s.</summary>
    public Option<T> Or(Func<Option<T>> alternative) => this._hasValue ? this : alternative();

    /// <summary>Runs <paramref name="action" /> with the value if there is one, and returns this option.</summary>
    public Option<T> IfSome(Action<T> action)
    {
        if (this._hasValue)
        {
            action(this._value);
        }

        return this;
    }

    /// <summary>Success with the value, or failure with <paramref name="error" /> if there is none.</summary>
    public Result<T> ToResult(Error error) => this._hasValue ? Result<T>.Success(this._value) : Result<T>.Failure(error);

    /// <summary>An option with <paramref name="value" />, or none if it is null.</summary>
    public static implicit operator Option<T>(T? value) => value == null ? None : new Option<T>(value);

    /// <summary>No value.</summary>
    public static implicit operator Option<T>(NoneOption _) => None;

    /// <inheritdoc />
    public bool Equals(Option<T> other) =>
        this._hasValue == other._hasValue && (!this._hasValue || EqualityComparer<T>.Default.Equals(this._value, other._value));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Option<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => this._hasValue ? HashCode.Combine(true, this._value) : 0;

    /// <summary>Whether two options are equal.</summary>
    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);

    /// <summary>Whether two options differ.</summary>
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => this._hasValue ? $"Some({this._value})" : "None";
}

/// <summary>Creates <see cref="Option{T}" />s.</summary>
public static class Option
{
    /// <summary>No value; converts to any <see cref="Option{T}" />.</summary>
    public static NoneOption None => default;

    /// <summary>An option with <paramref name="value" />. Throws if it is null.</summary>
    public static Option<T> Some<T>(T value) => Option<T>.Some(value);

    /// <summary>An option with <paramref name="value" />, or none if it is null.</summary>
    public static Option<T> From<T>(T? value) where T : class => value;

    /// <summary>An option with <paramref name="value" />, or none if it is null.</summary>
    public static Option<T> From<T>(T? value) where T : struct => value.HasValue ? Option<T>.Some(value.Value) : Option<T>.None;

    /// <summary>The value for <paramref name="key" />, or none if the dictionary doesn't have it.</summary>
    public static Option<TValue> GetOption<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
        dictionary.TryGetValue(key, out TValue? value) ? value : Option<TValue>.None;

    /// <summary>The first element, or none if there are no elements.</summary>
    public static Option<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        foreach (T item in source)
        {
            return item;
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

/// <summary>The type of <see cref="Option.None" />, which converts to any <see cref="Option{T}" />.</summary>
public readonly struct NoneOption;
