namespace Niddy.Avalonia.Tests;

/// <summary>
///     Tests that change app-wide state (the page sort, the toast fallback host). They run on their own, after the
///     other tests, so they can't change what those see.
/// </summary>
[CollectionDefinition(DisableParallelization = true)]
public sealed class GlobalStateCollection;
