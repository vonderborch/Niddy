using Microsoft.Extensions.Logging;

namespace Niddy.Core.Logging.Batching;

/// <summary>One logged message, as queued by a <see cref="BatchingLoggerProvider{TOptions}"/> and handed to its writer.</summary>
/// <param name="Timestamp">When it was logged, in UTC or local time per <see cref="BatchingLoggerOptions.UseUtc"/>.</param>
/// <param name="Level">The level it was logged at.</param>
/// <param name="Category">The logger's category, usually the type name of the class that logged it.</param>
/// <param name="EventId">The event ID passed to the logger, if any.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The exception passed to the logger, if any.</param>
/// <param name="Scopes">
/// The active scopes, outermost first, when <see cref="BatchingLoggerOptions.IncludeScopes"/> is on; otherwise empty.
/// </param>
/// <param name="Properties">
/// The structured values of the message template (e.g. <c>Count</c> for <c>"Synced {Count} items"</c>, plus
/// <c>{OriginalFormat}</c>), or empty if the message wasn't logged with a template.
/// </param>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyList<object?> Scopes,
    IReadOnlyList<KeyValuePair<string, object?>> Properties);
