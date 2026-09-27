namespace Niddy.Results;

/// <summary>Why an operation failed: a message, an optional machine-readable code, and the exception behind it if any.</summary>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Code">An optional code for callers to switch on, e.g. <c>"NotFound"</c>.</param>
/// <param name="Exception">The exception behind the failure, if there was one.</param>
public sealed record Error(string Message, string? Code = null, Exception? Exception = null)
{
    /// <summary>The error of a result created with <c>default</c> rather than a factory method.</summary>
    public static Error Uninitialized { get; } = new("The result wasn't initialized.", "Uninitialized");

    /// <summary>Creates an error from an exception, with the exception's message and type name as the code.</summary>
    public static Error FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new Error(exception.Message, exception.GetType().Name, exception);
    }

    /// <summary>Creates an error from a message.</summary>
    public static implicit operator Error(string message) => new(message);

    /// <inheritdoc />
    public override string ToString() => Code is null ? Message : $"{Code}: {Message}";
}
