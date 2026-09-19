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
    }

    private static IndicatorProblem Lower(IProblem problem, EncodingOptions options) {
        var formula = problem.Constraint.Normalise(options.StrictnessEpsilon);
        var variables = problem.Variables;
        var program = IndicatorEncoding.Encode(formula, new AuxiliaryNaming([.. variables.Select(variable => variable.Name)], options.AuxiliaryPrefix));
        var stated = BoundPropagation.Sweep(program.Rows.Where(IsStatedBound), ImmutableDictionary<IVariable, Interval>.Empty);
        return new IndicatorProblem(
            [
                .. variables.Select(variable => AsColumn(variable, stated, isAuxiliary: false)),
                .. program.Auxiliaries.Select(variable => AsColumn(variable, stated, isAuxiliary: true)),
            ],
            [.. program.Rows.Where(row => !IsStatedBound(row)).SelectMany(Tidied)],
            problem.Sense,
            problem.Objective.Normalise());
    }

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
