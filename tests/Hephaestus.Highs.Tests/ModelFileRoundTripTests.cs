using Highs;
using static Hephaestus.Piecewise;

namespace Hephaestus.Highs.Tests;

/// <summary>A model file is right if a solver that reads it finds the optimum that solving the problem directly does.</summary>
public sealed class ModelFileRoundTripTests {
    private static readonly ContinuousVariable A = Variable.Continuous("start A");
    private static readonly ContinuousVariable B = Variable.Continuous("start B");
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly BinaryVariable Runs = Variable.Binary("runs");

    public static TheoryData<string, IOneShotProblem> Problems => new() {
        { "changeover", Problem.Minimise(A + 2 * B + 7).SubjectTo(A.Between(0, 3600) & B.Between(30, 3600) & (((A + 120 <= B) | (B + 120 <= A)).WithName("changeover")) & (A >= 10)) },
        { "maximised", Problem.Maximise(3 * A + 5 * B - 2 * N + 1.5).SubjectTo((A <= 4) & (2 * B <= 12) & (3 * A + 2 * B <= 18) & (A >= 0) & (B >= 0) & N.Between(-3, 3) & (A + N >= 0.5)) },
        { "free and fixed", Problem.Minimise(Abs(A - 5) + B).SubjectTo((A - 2 * B).EqualTo(1) & (B >= -8) & Runs & (Runs * N >= 2) & N.Between(0, 9)) },
        { "whole numbers", Problem.Maximise(5 * N + 4 * Variable.Integer("m") - Runs).SubjectTo((N >= 0) & (Variable.Integer("m") >= 0) & (2 * N + 3 * Variable.Integer("m") <= 12) & (4 * N + Variable.Integer("m") <= 11) & Runs.Iff(N >= 2)) },
    };

    private static double SolveFile(string contents, string extension) {
        var path = Path.Combine(Path.GetTempPath(), $"hephaestus-{Guid.NewGuid():N}.{extension}");
        File.WriteAllText(path, contents);
        try {
            using var solver = new HighsLpSolver();
            solver.setBoolOptionValue("output_flag", 0);
            Assert.NotEqual(HighsStatus.kError, solver.readModel(path));
            Assert.NotEqual(HighsStatus.kError, solver.run());
            Assert.Equal(HighsModelStatus.kOptimal, solver.GetModelStatus());
            return solver.getInfo().ObjectiveValue;
        } finally {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(Problems))]
    public void HighsReadsBackWhatWasWritten(string name, IOneShotProblem problem) {
        var expected = Assert.IsType<Optimal>(HighsSolver.Create().Solve(problem)).Solution.ObjectiveValue;

        Assert.Equal(expected, SolveFile(problem.Encode().ToLp(), "lp"), precision: 4);
        Assert.Equal(expected, SolveFile(problem.Encode().ToMps(name), "mps"), precision: 4);
    }
}
