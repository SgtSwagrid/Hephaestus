namespace Hephaestus.Tests;

public sealed class ModelFilesTests {
    private static readonly ContinuousVariable StartA = Variable.Continuous("startA");
    private static readonly ContinuousVariable StartB = Variable.Continuous("startB");
    private static readonly BinaryVariable UsesA = Variable.Binary("usesA");
    private static readonly BinaryVariable UsesB = Variable.Binary("usesB");
    private static readonly IntegerVariable N = Variable.Integer("n");

    private static readonly IBooleanExpression Changeover =
        (!(UsesA & UsesB) | (StartA + 120 <= StartB) | (StartB + 120 <= StartA)).WithName("changeover A/B");

    private static readonly IOneShotProblem Problem1 = Problem.Minimise(StartA + 2 * StartB + 7).SubjectTo(StartA.Between(0, 3600) & StartB.Between(0, 3600) & Changeover & UsesA & (StartA - StartB + N).EqualTo(3) & (N >= -4));

    private static string[] Lines(string text) => text.TrimEnd('\n').Split('\n');


    [Fact]
    public void RowsKnowTheConstraintTheyWereEncodedFrom() {
        var encoded = Problem1.Encode();

        Assert.Equal(["changeover A/B", "changeover A/B", "startA - startB + n == 3"], encoded.Rows.Select(row => row.Origin!.Name));
        Assert.Equal(encoded.Rows.Select(row => row.Origin), Problem1.EncodeLogic().RelaxGuards().Rows.Select(row => row.Origin));
    }

    [Fact]
    public void AProgrammeIsWrittenInLpFormat() =>
        Assert.Equal(
            [
                "\\ Written by Hephaestus.",
                "Minimize",
                " obj: 1 startA + 2 startB + 7 constant_one",
                "Subject To",
                "\\ changeover A/B",
                " changeover_A_B: 3720 x__aux0 + 1 startA - 1 startB <= 3600",
                "\\ changeover A/B",
                " changeover_A_B_2: -3720 x__aux0 - 1 startA + 1 startB + 3720 usesA + 3720 usesB <= 7320",
                "\\ startA - startB + n == 3",
                " c2: 1 n + 1 startA - 1 startB = 3",
                "Bounds",
                " constant_one = 1",
                " -4 <= n <= +inf",
                " 0 <= startA <= 3600",
                " 0 <= startB <= 3600",
                " usesA = 1",
                "Binaries",
                " usesB",
                " x__aux0",
                "Generals",
                " n",
                " usesA",
                "End",
            ],
            Lines(Problem1.Encode().ToLp()));

    [Fact]
    public void WithoutBigMTheConditionalRowsAreWrittenAsIndicators() {
        var lines = Lines(Problem1.EncodeLogic().ToLp());

        Assert.Contains(" changeover_A_B: x__aux0 = 1 -> 1 startA - 1 startB <= -120", lines);
        Assert.Contains(lines, line => line.Contains("x__all0 = 1 -> ") && line.Contains("startB <= -120"));
        Assert.Contains(" x__all0", lines);
    }

    [Fact]
    public void AProgrammeIsWrittenInMpsFormat() =>
        Assert.Equal(
            [
                "NAME hephaestus",
                "OBJSENSE",
                "    MAX",
                "ROWS",
                " N  obj",
                " L  capacity",
                // startA + n >= 2 is held as -startA - n <= -2.
                " L  c1",
                " E  c2",
                "COLUMNS",
                "    MARKER0 'MARKER' 'INTORG'",
                "    n obj -2",
                "    n c1 -1",
                "    MARKER0 'MARKER' 'INTEND'",
                "    startA obj 1",
                "    startA capacity 1",
                "    startA c1 -1",
                "    startA c2 1",
                "    startB obj 0",
                "    startB capacity 1",
                "    startB c2 -2",
                "    MARKER3 'MARKER' 'INTORG'",
                "    usesA obj 0",
                "    MARKER3 'MARKER' 'INTEND'",
                "RHS",
                "    rhs obj -5",
                "    rhs capacity 10",
                "    rhs c1 -2",
                "    rhs c2 0",
                "BOUNDS",
                " LO bnd n 0",
                " UP bnd n 3",
                " MI bnd startA",
                " UP bnd startA 8",
                " FR bnd startB",
                " FX bnd usesA 0",
                "ENDATA",
            ],
            Lines(Problem.Maximise(StartA - 2 * N + 5).SubjectTo((StartA + StartB <= 10).WithName("capacity") & (StartA + N >= 2) & (StartA - 2 * StartB).EqualTo(0) & (StartA <= 8) & N.Between(0, 3) & !UsesA).Encode().ToMps()));

    [Fact]
    public void NamesAreMadeSafeAndKeptApart() {
        var awkward = new[] { Variable.Continuous("x y"), Variable.Continuous("x-y"), Variable.Continuous("2nd"), Variable.Continuous("e1") };
        var lines = Lines(Problem.Minimise(awkward.Sum()).SubjectTo(awkward.AllOf(variable => variable >= 1) & (awkward.Sum() >= 9).WithName("sum") & (awkward.Sum() <= 99).WithName("sum")).Encode().ToLp());

        Assert.Equal(" obj: 1 x_2nd + 1 x_e1 + 1 x_y + 1 x_y_2", lines[2]);
        Assert.Contains(lines, line => line.StartsWith(" sum: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(" sum_2: ", StringComparison.Ordinal));
    }

    [Fact]
    public void ARowWithTwoSidesIsARangeInMpsAndTwoRowsInLp() {
        var programme = Problem.Minimise(StartA).SubjectTo((StartA >= 0) & (StartB >= 0)).Encode() with {
            Rows = [new LinearRow(Solution.Empty.Values.Add(StartA, 1).Add(StartB, 1), 2, 6)],
        };

        Assert.Contains("    rng c0 4", Lines(programme.ToMps()));
        Assert.Contains(" L  c0", Lines(programme.ToMps()));
        Assert.Equal(2, Lines(programme.ToLp()).Count(line => line.StartsWith(" c", StringComparison.Ordinal) && line.Contains("<=")));
    }
}

