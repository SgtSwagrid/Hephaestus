using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// The one place big-M lives. A guarded row "if every guard holds then <c>e &lt;= 0</c>" becomes
/// <c>e &lt;= M &#183; slack</c>, where slack counts the guards that are off and <c>M</c> is the
/// largest value <c>e</c> can take within the propagated bounds: the tightest valid choice,
/// derived separately for every row.
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
            var range => [Relaxed(guards, form, double.IsPositiveInfinity(range.Upper) ? Fallback(form, bounds, options) : range.Upper)],
        };

    /// <summary><c>form - M &#183; slack &lt;= 0</c>.</summary>
    private static LinearRow Relaxed(ImmutableList<Literal> guards, AffineForm form, double bigM) =>
        AsRow(form.Plus(IndicatorEncoding.Slack(guards).Times(-bigM)), isEquality: false);

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
