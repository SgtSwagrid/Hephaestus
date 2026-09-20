using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// What each constraint of a problem costs at a solution: for a constraint <c>lhs &lt;= rhs</c> (or
/// <c>&gt;=</c>, or <c>==</c>), the change in the optimal objective for each unit by which its
/// right-hand side, as written, is raised. A constraint that is not binding has a price of zero.
/// </summary>
/// <param name="ByName">The price of every constraint (conjunct) of the problem, by its name.</param>
public sealed record ShadowPrices(ImmutableDictionary<string, double> ByName);

/// <summary>
/// Sensitivity analysis. Prices belong to linear programmes, and a problem with logic or whole
/// numbers is not one; but at a solution it comes down to one. Every disjunction has a side that
/// holds, and every whole-number variable has its value; keep those, and what is left is the linear
/// programme of the continuous variables that the solution is a solution of. Its dual values are the
/// prices. They say what each constraint costs given the discrete choices that were made, not what
/// it would cost if those choices could be made again.
/// </summary>
public static class Sensitivity {
    extension(ISingleObjectiveProblem problem) {
        /// <summary>The shadow prices of the problem's constraints at <paramref name="solution"/>.</summary>
        /// <param name="solution">A solution of the problem, normally its optimum.</param>
        /// <param name="backend">A backend that reports dual values for linear programmes: Gurobi, HiGHS, or OR-Tools with GLOP.</param>
        /// <param name="options">Limits and tuning for the solve of the linear programme.</param>
        /// <param name="cancellationToken">Stops the solve early.</param>
        /// <exception cref="InvalidOperationException">The backend did not solve the linear programme to optimality, or reported no dual values.</exception>
        public ShadowPrices ShadowPrices(Solution solution, IMilpBackend backend, SolverOptions? options = null, CancellationToken cancellationToken = default) =>
            Priced(problem, problem.Linearise(), solution, backend, options ?? SolverOptions.Default, cancellationToken);
    }

    extension(ShadowPrices prices) {
        /// <summary>The price of a constraint: of one conjunct, or the sum over the conjuncts of a conjunction, such as the two sides of <c>Between</c>.</summary>
        public double Of(IBooleanExpression constraint) => constraint.Conjuncts.Sum(conjunct => prices.ByName.GetValueOrDefault(conjunct.Name));
    }

    /// <summary>A row of the linear programme, and the constraint it came from.</summary>
    private sealed record PricedRow(
        string Name,
        LinearRow Row
    );

    private sealed record Step(
        IBooleanExpression Expression,
        bool Polarity,
        Solution Solution
    );

    private static ShadowPrices Priced(ISingleObjectiveProblem original, LinearisedProblem linearised, Solution solution, IMilpBackend backend, SolverOptions options, CancellationToken cancellationToken) {
        // The variables that stand for maxima are hidden from solutions, but what they stand for can be read off.
        var full = linearised.Definitions.Aggregate(solution, (known, definition) => known.With(definition.Variable, ValueOf(definition, known)));
        // Lowering keeps the conjuncts in order and adds its own after them, which go unpriced (and unnamed).
        var written = original.Constraint.Conjuncts;
        var rows = linearised.Problem.Constraint.Conjuncts
            .SelectMany((lowered, index) => RowsOf(index < written.Length ? written[index].Name : "", lowered, full))
            .ToImmutableArray();
        var programme = Programme(linearised.Problem, rows, full);
        var duals = Duals(backend.Solve(programme, full.Values.Where(entry => !entry.Key.IsIntegral).ToImmutableDictionary(), options, cancellationToken), rows.Length);
        return new ShadowPrices(rows
            .Zip(duals, (row, dual) => (row.Name, Dual: dual))
            .Where(entry => entry.Name.Length > 0)
            .GroupBy(entry => entry.Name)
            .ToImmutableDictionary(group => group.Key, group => group.Sum(entry => entry.Dual))
            // A constraint with no row in force (a bound on a whole-number variable, say) has no price to speak of.
            .SetItems(written.Select(conjunct => conjunct.Name).Except(rows.Select(row => row.Name)).Select(name => KeyValuePair.Create(name, 0.0))));
    }

    private static double ValueOf(IDefinition definition, Solution solution) =>
        definition switch {
            MaximumDefinition maximum => Math.Max(solution.Value(maximum.Left), solution.Value(maximum.Right)),
            ConditionalDefinition conditional => solution.Value(solution.Value(conditional.Condition) ? conditional.Then : conditional.Otherwise),
            _ => throw new NotSupportedException($"Unknown kind of definition: {definition.GetType().Name}."),
        };

    private static IEnumerable<PricedRow> RowsOf(string name, IBooleanExpression conjunct, Solution solution) =>
        Active(new Step(conjunct, true, solution)).SelectMany(comparison => AsRow(comparison, solution)).Select(row => new PricedRow(name, row));

    private static MilpProblem Programme(ISingleObjectiveProblem problem, ImmutableArray<PricedRow> rows, Solution solution) {
        var objective = Continuous(problem.Objective.Expression.Normalise(), solution);
        return new MilpProblem(
            [.. rows.SelectMany(row => row.Row.Coefficients.Keys).Concat(objective.Coefficients.Keys).Distinct().Order(VariableOrder.Comparer).Select(variable => new Column(variable, double.NegativeInfinity, double.PositiveInfinity, IsAuxiliary: false))],
            [.. rows.Select(row => row.Row)],
            problem.Sense,
            objective);
    }

    private static ImmutableArray<double> Duals(ISolveResult result, int count) =>
        result is Optimal { Solution.RowDuals: var duals } && duals.Length == count
            ? duals
            : throw new InvalidOperationException(
                result is Optimal
                    ? "The backend solved the linear programme but reported no dual values. Use one that does: Gurobi, HiGHS, or OR-Tools with GLOP."
                    : $"The linear programme at the solution was not solved to optimality ({result.GetType().Name}), so it has no prices. Is the solution one of this problem?");

    /// <summary>The comparisons that are in force at the solution: both sides of what is conjunctive, and the side that holds of what is disjunctive.</summary>
    private static IEnumerable<Comparison> Active(Step step) => DeepRecursion.Guard(ActiveUnguarded, step);

    private static IEnumerable<Comparison> ActiveUnguarded(Step step) =>
        step.Expression switch {
            Comparison comparison => [Oriented(comparison, step)],
            NamedConstraint named => Active(step with { Expression = named.Expression }),
            Negation negation => Active(step with { Expression = negation.Operand, Polarity = !step.Polarity }),
            Conjunction conjunction => Junction(step, conjunction.Left, conjunction.Right, isConjunctive: step.Polarity),
            Disjunction disjunction => Junction(step, disjunction.Left, disjunction.Right, isConjunctive: !step.Polarity),
            Implication implication => Active(step with { Expression = !implication.Antecedent | implication.Consequent }),
            Equivalence equivalence => Active(step with { Expression = (equivalence.Left & equivalence.Right) | (!equivalence.Left & !equivalence.Right) }),
            _ => [],
        };

    private static IEnumerable<Comparison> Junction(Step step, IBooleanExpression left, IBooleanExpression right, bool isConjunctive) =>
        isConjunctive ? [.. Active(step with { Expression = left }), .. Active(step with { Expression = right })]
        : step.Solution.Value(left) == step.Polarity ? Active(step with { Expression = left })
        : Active(step with { Expression = right });

    /// <summary>A comparison under negation is the opposite comparison, and a disequality is whichever strict inequality holds.</summary>
    private static Comparison Oriented(Comparison comparison, Step step) =>
        (step.Polarity ? comparison.Relation : BooleanNormalisation.Opposite(comparison.Relation)) switch {
            Relation.NotEqual => comparison with { Relation = step.Solution.Value(comparison.Left - comparison.Right) < 0 ? Relation.LessThan : Relation.GreaterThan },
            var relation => comparison with { Relation = relation },
        };

    /// <summary>
    /// <c>lhs - rhs = a&#183;x + k</c> compared with zero is the row <c>a&#183;x</c> compared with <c>-k</c>, and raising
    /// the right-hand side as written raises that bound by as much, whichever way the comparison faces.
    /// Strictness is dropped: a price is a rate, and the rate is the same at the boundary.
    /// </summary>
    private static IEnumerable<LinearRow> AsRow(Comparison comparison, Solution solution) =>
        Continuous((comparison.Left - comparison.Right).Normalise(), solution) is var form && form.IsConstant ? []
        : [comparison.Relation switch {
            Relation.LessThan or Relation.LessThanOrEqual => new LinearRow(form.Coefficients, double.NegativeInfinity, 0 - form.Constant),
            Relation.GreaterThan or Relation.GreaterThanOrEqual => new LinearRow(form.Coefficients, 0 - form.Constant, double.PositiveInfinity),
            _ => new LinearRow(form.Coefficients, 0 - form.Constant, 0 - form.Constant),
        }];

    /// <summary>The form with every whole-number variable replaced by its value.</summary>
    private static AffineForm Continuous(AffineForm form, Solution solution) =>
        form.Coefficients
            .Where(term => term.Key.IsIntegral)
            .Aggregate(form, (reduced, term) => reduced.PlusTerm(term.Key, -term.Value).Plus(term.Value * solution.Value(term.Key)));
}
