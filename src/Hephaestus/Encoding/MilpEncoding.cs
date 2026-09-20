using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Lowers a problem in pure steps: normalise and encode the logic with guarded rows, giving an
/// <see cref="IndicatorProblem"/>; then propagate bounds and relax the guards with big-M values
/// derived from them, giving a <see cref="MilpProblem"/>.
/// </summary>
public static class MilpEncoding {
    extension(IOneShotProblem problem) {
        /// <summary>The problem as a plain mixed-integer linear programme.</summary>
        /// <exception cref="ModellingException">
        /// Two different variables share a name, an expression is not finite, or a big-M cannot be
        /// derived and no fallback was configured.
        /// </exception>
        public MilpProblem Encode(EncodingOptions? options = null) => problem.EncodeLogic(options).RelaxGuards(options);

        /// <summary>
        /// The problem with its logic encoded as guarded rows, but no guard yet relaxed: the form
        /// for solvers with indicator or half-reified constraints, which need no big-M.
        /// </summary>
        /// <exception cref="ModellingException">Two different variables share a name, or an expression is not finite.</exception>
        public IndicatorProblem EncodeLogic(EncodingOptions? options = null) => Lower(problem, options ?? EncodingOptions.Default);
    }

    private static IndicatorProblem Lower(IOneShotProblem original, EncodingOptions options) {
        var linearised = original.Linearise(options);
        var (problem, definitions) = linearised;
        var auxiliaries = definitions.Select(definition => definition.Variable).ToImmutableHashSet();
        // A row is put down to the constraint as it was written; one that nobody wrote stands for itself.
        var conjuncts = linearised.Constraints(original.Constraint).Select(constraint => (Origin: constraint.Written ?? constraint.Lowered, Formula: constraint.Lowered.Normalise(options.StrictnessEpsilon)));
        // A maximum that the problem turns out not to lean on is never tied down, and would take its operands with it.
        var variables = problem.Variables.Union(original.Variables);
        var program = IndicatorEncoding.Encode(conjuncts, new AuxiliaryNaming([.. variables.Select(variable => variable.Name)], options.AuxiliaryPrefix));
        var stated = BoundPropagation.Sweep(program.Rows.Where(IsStatedBound), ImmutableDictionary<IVariable, Interval>.Empty);
        return WithOrigins(program.Rows.Where(IsStatedBound), stated, WithBounds(definitions, options.BoundPropagationRounds, new IndicatorProblem(
            [
                .. variables.Select(variable => AsColumn(variable, stated, isAuxiliary: auxiliaries.Contains(variable))),
                .. program.Auxiliaries.Select(variable => AsColumn(variable, stated, isAuxiliary: true)),
            ],
            [.. program.Rows.Where(row => !IsStatedBound(row)).SelectMany(Tidied)],
            problem.Sense,
            problem.Objective.Expression.Normalise())));
    }

    /// <summary>One side of a variable's bounds, as one constraint states it.</summary>
    private sealed record StatedBound(
        IVariable Variable,
        bool IsUpper,
        double Value,
        IBooleanExpression? Origin
    );

    /// <summary>
    /// Records which constraint states each bound: the tightest, where several do. A bound that was
    /// since tightened by derivation (as those of the variables that stand for maxima are) is no
    /// longer the stated one, and is put down to nothing.
    /// </summary>
    private static IndicatorProblem WithOrigins(IEnumerable<GuardedRow> statedRows, ImmutableDictionary<IVariable, Interval> stated, IndicatorProblem problem) {
        var tightest = statedRows.SelectMany(StatedBy).GroupBy(bound => (bound.Variable, bound.IsUpper)).ToImmutableDictionary(group => group.Key, group => group.Aggregate((best, next) => IsTighter(next, best) ? next : best).Origin);
        return problem with {
            BoundOrigins = problem.Columns
                .Select(column => KeyValuePair.Create(column.Variable, new BoundOrigin(
                    column.LowerBound == stated.Of(column.Variable).Lower ? tightest.GetValueOrDefault((column.Variable, false)) : null,
                    column.UpperBound == stated.Of(column.Variable).Upper ? tightest.GetValueOrDefault((column.Variable, true)) : null)))
                .Where(entry => entry.Value is not { Lower: null, Upper: null })
                .ToImmutableDictionary(),
        };
    }

    /// <summary>A smaller upper bound binds, as does a larger lower one; where two are equal neither is tighter, and the one written first is kept.</summary>
    private static bool IsTighter(StatedBound bound, StatedBound than) =>
        bound.IsUpper ? bound.Value < than.Value : bound.Value > than.Value;

    /// <summary><c>c&#183;x + k &lt;= 0</c> bounds <c>x</c> from above if <c>c</c> is positive and from below if not; an equation does both.</summary>
    private static IEnumerable<StatedBound> StatedBy(GuardedRow row) =>
        row.Expression.Coefficients.Single() is var term && row.IsEquality
            ? [new StatedBound(term.Key, true, -row.Expression.Constant / term.Value, row.Origin), new StatedBound(term.Key, false, -row.Expression.Constant / term.Value, row.Origin)]
            : [new StatedBound(term.Key, term.Value > 0, -row.Expression.Constant / term.Value, row.Origin)];

    /// <summary>
    /// A maximum lies between the largest of its operands' lower bounds and the largest of their
    /// upper bounds. Saying so bounds the variable that stands for it on both sides, even where it is
    /// tied to its operands on one side only: that loses nothing, and gives a big-M something to be
    /// derived from and a finite-domain solver its domain.
    /// </summary>
    private static IndicatorProblem WithBounds(ImmutableArray<IDefinition> definitions, int rounds, IndicatorProblem problem) =>
        definitions.IsEmpty
            ? problem
            : problem.WithColumnBounds(definitions.Aggregate(problem.DerivedBounds(rounds), Bound), [.. definitions.Select(definition => definition.Variable)]);

    private static ImmutableDictionary<IVariable, Interval> Bound(ImmutableDictionary<IVariable, Interval> bounds, IDefinition definition) =>
        bounds.SetItem(definition.Variable, bounds.Of(definition.Variable).Intersect(RangeOf(definition, bounds)));

    /// <summary>A maximum lies between the largest of the lower bounds and the largest of the upper; a conditional lies somewhere in one branch or the other.</summary>
    private static Interval RangeOf(IDefinition definition, ImmutableDictionary<IVariable, Interval> bounds) =>
        definition switch {
            MaximumDefinition maximum when (maximum.Left.Normalise().Range(bounds.Of), maximum.Right.Normalise().Range(bounds.Of)) is var (left, right) => new Interval(Math.Max(left.Lower, right.Lower), Math.Max(left.Upper, right.Upper)),
            ConditionalDefinition conditional when (conditional.Then.Normalise().Range(bounds.Of), conditional.Otherwise.Normalise().Range(bounds.Of)) is var (then, otherwise) => new Interval(Math.Min(then.Lower, otherwise.Lower), Math.Max(then.Upper, otherwise.Upper)),
            _ => throw new NotSupportedException($"Unknown kind of definition: {definition.GetType().Name}."),
        };

    /// <summary>A row without variables either says nothing, or says that its guards cannot all hold.</summary>
    private static IEnumerable<GuardedRow> Tidied(GuardedRow row) =>
        !row.Expression.IsConstant ? [row]
        : IndicatorProblems.IsSatisfied(row) ? []
        : [IndicatorEncoding.AtLeastOne([], row.Guards) with { Origin = row.Origin }];

    /// <summary>An unconditional row over a single variable is a bound, and is handed to the solver as one.</summary>
    private static bool IsStatedBound(GuardedRow row) => row.Guards.IsEmpty && row.Expression.Coefficients.Count == 1;

    private static Column AsColumn(IVariable variable, ImmutableDictionary<IVariable, Interval> stated, bool isAuxiliary) =>
        new(variable, stated.Of(variable).Lower, stated.Of(variable).Upper, isAuxiliary);
}
