using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Explains an infeasible problem by finding constraints that cannot all hold. It needs nothing of a
/// solver but that it can tell feasible from infeasible, so it works with every backend.
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
            new Oracle(solver, cancellationToken) is var oracle && oracle.Decide(constraint.Conjuncts)
                ? []
                : [.. Explain(oracle, [], hasGrown: false, constraint.Conjuncts)];

        /// <inheritdoc cref="FindConflict(ISolver, IBooleanExpression, CancellationToken)"/>
        public ImmutableArray<IBooleanExpression> FindConflict(IProblem problem, CancellationToken cancellationToken = default) =>
            solver.FindConflict(problem.Constraint, cancellationToken);
    }

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
