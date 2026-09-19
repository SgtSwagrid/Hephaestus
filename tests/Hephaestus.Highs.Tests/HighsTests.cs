using Hephaestus.Contracts;

namespace Hephaestus.Highs.Tests;

public sealed class HighsContract : SolverContract {
    protected override ISolver Solver => HighsSolver.Create();
}

public sealed class HighsWholeNumberContract : WholeNumberContract {
    protected override ISolver Solver => HighsSolver.Create();
}

public sealed class HighsBackendTests {
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly IntegerVariable N = Variable.Integer("n");

    [Fact]
    public void LimitsAreAccepted() =>
        Assert.IsType<Optimal>(HighsSolver.Create(options: new SolverOptions(TimeLimit: TimeSpan.FromSeconds(10), RelativeGap: 0, Threads: 2)).Solve(Problem.Maximise(X + N, subjectTo: X.Between(0, 1.5) & N.Between(0, 5) & (X + N <= 5.25))));

    [Fact]
    public void AProblemWithNoRowsAtAllIsSolved() =>
        Assert.Equal(7, Assert.IsType<Optimal>(HighsSolver.Create().Solve(Problem.Maximise(X + N, subjectTo: X.Between(0, 2) & N.Between(0, 5)))).Solution.ObjectiveValue, precision: 6);

    [Fact]
    public void ACancelledSolveNeverStarts() =>
        Assert.Throws<OperationCanceledException>(() => HighsSolver.Create().Solve(Problem.Satisfy(X >= 0), new CancellationToken(canceled: true)));
}
