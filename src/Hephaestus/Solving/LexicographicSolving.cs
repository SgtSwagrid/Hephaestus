using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Solves a <see cref="MultipleObjectiveProblem"/> with any solver, as a sequence of ordinary solves:
/// each objective in turn, subject to every earlier one doing no worse than it was found able to, and
/// starting from the solution before.
/// </summary>
public static class LexicographicSolving {
    extension(ISolver solver) {
        /// <summary>Solves a problem of either kind: in one go if it has a single objective or none, and one objective after another if it has several.</summary>
        public ISolveResult Solve(IProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default) =>
            problem switch {
                IOneShotProblem single => solver.Solve(single, startingFrom, cancellationToken),
                MultipleObjectiveProblem multiple => solver.Solve(multiple, startingFrom, cancellationToken),
                _ => throw new NotSupportedException($"Unknown kind of problem: {problem.GetType().Name}."),
            };

        /// <summary>Solves the problem, one objective after another.</summary>
        /// <returns>
        /// An outcome whose solution is the last one found and whose objective value is that of the
        /// first objective; read the others with <c>solution.Value(objective.Expression)</c>. It is
        /// <see cref="Optimal"/> only if every stage was. A later stage that ends without a solution
        /// (a time limit, say) leaves the solution of the stage before, as <see cref="Feasible"/>.
        /// The solver's limits apply to each stage separately; the statistics are totals.
        /// </returns>
        public ISolveResult Solve(MultipleObjectiveProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default) =>
            problem.Objective.Priorities.IsEmpty
                ? solver.Solve(Problem.Satisfy(problem.Constraint), startingFrom, cancellationToken)
                : Concluded(problem, problem.Objective.Priorities.Aggregate(new Progress(problem.Constraint, startingFrom, []), (progress, objective) => Advance(solver, progress, objective, cancellationToken)).Stages);
    }

    private sealed record Progress(
        IBooleanExpression Constraint,
        Solution? Start,
        ImmutableList<ISolveResult> Stages
    );

    /// <summary>Once a stage has ended without a solution there is nothing for the later ones to build on.</summary>
    private static Progress Advance(ISolver solver, Progress progress, Prioritised objective, CancellationToken cancellationToken) =>
        progress.Stages.Count > 0 && progress.Stages[^1].SolutionOrNull is null
            ? progress
            : Advanced(progress, objective, solver.Solve(Problem.Optimise(objective.Objective).SubjectTo(progress.Constraint), progress.Start, cancellationToken));

    private static Progress Advanced(Progress progress, Prioritised objective, ISolveResult stage) =>
        stage.SolutionOrNull is { } solution
            ? new Progress(progress.Constraint & objective.NoWorseThan(solution.ObjectiveValue), solution, progress.Stages.Add(stage))
            : progress with { Stages = progress.Stages.Add(stage) };

    private static ISolveResult Concluded(MultipleObjectiveProblem problem, ImmutableList<ISolveResult> stages) =>
        Outcome(problem, stages, stages[^1]).With(Total(stages));

    private static ISolveResult Outcome(MultipleObjectiveProblem problem, ImmutableList<ISolveResult> stages, ISolveResult last) =>
        last.SolutionOrNull is { } found ? Found(found, problem, isProven: stages.Count == problem.Objective.Priorities.Length && stages.All(stage => stage is Optimal))
        // An unbounded objective is a fact about the model, wherever it comes in the order; it is not to be papered over.
        : last is Unbounded || stages.Count == 1 ? last
        : Found(stages[^2].SolutionOrNull!, problem, isProven: false);

    private static ISolveResult Found(Solution solution, MultipleObjectiveProblem problem, bool isProven) =>
        Found(solution with { ObjectiveValue = solution.Value(problem.Objective.Priorities[0].Objective.Expression) }, isProven);

    private static ISolveResult Found(Solution solution, bool isProven) => isProven ? new Optimal(solution) : new Feasible(solution);

    /// <summary>The times and counts of every stage together, with the bound of the first, since it is the first objective whose value is reported.</summary>
    private static SolveStatistics Total(ImmutableList<ISolveResult> stages) =>
        new(
            stages.Aggregate(TimeSpan.Zero, (total, stage) => total + stage.Statistics.EncodingTime),
            stages.Aggregate(TimeSpan.Zero, (total, stage) => total + stage.Statistics.SolvingTime),
            stages[0].Statistics.BestBound,
            stages.Any(stage => stage.Statistics.Nodes is not null) ? stages.Sum(stage => stage.Statistics.Nodes ?? 0) : null,
            stages.Any(stage => stage.Statistics.Iterations is not null) ? stages.Sum(stage => stage.Statistics.Iterations ?? 0) : null);
}
