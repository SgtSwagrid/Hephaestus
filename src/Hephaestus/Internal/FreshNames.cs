namespace Hephaestus;

/// <summary>A name that nobody has taken, and the index it was found at, from which to look for the next one.</summary>
internal readonly record struct FreshName(
    string Name,
    int Index
);

/// <summary>
/// Names for the variables a lowering introduces: a prefix and a counter, skipping whatever names
/// are already spoken for. The counter only ever goes forwards, so two variables introduced by the
/// same pass cannot collide however the modeller has named their own.
/// </summary>
internal static class FreshNames {
    /// <summary>The first of <c>prefix0</c>, <c>prefix1</c>, &#8230; at or after <paramref name="index"/> that <paramref name="isTaken"/> does not claim.</summary>
    public static FreshName After(int index, string prefix, Func<string, bool> isTaken) =>
        Named(prefix, Enumerable.Range(index, int.MaxValue - index).First(candidate => !isTaken(prefix + candidate)));

    private static FreshName Named(string prefix, int index) => new(prefix + index, index);
}
