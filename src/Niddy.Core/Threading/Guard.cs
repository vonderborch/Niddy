using System.Diagnostics;

namespace Niddy.Core.Threading;

/// <summary>
/// A thread-safe boolean flag. Backed by an int and manipulated via <see cref="Interlocked"/>.
/// </summary>
[DebuggerDisplay("State = {IsClaimed}")]
public struct Guard : IEquatable<Guard>
{
    private const int False = 0;
    private const int True = 1;

    private int _state = False;

    /// <summary>Creates an unset guard.</summary>
    public Guard() { }

    /// <summary>
    /// Returns true if the guard is set. Does not modify state.
    /// </summary>
    public bool IsClaimed => this._state == True;

    /// <summary>
    /// Returns true and atomically sets the guard if it was previously unset. Returns false if already set.
    /// </summary>
    public bool TryClaim => Interlocked.Exchange(ref this._state, True) == False;

    /// <summary>
    /// Claims the guard. Avoid using this method if possible. It is better to use <see cref="TryClaim"/>.
    /// This method is unconditional and cannot tell if the guard was previously claimed, or which thread claimed it.
    /// This is useful for overriding the default behavior of <see cref="IsClaimed"/> in controlled situations.
    /// </summary>
    public void Claim() => Interlocked.Exchange(ref this._state, True);

    /// <summary>
    /// Releases the guard.
    /// </summary>
    public void Release() => Interlocked.Exchange(ref this._state, False);

    /// <summary>Whether both guards are in the same state.</summary>
    public bool Equals(Guard other) => this._state == other._state;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Guard other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => this._state;

    /// <summary>Whether the guard's <see cref="IsClaimed"/> equals <paramref name="right"/>.</summary>
    public static bool operator ==(Guard left, bool right) => left.IsClaimed == right;

    /// <summary>Whether the guard's <see cref="IsClaimed"/> equals <paramref name="left"/>.</summary>
    public static bool operator ==(bool left, Guard right) => left == right.IsClaimed;

    /// <summary>Whether the guard's <see cref="IsClaimed"/> differs from <paramref name="right"/>.</summary>
    public static bool operator !=(Guard left, bool right) => left.IsClaimed != right;

    /// <summary>Whether the guard's <see cref="IsClaimed"/> differs from <paramref name="left"/>.</summary>
    public static bool operator !=(bool left, Guard right) => left != right.IsClaimed;
}
