using System.Numerics;

namespace Niddy.Core.Threading;

/// <summary>
/// Atomic operations for use in multithreaded scenarios.
/// </summary>
public static class AtomicOperations
{
    /// <summary>
    /// Atomically sets <paramref name="location" /> to <paramref name="newValue" /> if its current value equals
    /// <paramref name="expected" />. Returns true if the swap occurred.
    /// </summary>
    public static bool CompareAndSwap<T>(ref T location, T newValue, T expected)
    {
        ArgumentNullException.ThrowIfNull(location);
        T value = Interlocked.CompareExchange(ref location!, newValue, expected);
        bool output = EqualityComparer<T>.Default.Equals(value, expected);
        return output;
    }

    /// <summary>
    /// Atomically sets <paramref name="location" /> to <paramref name="newValue" /> if its current value equals
    /// <paramref name="expected" />. Returns true if the swap occurred and outputs the original value.
    /// </summary>
    public static bool CompareAndSwap<T>(ref T location, T newValue, T expected, out T original)
    {
        ArgumentNullException.ThrowIfNull(location);
        original = Interlocked.CompareExchange(ref location!, newValue, expected);
        bool output = EqualityComparer<T>.Default.Equals(original, expected);
        return output;
    }

    /// <summary>
    /// Atomically increments <paramref name="variable" /> by one, clamping at <paramref name="maximum" />.
    /// Returns the value before modification.
    /// </summary>
    public static T Increment<T>(ref T variable, T maximum) where T : INumber<T>
    {
        T original;
        while (true)
        {
            T val = variable;
            if (val == maximum)
            {
                return val;
            }
            if (val > maximum)
            {
                CompareAndSwap(ref variable, maximum, val);
            }
            else if (CompareAndSwap(ref variable, val + T.One, val, out original))
            {
                break;
            }
        }
        return original;
    }

    /// <summary>
    /// Atomically decrements <paramref name="variable" /> by one, clamping at <paramref name="minimum" />.
    /// Returns the value before modification.
    /// </summary>
    public static T Decrement<T>(ref T variable, T minimum) where T : INumber<T>
    {
        T original;
        while (true)
        {
            T val = variable;
            if (val == minimum)
            {
                return val;
            }
            if (val < minimum)
            {
                CompareAndSwap(ref variable, minimum, val);
            }
            else if (CompareAndSwap(ref variable, val - T.One, val, out original))
            {
                break;
            }
        }
        return original;
    }
}
