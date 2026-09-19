using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Lowers a problem in pure steps: normalise and encode the logic with guarded rows, giving an
/// <see cref="IndicatorProblem"/>; then propagate bounds and relax the guards with big-M values
/// derived from them, giving a <see cref="MilpProblem"/>.
/// </summary>
public static class MilpEncoding {
    extension(IProblem problem) {
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

        /// <summary>The constraint every solution must satisfy.</summary>
        public IBooleanExpression Constraint =>
            problem switch {
                Satisfaction satisfaction => satisfaction.Constraint,
                Minimisation minimisation => minimisation.Constraint,
                Maximisation maximisation => maximisation.Constraint,
                _ => throw new NotSupportedException($"Unknown kind of problem: {problem.GetType().Name}."),
            };

        /// <summary>The expression being optimised; constantly zero for a <see cref="Satisfaction"/> problem.</summary>
        public ILinearExpression Objective =>
            problem switch {
                Minimisation minimisation => minimisation.Objective,
                Maximisation maximisation => maximisation.Objective,
                _ => new Constant(0),
            };

        /// <summary>Whether the objective is to be made small or large.</summary>
        public ObjectiveSense Sense => problem is Maximisation ? ObjectiveSense.Maximise : ObjectiveSense.Minimise;

        /// <summary>A problem of the same kind with another objective and constraint. (A <see cref="Satisfaction"/> problem has no objective to replace.)</summary>
        public IProblem With(ILinearExpression objective, IBooleanExpression constraint) =>
            problem switch {
                Minimisation => new Minimisation(objective, constraint),
                Maximisation => new Maximisation(objective, constraint),
                _ => new Satisfaction(constraint),
            };
    }

    private static IndicatorProblem Lower(IProblem original, EncodingOptions options) {
        var (problem, definitions) = original.Linearise(options);
        var auxiliaries = definitions.Select(definition => definition.Variable).ToImmutableHashSet();
        var formula = problem.Constraint.Normalise(options.StrictnessEpsilon);
        // A maximum that the problem turns out not to lean on is never tied down, and would take its operands with it.
        var variables = problem.Variables.Union(original.Variables);
        var program = IndicatorEncoding.Encode(formula, new AuxiliaryNaming([.. variables.Select(variable => variable.Name)], options.AuxiliaryPrefix));
        var stated = BoundPropagation.Sweep(program.Rows.Where(IsStatedBound), ImmutableDictionary<IVariable, Interval>.Empty);
        return WithBounds(definitions, options.BoundPropagationRounds, new IndicatorProblem(
            [
                .. variables.Select(variable => AsColumn(variable, stated, isAuxiliary: auxiliaries.Contains(variable))),
                .. program.Auxiliaries.Select(variable => AsColumn(variable, stated, isAuxiliary: true)),
            ],
            [.. program.Rows.Where(row => !IsStatedBound(row)).SelectMany(Tidied)],
            problem.Sense,
            problem.Objective.Normalise()));
    }

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
        : [IndicatorEncoding.AtLeastOne([], row.Guards)];

    /// <summary>An unconditional row over a single variable is a bound, and is handed to the solver as one.</summary>
    private static bool IsStatedBound(GuardedRow row) => row.Guards.IsEmpty && row.Expression.Coefficients.Count == 1;

    private static Column AsColumn(IVariable variable, ImmutableDictionary<IVariable, Interval> stated, bool isAuxiliary) =>
        new(variable, stated.Of(variable).Lower, stated.Of(variable).Upper, isAuxiliary);
}
