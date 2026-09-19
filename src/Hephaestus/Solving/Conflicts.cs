using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A backend that can narrow an infeasibility down by itself: Gurobi, with its irreducible
/// infeasible subsystems. It answers in terms of the encoded problem; <see cref="Conflicts"/> turns
/// that into the modeller's constraints.
/// </summary>
public interface IConflictBackend {
    /// <summary>
    /// The origins of the rows and bounds of some infeasible part of the problem; empty if the
    /// problem is not found to be infeasible.
    /// </summary>
    ImmutableArray<IBooleanExpression> FindConflict(IndicatorProblem problem, SolverOptions options, CancellationToken cancellationToken);
}

/// <summary>A solver that can say which constraints an infeasibility lies among, without being asked about them one subset at a time.</summary>
public interface IConflictSolver : ISolver {
    /// <summary>
    /// Constraints (conjuncts of the problem's constraint, the very objects) among which a conflict
    /// lies; empty if the solver has nothing to offer. It need not be minimal.
    /// </summary>
    ImmutableArray<IBooleanExpression> NarrowConflict(ISingleObjectiveProblem problem, CancellationToken cancellationToken = default);
}

/// <summary>
/// Explains an infeasible problem by finding constraints that cannot all hold. It needs nothing of a
/// solver but that it can tell feasible from infeasible, so it works with every backend; one that
/// can narrow the search down by itself is asked to first.
/// </summary>
public static class Conflicts {
    extension(ISolver solver) {
        /// <summary>
        /// A conflict among the <c>Conjuncts</c> of the constraint: a set that cannot all hold, none
        /// of which can be left out (drop any one and the rest can). It is empty if the constraint
        /// can be satisfied. There may be other conflicts; this finds one, preferring constraints
        /// written earlier. It takes about <c>k &#183; log(n / k)</c> solves to find <c>k</c>
        /// constraints among <c>n</c> (the QuickXplain method), each of only part of the problem.
        /// <para>
        /// A part of the problem may be impossible to encode without the rest: leave out the bounds
        /// and a big-M cannot be derived. Such a part counts as able to hold. The conflict found is a
        /// genuine one all the same, but it then includes the bounds that the encoding leant on.
        /// </para>
        /// </summary>
        /// <exception cref="ModellingException">The constraint as a whole cannot be encoded.</exception>
        /// <exception cref="InvalidOperationException">The solver could not decide one of the subproblems, typically for want of time.</exception>
        public ImmutableArray<IBooleanExpression> FindConflict(IBooleanExpression constraint, CancellationToken cancellationToken = default) =>
            Find(new Oracle(solver, cancellationToken), constraint.Conjuncts, Narrowed(solver, constraint, cancellationToken));

        /// <inheritdoc cref="FindConflict(ISolver, IBooleanExpression, CancellationToken)"/>
        public ImmutableArray<IBooleanExpression> FindConflict(IProblem problem, CancellationToken cancellationToken = default) =>
            solver.FindConflict(problem.Constraint, cancellationToken);
    }

    /// <summary>
    /// A solver's own narrowing is a head start, not the answer: it is in terms of encoded rows, and
    /// a set of rows that is irreducible need not come from a set of constraints that is. So the same
    /// search is run over what it offers, which is quick because that is little; and if what it offers
    /// turns out not to be infeasible after all, the search is run over everything, as if it had offered nothing.
    /// </summary>
    private static ImmutableArray<IBooleanExpression> Find(Oracle oracle, ImmutableArray<IBooleanExpression> conjuncts, ImmutableArray<IBooleanExpression> narrowed) =>
        !narrowed.IsEmpty && !oracle.CanHold(narrowed) ? [.. Explain(oracle, [], hasGrown: false, narrowed)]
        : oracle.Decide(conjuncts) ? []
        : [.. Explain(oracle, [], hasGrown: false, conjuncts)];

    /// <summary>What the solver offers, as conjuncts of the constraint in the order written.</summary>
    private static ImmutableArray<IBooleanExpression> Narrowed(ISolver solver, IBooleanExpression constraint, CancellationToken cancellationToken) =>
        solver is IConflictSolver native && native.NarrowConflict(Problem.Satisfy(constraint), cancellationToken).ToImmutableHashSet<IBooleanExpression>(ReferenceEqualityComparer.Instance) is { IsEmpty: false } offered
            ? [.. constraint.Conjuncts.Where(offered.Contains)]
            : [];

    /// <summary>
    /// Asks a backend to narrow an encoded problem down. What is evident is answered without it:
    /// bounds that contradict each other, or a row without variables that fails. And the bounds that
    /// were derived for auxiliary variables are lifted first. They lose nothing, but they stand in
    /// for the constraints they were derived from, and a conflict that named them would name nothing.
    /// </summary>
    public static ImmutableArray<IBooleanExpression> Narrow(IndicatorProblem problem, IConflictBackend backend, SolverOptions options, CancellationToken cancellationToken) =>
        problem.Columns.FirstOrDefault(column => column.LowerBound > column.UpperBound && problem.BoundOrigins.GetValueOrDefault(column.Variable) is { Lower: not null, Upper: not null }) is { } contradictory
            ? [problem.BoundOrigins[contradictory.Variable].Lower!, problem.BoundOrigins[contradictory.Variable].Upper!]
        : problem.Rows.FirstOrDefault(row => row.Guards.IsEmpty && row.Expression.IsConstant && !IndicatorProblems.IsSatisfied(row)) is { Origin: { } origin }
            ? [origin]
        : backend.FindConflict(problem with { Columns = [.. problem.Columns.Select(column => WithoutDerivedBounds(column, problem.BoundOrigins.GetValueOrDefault(column.Variable)))] }, options, cancellationToken);

    private static Column WithoutDerivedBounds(Column column, BoundOrigin? origin) =>
        !column.IsAuxiliary || column.Variable is BinaryVariable
            ? column
            : column with {
                LowerBound = origin?.Lower is null ? double.NegativeInfinity : column.LowerBound,
                UpperBound = origin?.Upper is null ? double.PositiveInfinity : column.UpperBound,
            };

    private sealed record Oracle(
        ISolver Solver,
        CancellationToken CancellationToken
    ) {
        /// <summary>Whether the constraints can all hold, as the solver has it.</summary>
        public bool Decide(IEnumerable<IBooleanExpression> constraints) =>
            Solver.Solve(Problem.Satisfy(constraints.AllOf()), cancellationToken: CancellationToken) switch {
                Infeasible => false,
                Unknown unknown => throw new InvalidOperationException($"The solver could not decide whether part of the problem is feasible ({unknown.Reason}), so no conflict can be vouched for."),
                _ => true,
            };

        /// <summary>Whether part of the problem can hold, as far as can be told: a part that cannot be encoded by itself is not known to be infeasible.</summary>
        public bool CanHold(IEnumerable<IBooleanExpression> constraints) {
            try {
                return Decide(constraints);
            } catch (ModellingException) {
                return true;
            }
        }
    }

    /// <summary>
    /// A minimal part of <paramref name="candidates"/> that cannot hold together with
    /// <paramref name="background"/>, given that all of it cannot. If the background has just grown it
    /// may have become infeasible by itself, and then nothing more is needed. Otherwise the candidates
    /// are halved: what is needed from the second half is found with the first half held, then what is
    /// needed from the first half with only that.
    /// </summary>
    private static ImmutableList<IBooleanExpression> Explain(Oracle oracle, ImmutableList<IBooleanExpression> background, bool hasGrown, ImmutableArray<IBooleanExpression> candidates) =>
        hasGrown && !oracle.CanHold(background) ? []
        : candidates.Length == 1 ? [candidates[0]]
        : ExplainHalves(oracle, background, candidates[..(candidates.Length / 2)], candidates[(candidates.Length / 2)..]);

    private static ImmutableList<IBooleanExpression> ExplainHalves(Oracle oracle, ImmutableList<IBooleanExpression> background, ImmutableArray<IBooleanExpression> first, ImmutableArray<IBooleanExpression> second) {
        var fromSecond = Explain(oracle, background.AddRange(first), hasGrown: true, second);
        var fromFirst = Explain(oracle, background.AddRange(fromSecond), hasGrown: !fromSecond.IsEmpty, first);
        return fromFirst.AddRange(fromSecond);
    }
}
