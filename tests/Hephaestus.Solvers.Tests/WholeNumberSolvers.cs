using Hephaestus.Contracts;
using Hephaestus.Gurobi;
using Hephaestus.OrTools;
using Hephaestus.Z3;

namespace Hephaestus.Solvers.Tests;

public sealed class CpSatWholeNumberContract : WholeNumberContract {
    protected override ISolver Solver => CpSatSolver.Create();
}

public sealed class ScipWholeNumberContract : WholeNumberContract {
    protected override ISolver Solver => OrToolsSolver.Create(OrToolsSolverId.Scip);
}

public sealed class Z3WholeNumberContract : WholeNumberContract {
    protected override ISolver Solver => new Z3Solver();
}

public sealed class GurobiWholeNumberContract : WholeNumberContract {
    protected override ISolver Solver => GurobiLicence.Require(GurobiSolver.Create());
}

/// <summary>What CP-SAT cannot take, it must say so; and where it needs bounds, it must say which.</summary>
public sealed class CpSatRefusalTests {
    private static readonly IntegerVariable N = Variable.Integer("n");

    [Fact]
    public void ContinuousVariablesAreRefusedByName() {
        var exception = Assert.Throws<NotSupportedException>(() => CpSatSolver.Create().Solve(Problem.Minimise(N, subjectTo: N.Between(0, 5) & (Variable.Continuous("x") >= 1))));

        Assert.Contains("'x'", exception.Message);
    }

    [Fact]
    public void VariablesWithoutFiniteDomainsAreNamed() {
        var exception = Assert.Throws<ModellingException>(() => CpSatSolver.Create().Solve(Problem.Minimise(N, subjectTo: (N >= 0) & (Variable.Integer("m") <= N))));

        Assert.Contains("'n'", exception.Message);
        Assert.Contains("'m'", exception.Message);
    }

    [Fact]
    public void CoefficientsThatNoPowerOfTenMakesWholeAreRefused() =>
        Assert.Throws<NotSupportedException>(() => CpSatSolver.Create().Solve(Problem.Satisfy(N.Between(0, 5) & (N / 3 + Variable.Integer("m") <= 1) & Variable.Integer("m").Between(0, 5))));

    [Fact]
    public void LimitsAreRespected() =>
        Assert.IsType<Optimal>(CpSatSolver.Create(options: new SolverOptions(TimeLimit: TimeSpan.FromSeconds(10), RelativeGap: 0, Threads: 2)).Solve(Problem.Maximise(N, subjectTo: N.Between(0, 5))));
}
