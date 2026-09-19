using System.Diagnostics;

namespace Hephaestus;

/// <summary>Reading the clock is a side effect; this is the one place it happens.</summary>
public static class Timed {
    /// <summary>The result of <paramref name="function"/>, and how long it took.</summary>
    public static (T Result, TimeSpan Elapsed) Run<T>(Func<T> function) {
        var stopwatch = Stopwatch.StartNew();
        var result = function();
        return (result, stopwatch.Elapsed);
    }
}
