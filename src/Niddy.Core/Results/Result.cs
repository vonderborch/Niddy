namespace Niddy.Core.Results;

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
        this._isSuccess = isSuccess;
        this._error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => this._isSuccess;

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !this._isSuccess;

    /// <summary>Why the operation failed, or null if it succeeded.</summary>
    public Error? Error => this._isSuccess ? null : this._error ?? Error.Uninitialized;

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
        foreach (Result result in results)
        {
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Success();
    }

    /// <summary>Returns <paramref name="onSuccess"/> or <paramref name="onFailure"/>'s value, depending on the outcome.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        this._isSuccess ? onSuccess() : onFailure(this.Error!);

    /// <summary>Runs <paramref name="next"/> if this succeeded; otherwise keeps this failure.</summary>
    public Result Bind(Func<Result> next) => this._isSuccess ? next() : this;

    /// <summary>Runs <paramref name="next"/> if this succeeded; otherwise keeps this failure.</summary>
    public Result<T> Bind<T>(Func<Result<T>> next) => this._isSuccess ? next() : Result<T>.Failure(this.Error!);

    /// <summary>Runs <paramref name="action"/> if this succeeded, and returns this result.</summary>
    public Result OnSuccess(Action action)
    {
        if (this._isSuccess)
        {
            action();
        }

        return this;
    }

    /// <summary>Runs <paramref name="action"/> with the error if this failed, and returns this result.</summary>
    public Result OnFailure(Action<Error> action)
    {
        if (!this._isSuccess)
        {
            action(this.Error!);
        }

        return this;
    }

    /// <summary>Throws <see cref="ResultException"/> if this failed.</summary>
    public void ThrowIfFailure()
    {
        if (!this._isSuccess)
        {
            throw new ResultException(this.Error!);
        }
    }

    /// <summary>A failed result.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <inheritdoc />
    public bool Equals(Result other) => this._isSuccess == other._isSuccess && Equals(this.Error, other.Error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(this._isSuccess, this.Error);

    /// <summary>Whether two results are equal.</summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Whether two results differ.</summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => this._isSuccess ? "Success" : $"Failure({this.Error})";
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
///     int.TryParse(text, out int age) &amp;&amp; age >= 0 ? age : new Error("Enter a whole number.", "Invalid");
///
/// string message = ParseAge(input).Match(age => $"Age {age}", error => error.Message);
/// </code>
/// </example>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T _value;
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, T value, Error? error)
    {
        this._isSuccess = isSuccess;
        this._value = value;
        this._error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => this._isSuccess;

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !this._isSuccess;

    /// <summary>The value. Throws <see cref="ResultException"/> if the operation failed.</summary>
    public T Value => this._isSuccess ? this._value : throw new ResultException(this.Error!);

    /// <summary>Why the operation failed, or null if it succeeded.</summary>
    public Error? Error => this._isSuccess ? null : this._error ?? Error.Uninitialized;

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
        value = this._value;
        return this._isSuccess;
    }

    /// <summary>Gets the value if the operation succeeded, or the error if it failed.</summary>
    public bool TryGetValue(out T value, out Error? error)
    {
        value = this._value;
        error = this.Error;
        return this._isSuccess;
    }

    /// <summary>The value, or <paramref name="fallback"/> if the operation failed.</summary>
    public T GetValueOrDefault(T fallback) => this._isSuccess ? this._value : fallback;

    /// <summary>The value, or <c>default</c> if the operation failed.</summary>
    public T? GetValueOrDefault() => this._isSuccess ? this._value : default;

    /// <summary>Returns <paramref name="onSuccess"/> or <paramref name="onFailure"/>'s value, depending on the outcome.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        this._isSuccess ? onSuccess(this._value) : onFailure(this.Error!);

    /// <summary>Transforms the value if this succeeded; otherwise keeps this failure.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        this._isSuccess ? Result<TOut>.Success(map(this._value)) : Result<TOut>.Failure(this.Error!);

    /// <summary>Transforms the error if this failed; otherwise keeps the value.</summary>
    public Result<T> MapError(Func<Error, Error> map) => this._isSuccess ? this : Failure(map(this.Error!));

    /// <summary>Runs <paramref name="next"/> with the value if this succeeded; otherwise keeps this failure.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> next) =>
        this._isSuccess ? next(this._value) : Result<TOut>.Failure(this.Error!);

    /// <summary>Runs <paramref name="next"/> with the value if this succeeded; otherwise keeps this failure.</summary>
    public Result Bind(Func<T, Result> next) => this._isSuccess ? next(this._value) : Result.Failure(this.Error!);

    /// <summary>Transforms the value asynchronously if this succeeded; otherwise keeps this failure.</summary>
    public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) =>
        this._isSuccess ? Result<TOut>.Success(await map(this._value)) : Result<TOut>.Failure(this.Error!);

    /// <summary>Runs <paramref name="next"/> with the value asynchronously if this succeeded; otherwise keeps this failure.</summary>
    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> next) =>
        this._isSuccess ? next(this._value) : Task.FromResult(Result<TOut>.Failure(this.Error!));

    /// <summary>Keeps the value if it satisfies <paramref name="predicate"/>; otherwise fails with <paramref name="error"/>.</summary>
    public Result<T> Ensure(Func<T, bool> predicate, Error error) =>
        !this._isSuccess || predicate(this._value) ? this : Failure(error);

    /// <summary>Runs <paramref name="action"/> with the value if this succeeded, and returns this result.</summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (this._isSuccess)
        {
            action(this._value);
        }

        return this;
    }

    /// <summary>Runs <paramref name="action"/> with the error if this failed, and returns this result.</summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (!this._isSuccess)
        {
            action(this.Error!);
        }

        return this;
    }

    /// <summary>The value as an <see cref="Option{T}"/>: some if this succeeded, none if it failed.</summary>
    public Option<T> ToOption() => this._isSuccess ? Option<T>.Some(this._value) : Option<T>.None;

    /// <summary>This result without its value.</summary>
    public Result ToResult() => this._isSuccess ? Result.Success() : Result.Failure(this.Error!);

    /// <summary>Deconstructs into whether it succeeded, the value and the error.</summary>
    public void Deconstruct(out bool isSuccess, out T? value, out Error? error)
    {
        isSuccess = this._isSuccess;
        value = this._isSuccess ? this._value : default;
        error = this.Error;
    }

    /// <summary>A successful result with <paramref name="value"/>.</summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>A failed result.</summary>
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>This result without its value.</summary>
    public static implicit operator Result(Result<T> result) => result.ToResult();

    /// <inheritdoc />
    public bool Equals(Result<T> other) =>
        this._isSuccess == other._isSuccess
        && (this._isSuccess ? EqualityComparer<T>.Default.Equals(this._value, other._value) : Equals(this.Error, other.Error));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => this._isSuccess ? HashCode.Combine(true, this._value) : HashCode.Combine(false, this.Error);

    /// <summary>Whether two results are equal.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Whether two results differ.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => this._isSuccess ? $"Success({this._value})" : $"Failure({this.Error})";
}

/// <summary>Thrown when the value of a failed <see cref="Result{T}"/> is read, or by <see cref="Result.ThrowIfFailure"/>.</summary>
public sealed class ResultException(Error error) : InvalidOperationException(error.Message, error.Exception)
{
    /// <summary>Why the operation failed.</summary>
    public Error Error { get; } = error;
}
