using Hephaestus.Contracts;
using Hephaestus.Gurobi;
using Hephaestus.OrTools;
using Hephaestus.Z3;

namespace Hephaestus.Solvers.Tests;

/// <summary>SCIP solves the problem; GLOP, a pure LP solver, prices it.</summary>
public sealed class OrToolsSensitivityContract : SensitivityContract {
    protected override ISolver Solver => OrToolsSolver.Create(OrToolsSolverId.Scip);

    protected override IMilpBackend Backend => new OrToolsBackend(OrToolsSolverId.Glop);
}

/// <summary>The problem need not have been solved by a MILP solver at all.</summary>
public sealed class Z3SensitivityContract : SensitivityContract {
    protected override ISolver Solver => new Z3Solver();

    protected override IMilpBackend Backend => new OrToolsBackend(OrToolsSolverId.Glop);
}

public sealed class GurobiSensitivityContract : SensitivityContract {
    protected override ISolver Solver => GurobiLicence.Require(GurobiSolver.Create());

    protected override IMilpBackend Backend => new GurobiBackend();
}

public sealed class SensitivityRefusalTests {
    private static readonly ContinuousVariable X = Variable.Continuous("x");

    [Fact]
    public void ABackendWithoutDualValuesSaysSo() {
        var problem = Problem.Minimise(X).SubjectTo(X >= 1);

        Assert.Contains("no dual values", Assert.Throws<InvalidOperationException>(() => problem.ShadowPrices(Solution.Empty.With(X, 1), new OrToolsBackend(OrToolsSolverId.Scip))).Message);
    }

    [Fact]
    public void ASolutionOfSomeOtherProblemHasNoPrices() {
        var problem = Problem.Minimise(X).SubjectTo((X >= 1) | (X <= -5));

        Assert.Contains("not solved to optimality", Assert.Throws<InvalidOperationException>(() => problem.ShadowPrices(Solution.Empty.With(X, -7), new OrToolsBackend(OrToolsSolverId.Glop))).Message);
    }
}
