using Hephaestus.Gurobi;

namespace Hephaestus.Solvers.Tests;

/// <summary>What the shared contracts do not reach: Gurobi's two formulations side by side, its options, and its silence.</summary>
public sealed class GurobiTests {
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly BinaryVariable A = Variable.Binary("a");
    private static readonly BinaryVariable B = Variable.Binary("b");
    private static readonly BinaryVariable C = Variable.Binary("c");

    private static double Optimum(ISolver solver, IProblem problem) =>
        Assert.IsType<Optimal>(GurobiLicence.Require(solver).Solve(problem)).Solution.ObjectiveValue;

    [Fact]
    public void ARowUnderSeveralGuardsBindsOnlyWhenTheyAllHold() {
        var constraint = X.Between(0, 10) & (!(A & B & C) | (X >= 7));

        Assert.Equal(7, Optimum(GurobiSolver.Create(), Problem.Minimise(X, subjectTo: constraint & A & B & C)), precision: 6);
        Assert.Equal(0, Optimum(GurobiSolver.Create(), Problem.Minimise(X, subjectTo: constraint & A & B & !C)), precision: 6);
        // Worth switching the third guard on, even though the row then binds: 3 - 0.7 beats 2 - 0.
        Assert.Equal(2.3, Optimum(GurobiSolver.Create(), Problem.Maximise(A + B + C - 0.1 * X, subjectTo: constraint)), precision: 6);
    }

    [Fact]
    public void IndicatorsNeedNoBoundsWhereBigMWouldRefuse() {
        var problem = Problem.Minimise(X, subjectTo: (X >= 0) & A & A.Implies(X + Variable.Continuous("y") >= 5) & (Variable.Continuous("y") <= 2));

        Assert.Equal(3, Optimum(GurobiSolver.Create(), problem), precision: 6);
        Assert.Throws<ModellingException>(() => GurobiLicence.Require(GurobiSolver.Create(useIndicators: false)).Solve(problem));
    }

    [Fact]
    public void BothFormulationsAgree() {
        var slots = Enumerable.Range(0, 6).Select(index => Variable.Continuous($"slot{index}")).ToList();
        var separated = slots.SelectMany((first, index) => slots.Skip(index + 1).Select(second => (first + 90 <= second) | (second + 90 <= first)));
        var problem = Problem.Minimise(slots.Sum(), subjectTo: slots.AllOf(slot => slot.Between(0, 3600)) & separated.AllOf());

        Assert.Equal(90 * 15, Optimum(GurobiSolver.Create(), problem), precision: 4);
        Assert.Equal(90 * 15, Optimum(GurobiSolver.Create(useIndicators: false), problem), precision: 4);
    }

    [Fact]
    public void LimitsAreAccepted() =>
        Assert.Equal(5, Optimum(GurobiSolver.Create(options: new SolverOptions(TimeLimit: TimeSpan.FromSeconds(10), RelativeGap: 0, Threads: 2)), Problem.Maximise(N, subjectTo: N.Between(0, 5))));

    [Fact]
    public void NothingIsPrinted() {
        var original = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);
        try {
            Optimum(GurobiSolver.Create(), Problem.Maximise(N, subjectTo: N.Between(0, 5)));
        } finally {
            Console.SetOut(original);
        }

        Assert.Equal("", captured.ToString());
    }

    [Fact]
    public void AStartingSolutionIsTakenUpEvenThoughItLeavesTheAuxiliariesOut() {
        var slots = Enumerable.Range(0, 6).Select(index => Variable.Continuous($"slot{index}")).ToList();
        var separated = slots.SelectMany((first, index) => slots.Skip(index + 1).Select(second => (first + 90 <= second) | (second + 90 <= first)));
        var problem = Problem.Minimise(slots.Sum(), subjectTo: slots.AllOf(slot => slot.Between(0, 3600)) & separated.AllOf());
        var start = slots.Select((slot, index) => (slot, index)).Aggregate(Solution.Empty, (solution, entry) => solution.With(entry.slot, 100.0 * entry.index));
        var log = new System.Collections.Concurrent.ConcurrentQueue<string>();

        Assert.IsType<Optimal>(GurobiLicence.Require(GurobiSolver.Create(options: new SolverOptions(Log: log.Enqueue))).Solve(problem, startingFrom: start));

        Assert.Contains(log, line => line.Contains("MIP start", StringComparison.OrdinalIgnoreCase) && line.Contains("1500"));
    }

    [Fact]
    public void GurobiReadsBackModelFilesIndicatorsIncluded() {
        var slots = Enumerable.Range(0, 5).Select(index => Variable.Continuous($"slot {index}")).ToList();
        var separated = slots.SelectMany((first, index) => slots.Skip(index + 1).Select(second => ((first + 90 <= second) | (second + 90 <= first)).WithName($"headway {first.Name}/{second.Name}")));
        var problem = Problem.Minimise(slots.Sum() + A * slots[0] + 3, subjectTo: slots.AllOf(slot => slot.Between(0, 3600)) & separated.AllOf() & (!(A & B & C) | (slots[0] >= 50)) & A & B & C);
        var expected = Optimum(GurobiSolver.Create(), problem);

        Assert.Equal(expected, SolveFile(problem.EncodeLogic().ToLp(), "lp"), precision: 4);
        Assert.Equal(expected, SolveFile(problem.Encode().ToLp(), "lp"), precision: 4);
        Assert.Equal(expected, SolveFile(problem.Encode().ToMps(), "mps"), precision: 4);
    }

    private static double SolveFile(string contents, string extension) {
        var path = Path.Combine(Path.GetTempPath(), $"hephaestus-{Guid.NewGuid():N}.{extension}");
        File.WriteAllText(path, contents);
        try {
            using var environment = new global::Gurobi.GRBEnv(empty: true);
            environment.Set(global::Gurobi.GRB.IntParam.OutputFlag, 0);
            environment.Start();
            using var model = new global::Gurobi.GRBModel(environment, path);
            model.Optimize();
            return model.ObjVal;
        } finally {
            File.Delete(path);
        }
    }
}
