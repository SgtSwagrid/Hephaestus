namespace Hephaestus;

internal static class Balanced {
    /// <summary>Combines the items pairwise into a tree of logarithmic depth.</summary>
    public static T Fold<T>(IReadOnlyList<T> items, T empty, Func<T, T, T> combine) =>
        items.Count == 0 ? empty : FoldRange(items, 0, items.Count, combine);

    private static T FoldRange<T>(IReadOnlyList<T> items, int start, int end, Func<T, T, T> combine) =>
        end - start == 1
            ? items[start]
            : combine(
                FoldRange(items, start, (start + end) / 2, combine),
                FoldRange(items, (start + end) / 2, end, combine));
}
