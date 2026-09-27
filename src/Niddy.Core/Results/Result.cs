namespace Niddy.Results;

/// <summary>
/// The outcome of an operation with no value: success, or failure with an <see cref="Results.Error"/>. Return it
/// instead of throwing for expected failures, such as validation or a missing file. <c>default</c> is a failure
/// with <see cref="Error.Uninitialized"/>.
/// </summary>
/// <example>
/// <code>
/// Result Save(Settings settings) =>
///     settings.Name.Length == 0 ? Result.Failure("A name is required.") : Result.Try(() => store.Write(settings));
/// </code>
/// </example>
public readonly struct Result : IEquatable<Result>
{
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
        _error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>Why the operation failed, or null if it succeeded.</summary>
    public Error? Error => _isSuccess ? null : _error ?? Error.Uninitialized;

    /// <summary>A successful result.</summary>
    public static Result Success() => new(true, null);

    /// <summary>A successful result with <paramref name="value"/>.</summary>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>A failed result.</summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }

    /// <summary>A failed result with a message and optional code.</summary>
    public static Result Failure(string message, string? code = null) => Failure(new Error(message, code));

    /// <summary>Runs <paramref name="action"/>, returning a failure with the exception if it throws.</summary>
    public static Result Try(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(Error.FromException(ex));
        }
    }

    /// <summary>Runs <paramref name="func"/>, returning a failure with the exception if it throws.</summary>
    public static Result<T> Try<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Result<T>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/>, returning a failure with the exception if it throws. Cancellation is still thrown.
    /// </summary>
    public static async Task<Result> TryAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action();
            return Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Runs <paramref name="func"/>, returning a failure with the exception if it throws. Cancellation is still thrown.
    /// </summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Result<T>.Success(await func());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<T>.Failure(Error.FromException(ex));
        }
    }

    /// <summary>Returns the first failure, or success if every result succeeded.</summary>
    public static Result Combine(params IEnumerable<Result> results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return result;
        }

        return Success();
    }

    /// <summary>Returns <paramref name="onSuccess"/> or <paramref name="onFailure"/>'s value, depending on the outcome.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        _isSuccess ? onSuccess() : onFailure(Error!);

    /// <summary>Runs <paramref name="next"/> if this succeeded; otherwise keeps this failure.</summary>
    public Result Bind(Func<Result> next) => _isSuccess ? next() : this;

    /// <summary>Runs <paramref name="next"/> if this succeeded; otherwise keeps this failure.</summary>
    public Result<T> Bind<T>(Func<Result<T>> next) => _isSuccess ? next() : Result<T>.Failure(Error!);

    /// <summary>Runs <paramref name="action"/> if this succeeded, and returns this result.</summary>
    public Result OnSuccess(Action action)
    {
        if (_isSuccess)
            action();
        return this;
    }

    /// <summary>Runs <paramref name="action"/> with the error if this failed, and returns this result.</summary>
    public Result OnFailure(Action<Error> action)
    {
        if (!_isSuccess)
            action(Error!);
        return this;
    }

    /// <summary>Throws <see cref="ResultException"/> if this failed.</summary>
    public void ThrowIfFailure()
    {
        if (!_isSuccess)
            throw new ResultException(Error!);
    }

    /// <summary>A failed result.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <inheritdoc />
    public bool Equals(Result other) => _isSuccess == other._isSuccess && Equals(Error, other.Error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_isSuccess, Error);

    /// <summary>Whether two results are equal.</summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Whether two results differ.</summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _isSuccess ? "Success" : $"Failure({Error})";
}

/// <summary>
/// The outcome of an operation that produces a <typeparamref name="T"/>: success with a value, or failure with an
/// <see cref="Results.Error"/>. A value or an <see cref="Results.Error"/> converts to it implicitly, so a method can
/// <c>return value;</c> or <c>return new Error("...");</c>. <c>default</c> is a failure with
/// <see cref="Error.Uninitialized"/>.
/// </summary>
/// <example>
/// <code>
/// Result&lt;int&gt; ParseAge(string text) =>
///     int.TryParse(text, out var age) &amp;&amp; age >= 0 ? age : new Error("Enter a whole number.", "Invalid");
///
/// var message = ParseAge(input).Match(age => $"Age {age}", error => error.Message);
/// </code>
/// </example>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T _value;
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, T value, Error? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>The value. Throws <see cref="ResultException"/> if the operation failed.</summary>
    public T Value => _isSuccess ? _value : throw new ResultException(Error!);

    /// <summary>Why the operation failed, or null if it succeeded.</summary>
    public Error? Error => _isSuccess ? null : _error ?? Error.Uninitialized;

    /// <summary>A successful result with <paramref name="value"/>.</summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>A failed result.</summary>
    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default!, error);
    }

    /// <summary>A failed result with a message and optional code.</summary>
    public static Result<T> Failure(string message, string? code = null) => Failure(new Error(message, code));

    /// <summary>Gets the value if the operation succeeded.</summary>
    public bool TryGetValue(out T value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <summary>Gets the value if the operation succeeded, or the error if it failed.</summary>
    public bool TryGetValue(out T value, out Error? error)
    {
        value = _value;
        error = Error;
        return _isSuccess;
    }

    /// <summary>The value, or <paramref name="fallback"/> if the operation failed.</summary>
    public T GetValueOrDefault(T fallback) => _isSuccess ? _value : fallback;

    /// <summary>The value, or <c>default</c> if the operation failed.</summary>
    public T? GetValueOrDefault() => _isSuccess ? _value : default;

    /// <summary>Returns <paramref name="onSuccess"/> or <paramref name="onFailure"/>'s value, depending on the outcome.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        _isSuccess ? onSuccess(_value) : onFailure(Error!);

    /// <summary>Transforms the value if this succeeded; otherwise keeps this failure.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        _isSuccess ? Result<TOut>.Success(map(_value)) : Result<TOut>.Failure(Error!);

    /// <summary>Transforms the error if this failed; otherwise keeps the value.</summary>
    public Result<T> MapError(Func<Error, Error> map) => _isSuccess ? this : Failure(map(Error!));

    /// <summary>Runs <paramref name="next"/> with the value if this succeeded; otherwise keeps this failure.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) =>
        _isSuccess ? next(_value) : Result<TOut>.Failure(Error!);

    /// <summary>Runs <paramref name="next"/> with the value if this succeeded; otherwise keeps this failure.</summary>
    public Result Bind(Func<T, Result> next) => _isSuccess ? next(_value) : Result.Failure(Error!);

    /// <summary>Transforms the value asynchronously if this succeeded; otherwise keeps this failure.</summary>
    public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) =>
        _isSuccess ? Result<TOut>.Success(await map(_value)) : Result<TOut>.Failure(Error!);

    /// <summary>Runs <paramref name="next"/> with the value asynchronously if this succeeded; otherwise keeps this failure.</summary>
    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> next) =>
        _isSuccess ? next(_value) : Task.FromResult(Result<TOut>.Failure(Error!));

    /// <summary>Keeps the value if it satisfies <paramref name="predicate"/>; otherwise fails with <paramref name="error"/>.</summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        !_isSuccess || predicate(_value) ? this : Failure(error);

    /// <summary>Runs <paramref name="action"/> with the value if this succeeded, and returns this result.</summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (_isSuccess)
            action(_value);
        return this;
    }

    /// <summary>Runs <paramref name="action"/> with the error if this failed, and returns this result.</summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (!_isSuccess)
            action(Error!);
        return this;
    }

    /// <summary>The value as an <see cref="Option{T}"/>: some if this succeeded, none if it failed.</summary>
    public Option<T> ToOption() => _isSuccess ? Option<T>.Some(_value) : Option<T>.None;

    /// <summary>This result without its value.</summary>
    public Result ToResult() => _isSuccess ? Result.Success() : Result.Failure(Error!);

    /// <summary>Deconstructs into whether it succeeded, the value and the error.</summary>
    public void Deconstruct(out bool isSuccess, out T? value, out Error? error)
    {
        isSuccess = _isSuccess;
        value = _isSuccess ? _value : default;
        error = Error;
    }

    /// <summary>A successful result with <paramref name="value"/>.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>A failed result.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>This result without its value.</summary>
    public static implicit operator Result(Result<T> result) => result.ToResult();

    /// <inheritdoc />
    public bool Equals(Result<T> other) =>
        _isSuccess == other._isSuccess
        && (_isSuccess ? EqualityComparer<T>.Default.Equals(_value, other._value) : Equals(Error, other.Error));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _isSuccess ? HashCode.Combine(true, _value) : HashCode.Combine(false, Error);

    /// <summary>Whether two results are equal.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Whether two results differ.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => _isSuccess ? $"Success({_value})" : $"Failure({Error})";
}

/// <summary>Thrown when the value of a failed <see cref="Result{T}"/> is read, or by <see cref="Result.ThrowIfFailure"/>.</summary>
public sealed class ResultException(Error error) : InvalidOperationException(error.Message, error.Exception)
{
    /// <summary>Why the operation failed.</summary>
    public Error Error { get; } = error;
}
