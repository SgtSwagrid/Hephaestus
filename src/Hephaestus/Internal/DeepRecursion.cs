using System.Runtime.CompilerServices;

namespace Hephaestus;

/// <summary>
/// Expressions are as-written trees, so folding <c>+</c> or <c>&amp;</c> over a long sequence makes a
/// tree as deep as the sequence is long. Recursive passes stay natural by continuing on a fresh
/// stack whenever the current one is nearly exhausted, instead of overflowing. Public so that
/// solver backends which walk expressions themselves can do the same.
/// </summary>
public static class DeepRecursion {
    /// <summary>Calls <paramref name="function"/>, on a fresh stack if the current one is running low.</summary>
    public static TResult Guard<TArgument, TResult>(Func<TArgument, TResult> function, TArgument argument) =>
        RuntimeHelpers.TryEnsureSufficientExecutionStack()
            ? function(argument)
            : OnFreshStack(function, argument);

    // Kept separate so that the closure is only allocated on the rare path.
    private static TResult OnFreshStack<TArgument, TResult>(Func<TArgument, TResult> function, TArgument argument) =>
        Task.Factory
            .StartNew(() => function(argument), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .GetAwaiter()
            .GetResult();
}
