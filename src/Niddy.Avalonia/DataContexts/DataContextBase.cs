using ReactiveUI;

namespace Niddy.Avalonia.DataContexts;

/// <summary>
///     Base class for a view's data context (what MVVM calls a view-model), built on ReactiveUI. Name the
///     class after its view, e.g. <c>SettingsPage</c> and <c>SettingsPageDataContext</c>.
///     <para>
///         Provides <see cref="IsBusy"/> / <see cref="ErrorMessage"/> state and a <see cref="RunAsync"/>
///         helper that manages both automatically. Declare properties with
///         <c>set => this.RaiseAndSetIfChanged(ref field, value);</c>.
///     </para>
/// </summary>
public abstract class DataContextBase : ReactiveObject
{
    /// <summary>True while a <see cref="RunAsync"/> call is executing.</summary>
    public bool IsBusy
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     The message from the most recent unhandled exception inside <see cref="RunAsync"/>,
    ///     or null if the last run succeeded.
    /// </summary>
    public string? ErrorMessage
    {
        get;
        protected set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     Executes <paramref name="work"/> on the current synchronisation context, setting
    ///     <see cref="IsBusy"/> for its duration. On failure, <paramref name="onError"/> is invoked
    ///     if supplied; otherwise <see cref="ErrorMessage"/> is set to the exception message.
    /// </summary>
    protected async Task RunAsync(Func<Task> work, Action<Exception>? onError = null)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            if (onError is not null)
                onError(ex);
            else
                ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    ///     Executes <paramref name="work"/> and returns its result, or <c>default</c> on failure.
    ///     <see cref="IsBusy"/> is set for the duration; errors are handled like the void overload.
    /// </summary>
    protected async Task<T?> RunAsync<T>(Func<Task<T>> work, Action<Exception>? onError = null)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            return await work();
        }
        catch (Exception ex)
        {
            if (onError is not null)
                onError(ex);
            else
                ErrorMessage = ex.Message;
            return default;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
