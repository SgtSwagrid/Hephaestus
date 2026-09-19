using System.Collections.Immutable;
using Highs;

namespace Hephaestus.Highs;

/// <summary>Solves mixed-integer linear programmes with HiGHS, through the wrapper its maintainers publish.</summary>
public sealed record HighsBackend : IMilpBackend {
    /// <inheritdoc/>
    /// <remarks>HiGHS cannot be interrupted through this wrapper, so cancellation is honoured only before the solve starts; use a time limit.</remarks>
    public ISolveResult Solve(MilpProblem problem, SolverOptions options, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        using var solver = new HighsLpSolver();
        Require(solver.setBoolOptionValue("output_flag", 0), "silence the log");
        Configure(solver, options);
        Require(solver.passMip(AsModel(problem)), "load the model");
        Require(solver.run(), "solve");

        return AsResult(Settled(solver), solver, problem);
    }

    /// <summary>The programme in the row-wise sparse form that HiGHS takes.</summary>
    private static HighsModel AsModel(MilpProblem problem) {
        var indexOf = problem.Columns.Select((column, index) => (column.Variable, Index: index)).ToImmutableDictionary(entry => entry.Variable, entry => entry.Index);
        return new HighsModel(
            colcost: [.. problem.Columns.Select(column => problem.Objective.Coefficients.GetValueOrDefault(column.Variable))],
            collower: [.. problem.Columns.Select(column => column.LowerBound)],
            colupper: [.. problem.Columns.Select(column => column.UpperBound)],
            rowlower: [.. problem.Rows.Select(row => row.LowerBound)],
            rowupper: [.. problem.Rows.Select(row => row.UpperBound)],
            astart: [.. problem.Rows.Aggregate(ImmutableList.Create(0), (starts, row) => starts.Add(starts[^1] + row.Coefficients.Count)).SkipLast(1)],
            aindex: [.. problem.Rows.SelectMany(row => row.Coefficients.Keys.Select(variable => indexOf[variable]))],
            avalue: [.. problem.Rows.SelectMany(row => row.Coefficients.Values)],
            highs_integrality: [.. problem.Columns.Select(column => (int)(column.Variable.IsIntegral ? HighsIntegrality.kInteger : HighsIntegrality.kContinuous))],
            offset: problem.Objective.Constant,
            a_format: HighsMatrixFormat.kRowwise,
            sense: problem.Sense == ObjectiveSense.Maximise ? HighsObjectiveSense.kMaximize : HighsObjectiveSense.kMinimize);
    }

    private static void Configure(HighsLpSolver solver, SolverOptions options) {
        if (options.TimeLimit is { } timeLimit) {
            Require(solver.setDoubleOptionValue("time_limit", timeLimit.TotalSeconds), "set the time limit");
        }
        if (options.Threads is { } threads) {
            Require(solver.setIntOptionValue("threads", threads), "set the thread count");
        }
        if (options.RelativeGap is { } gap) {
            Require(solver.setDoubleOptionValue("mip_rel_gap", gap), "set the gap");
        }
    }

    /// <summary>Presolve sometimes cannot tell infeasible from unbounded; solving again without it settles which.</summary>
    private static HighsModelStatus Settled(HighsLpSolver solver) {
        if (solver.GetModelStatus() == HighsModelStatus.kUnboundedOrInfeasible) {
            Require(solver.setStringOptionValue("presolve", "off"), "switch presolve off");
            Require(solver.run(), "solve without presolve");
        }
        return solver.GetModelStatus();
    }

    private static ISolveResult AsResult(HighsModelStatus status, HighsLpSolver solver, MilpProblem problem) =>
        status switch {
            HighsModelStatus.kOptimal => new Optimal(ReadSolution(solver, problem)),
            HighsModelStatus.kInfeasible => new Infeasible(),
            HighsModelStatus.kUnbounded => new Unbounded(),
            // Without an incumbent HiGHS reports an infinite objective.
            _ when double.IsFinite(solver.getInfo().ObjectiveValue) && solver.getSolution().colvalue.Length == problem.Columns.Length => new Feasible(ReadSolution(solver, problem)),
            _ => new Unknown($"HiGHS stopped with status {status} and no solution."),
        };

    private static Solution ReadSolution(HighsLpSolver solver, MilpProblem problem) {
        var values = solver.getSolution().colvalue;
        return new Solution(
            problem.Columns.Select((column, index) => (column.Variable, Value: values[index])).ToImmutableSortedDictionary(entry => entry.Variable, entry => entry.Value, VariableOrder.Comparer),
            solver.getInfo().ObjectiveValue);
    }

    private static void Require(HighsStatus status, string action) {
        if (status == HighsStatus.kError) {
            throw new InvalidOperationException($"HiGHS failed to {action}.");
        }
    }
}

/// <summary>Ready-made solvers backed by HiGHS.</summary>
public static class HighsSolver {
    /// <summary>A solver for full problems, backed by HiGHS.</summary>
    public static ISolver Create(EncodingOptions? encoding = null, SolverOptions? options = null) =>
        new MilpSolver(new HighsBackend(), encoding, options);
}
