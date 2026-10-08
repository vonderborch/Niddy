namespace Niddy.Core;

/// <summary>
/// Extensions for disposing values that may or may not be <see cref="IDisposable"/>.
/// </summary>
public static class DisposableExtensions
{
    extension<T>(T value)
    {
        /// <summary>
        /// Disposes the value if it implements <see cref="IDisposable" />. Does nothing otherwise, including for null.
        /// </summary>
        public void DisposeIfPossible()
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
