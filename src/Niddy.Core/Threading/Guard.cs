using System.Diagnostics;

namespace Niddy.Threading;

/// <summary>
/// A thread-safe boolean flag. Backed by an int and manipulated via <see cref="Interlocked"/>.
/// </summary>
[DebuggerDisplay("State = {Check}")]
public struct Guard : IEquatable<Guard>
{
    private const int False = 0;
    private const int True = 1;

    private int _state = False;

    public Guard() { }

    /// <summary>
    /// Returns true if the guard is set. Does not modify state.
    /// </summary>
    public bool Check => _state == True;

    /// <summary>
    /// Returns true and atomically sets the guard if it was previously unset. Returns false if already set.
    /// </summary>
    public bool CheckSet => Interlocked.Exchange(ref _state, True) == False;

    /// <summary>
    /// Sets the guard to true.
    /// </summary>
    public void MarkChecked() => Interlocked.Exchange(ref _state, True);

    /// <summary>
    /// Resets the guard to false.
    /// </summary>
    public void Reset() => Interlocked.Exchange(ref _state, False);

    public bool Equals(Guard other) => _state == other._state;
    public override bool Equals(object? obj) => obj is Guard other && Equals(other);
    public override int GetHashCode() => _state;

    public static bool operator ==(Guard left, bool right) => left.Check == right;
    public static bool operator ==(bool left, Guard right) => left == right.Check;
    public static bool operator !=(Guard left, bool right) => left.Check != right;
    public static bool operator !=(bool left, Guard right) => left != right.Check;
}
