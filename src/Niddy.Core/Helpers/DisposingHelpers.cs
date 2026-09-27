namespace Niddy.Helpers;

/// <summary>
/// Helper methods for safely disposing objects.
/// </summary>
public static class DisposingHelpers
{
    /// <summary>
    /// Disposes <paramref name="obj" /> if it implements <see cref="IDisposable" />. No-ops otherwise.
    /// </summary>
    public static void DisposeIfPossible<T>(T obj)
    {
        (obj as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Disposes this object if it implements <see cref="IDisposable" />. No-ops otherwise.
    /// </summary>
    public static void DisposeIfPossible(this object obj)
    {
        (obj as IDisposable)?.Dispose();
    }
}
