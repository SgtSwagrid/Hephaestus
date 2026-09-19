using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Feasibility-based bound tightening. A row <c>&#931; a&#7522;x&#7522; + c &lt;= 0</c> that always
/// holds bounds each of its variables: <c>a&#11388;x&#11388; &lt;= -c - &#931; min(a&#7522;x&#7522;)</c>
/// over the other terms. A singleton row such as <c>x - 10 &lt;= 0</c> is simply the case with no
/// other terms, so stated bounds and implied bounds come from the same function.
/// </summary>
internal static class BoundPropagation {
    extension(ImmutableDictionary<IVariable, Interval> bounds) {
        /// <summary>The known bounds of a variable, falling back on those of its kind.</summary>
        public Interval Of(IVariable variable) => bounds.GetValueOrDefault(variable, variable.IntrinsicBounds);
    }

    /// <summary>Sweeps over the rows until nothing tightens, the rounds run out, or some interval empties.</summary>
    public static ImmutableDictionary<IVariable, Interval> Propagate(IReadOnlyCollection<GuardedRow> rows, ImmutableDictionary<IVariable, Interval> bounds, int rounds) =>
        rounds <= 0 || bounds.Values.Any(interval => interval.IsEmpty)
            ? bounds
            : Continue(rows, bounds, Sweep(rows, bounds), rounds);

    /// <summary>One pass of tightening over every row.</summary>
    public static ImmutableDictionary<IVariable, Interval> Sweep(IEnumerable<GuardedRow> rows, ImmutableDictionary<IVariable, Interval> bounds) =>
        rows.Aggregate(bounds, Tighten);

    // An unchanged dictionary is returned as the same instance, so reference equality detects the fixed point.
    private static ImmutableDictionary<IVariable, Interval> Continue(IReadOnlyCollection<GuardedRow> rows, ImmutableDictionary<IVariable, Interval> before, ImmutableDictionary<IVariable, Interval> after, int rounds) =>
        ReferenceEquals(before, after) ? after : Propagate(rows, after, rounds - 1);

    private static ImmutableDictionary<IVariable, Interval> Tighten(ImmutableDictionary<IVariable, Interval> bounds, GuardedRow row) =>
        row.IsEquality
            ? TightenBelowZero(TightenBelowZero(bounds, row.Expression), row.Expression.Negated)
            : TightenBelowZero(bounds, row.Expression);

    private sealed record Contribution(
        IVariable Variable,
        double Coefficient,
        double Minimum
    );

    /// <summary>Tightens every variable of <paramref name="form"/>, knowing that <c>form &lt;= 0</c>.</summary>
    private static ImmutableDictionary<IVariable, Interval> TightenBelowZero(ImmutableDictionary<IVariable, Interval> bounds, AffineForm form) {
        var contributions = form.Coefficients
            .Select(term => new Contribution(term.Key, term.Value, bounds.Of(term.Key).Times(term.Value).Lower))
            .ToImmutableArray();
        var unboundedCount = contributions.Count(contribution => double.IsNegativeInfinity(contribution.Minimum));
        var boundedTotal = contributions.Where(contribution => !double.IsNegativeInfinity(contribution.Minimum)).Sum(contribution => contribution.Minimum);
        return unboundedCount > 1
            ? bounds
            : contributions.Aggregate(bounds, (tightened, contribution) =>
                Narrow(tightened, contribution, -form.Constant - MinimumOfOthers(contribution, unboundedCount, boundedTotal)));
    }

    private static double MinimumOfOthers(Contribution contribution, int unboundedCount, double boundedTotal) =>
        unboundedCount == 0 ? boundedTotal - contribution.Minimum
        : double.IsNegativeInfinity(contribution.Minimum) ? boundedTotal
        : double.NegativeInfinity;

    /// <summary>Applies <c>coefficient &#183; variable &lt;= limit</c>.</summary>
    private static ImmutableDictionary<IVariable, Interval> Narrow(ImmutableDictionary<IVariable, Interval> bounds, Contribution contribution, double limit) =>
        double.IsPositiveInfinity(limit)
            ? bounds
            : Narrow(bounds, contribution.Variable, contribution.Coefficient > 0
                ? new Interval(double.NegativeInfinity, limit / contribution.Coefficient)
                : new Interval(limit / contribution.Coefficient, double.PositiveInfinity));

    private static ImmutableDictionary<IVariable, Interval> Narrow(ImmutableDictionary<IVariable, Interval> bounds, IVariable variable, Interval limit) =>
        bounds.Of(variable) is var current && current.Intersect(Inwards(variable, limit)) is var narrowed && narrowed != current
            ? bounds.SetItem(variable, narrowed)
            : bounds;

    /// <summary>Whole-number variables round inwards, forgiving floating-point error a hair's breadth outside.</summary>
    private static Interval Inwards(IVariable variable, Interval limit) =>
        variable.IsIntegral
            ? new Interval(Math.Ceiling(limit.Lower - Slack(limit.Lower)), Math.Floor(limit.Upper + Slack(limit.Upper)))
            : limit;

    private static double Slack(double value) => double.IsInfinity(value) ? 0 : 1e-9 * Math.Max(1, Math.Abs(value));
}
