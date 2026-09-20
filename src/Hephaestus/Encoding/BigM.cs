using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// The one place big-M lives. A guarded row "if every guard holds then <c>e &lt;= 0</c>" becomes
/// <c>e &lt;= &#931; M&#7522; &#183; slack&#7522;</c>, with one term for each guard, whose slack is one
/// exactly when that guard is off. The row only has to give way when some guard is off, so
/// <c>M&#7522;</c> need only be the largest value <c>e</c> can take <em>while guard i is off</em>:
/// the tightest valid choice, derived separately for every guard of every row.
/// </summary>
internal static class BigM {
    public static IEnumerable<LinearRow> Relax(GuardedRow row, ImmutableDictionary<IVariable, Interval> bounds, EncodingOptions options) =>
        row switch {
            { Guards.IsEmpty: true } => [AsRow(row.Expression, row.IsEquality)],
            { IsEquality: true } => [
                .. RelaxBelowZero(row.Guards, row.Expression, bounds, options),
                .. RelaxBelowZero(row.Guards, row.Expression.Negated, bounds, options),
            ],
            _ => RelaxBelowZero(row.Guards, row.Expression, bounds, options),
        };

    private static IEnumerable<LinearRow> RelaxBelowZero(ImmutableList<Literal> guards, AffineForm form, ImmutableDictionary<IVariable, Interval> bounds, EncodingOptions options) =>
        form.Range(bounds.Of) switch {
            // The row can never be violated, so it need not be stated.
            { Upper: <= 0 } => [],
            // The row can never be satisfied, so the guards may not all hold.
            { Lower: > 0 } => [AsRow(IndicatorEncoding.AtLeastOne([], guards).Expression, isEquality: false)],
            _ => [Relaxed(guards, form, bounds, options)],
        };

    /// <summary><c>form - &#931; M&#7522; &#183; slack&#7522; &lt;= 0</c>, one term for each guard.</summary>
    private static LinearRow Relaxed(ImmutableList<Literal> guards, AffineForm form, ImmutableDictionary<IVariable, Interval> bounds, EncodingOptions options) =>
        AsRow(
            guards.Aggregate(form, (relaxed, guard) => relaxed.Plus(IndicatorEncoding.Slack(guard).Times(-Allowance(guard, form, bounds, options)))),
            isEquality: false);

    /// <summary>
    /// The most the row may exceed zero by while this guard is off, which is the tightest coefficient
    /// its slack can take. Fixing the guard's own variable is what makes this narrower than the row's
    /// unconditional range, for a row that counts the very binary that guards it.
    /// </summary>
    private static double Allowance(Literal guard, AffineForm form, ImmutableDictionary<IVariable, Interval> bounds, EncodingOptions options) =>
        Limited(form.Range(bounds.SetItem(guard.Variable, Off(guard)).Of).Upper, form, bounds, options);

    /// <summary>A negative allowance is no allowance at all, since the row holds anyway; an infinite one cannot be written down.</summary>
    private static double Limited(double allowance, AffineForm form, ImmutableDictionary<IVariable, Interval> bounds, EncodingOptions options) =>
        allowance <= 0 ? 0
        : double.IsPositiveInfinity(allowance) ? Fallback(form, bounds, options)
        : allowance;

    /// <summary>The value a guard's variable takes when the guard does not hold.</summary>
    private static Interval Off(Literal guard) => guard.IsPositive ? new Interval(0, 0) : new Interval(1, 1);

    private static double Fallback(AffineForm form, ImmutableDictionary<IVariable, Interval> bounds, EncodingOptions options) =>
        options.FallbackBigM ?? throw new ModellingException(
            $"Cannot derive a big-M for the conditional constraint '{form.Format()} <= 0': it has no upper limit because "
            + $"{string.Join(", ", UnboundedVariables(form, bounds).Select(variable => $"'{variable.Name}'"))} "
            + "lack the bounds that would limit it. State them as ordinary constraints (for example 0 <= x & x <= 100), "
            + $"or opt into a guessed value with {nameof(EncodingOptions)}.{nameof(EncodingOptions.FallbackBigM)}.");

    private static IEnumerable<IVariable> UnboundedVariables(AffineForm form, ImmutableDictionary<IVariable, Interval> bounds) =>
        form.Coefficients
            .Where(term => double.IsPositiveInfinity(bounds.Of(term.Key).Times(term.Value).Upper))
            .Select(term => term.Key);

    /// <summary>Moves the constant across: <c>&#931; ax + c &lt;= 0</c> is the row <c>&#931; ax &lt;= -c</c>.</summary>
    private static LinearRow AsRow(AffineForm form, bool isEquality) =>
        new(form.Coefficients, isEquality ? 0 - form.Constant : double.NegativeInfinity, 0 - form.Constant);
}
