namespace Hephaestus.Tests;

public sealed class ModelFilesTests {
    private static readonly ContinuousVariable DepartureA = Variable.Continuous("departureA");
    private static readonly ContinuousVariable DepartureB = Variable.Continuous("departureB");
    private static readonly BinaryVariable OccupiesA = Variable.Binary("occupiesA");
    private static readonly BinaryVariable OccupiesB = Variable.Binary("occupiesB");
    private static readonly IntegerVariable N = Variable.Integer("n");

    private static readonly IBooleanExpression Headway =
        (!(OccupiesA & OccupiesB) | (DepartureA + 120 <= DepartureB) | (DepartureB + 120 <= DepartureA)).WithName("headway A/B");

    private static readonly IProblem Problem1 = Problem.Minimise(
        DepartureA + 2 * DepartureB + 7,
        subjectTo: DepartureA.Between(0, 3600) & DepartureB.Between(0, 3600) & Headway & OccupiesA & (DepartureA - DepartureB + N).EqualTo(3) & (N >= -4));

    private static string[] Lines(string text) => text.TrimEnd('\n').Split('\n');

    [Fact]
    public void RowsKnowTheConstraintTheyWereEncodedFrom() {
        var encoded = Problem1.Encode();

        Assert.Equal(["headway A/B", "headway A/B", "departureA - departureB + n == 3"], encoded.Rows.Select(row => row.Origin!.Name));
        Assert.Equal(encoded.Rows.Select(row => row.Origin), Problem1.EncodeLogic().RelaxGuards().Rows.Select(row => row.Origin));
    }

    [Fact]
    public void AProgrammeIsWrittenInLpFormat() =>
        Assert.Equal(
            [
                "\\ Written by Hephaestus.",
                "Minimize",
                " obj: 1 departureA + 2 departureB + 7 constant_one",
                "Subject To",
                "\\ headway A/B",
                " headway_A_B: 3720 x__aux0 + 1 departureA - 1 departureB <= 3600",
                "\\ headway A/B",
                " headway_A_B_2: -3720 x__aux0 - 1 departureA + 1 departureB + 3720 occupiesA + 3720 occupiesB <= 7320",
                "\\ departureA - departureB + n == 3",
                " c2: 1 departureA - 1 departureB + 1 n = 3",
                "Bounds",
                " constant_one = 1",
                " 0 <= departureA <= 3600",
                " 0 <= departureB <= 3600",
                " -4 <= n <= +inf",
                " occupiesA = 1",
                "Binaries",
                " occupiesB",
                " x__aux0",
                "Generals",
                " n",
                " occupiesA",
                "End",
            ],
            Lines(Problem1.Encode().ToLp()));

    [Fact]
    public void WithoutBigMTheConditionalRowsAreWrittenAsIndicators() {
        var lines = Lines(Problem1.EncodeLogic().ToLp());

        Assert.Contains(" headway_A_B: x__aux0 = 1 -> 1 departureA - 1 departureB <= -120", lines);
        Assert.Contains(lines, line => line.Contains("x__all0 = 1 -> ") && line.Contains("departureB <= -120"));
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
                // departureA + n >= 2 is held as -departureA - n <= -2.
                " L  c1",
                " E  c2",
                "COLUMNS",
                "    departureA obj 1",
                "    departureA capacity 1",
                "    departureA c1 -1",
                "    departureA c2 1",
                "    departureB obj 0",
                "    departureB capacity 1",
                "    departureB c2 -2",
                "    MARKER2 'MARKER' 'INTORG'",
                "    n obj -2",
                "    n c1 -1",
                "    MARKER2 'MARKER' 'INTEND'",
                "    MARKER3 'MARKER' 'INTORG'",
                "    occupiesA obj 0",
                "    MARKER3 'MARKER' 'INTEND'",
                "RHS",
                "    rhs obj -5",
                "    rhs capacity 10",
                "    rhs c1 -2",
                "    rhs c2 0",
                "BOUNDS",
                " MI bnd departureA",
                " UP bnd departureA 8",
                " FR bnd departureB",
                " LO bnd n 0",
                " UP bnd n 3",
                " FX bnd occupiesA 0",
                "ENDATA",
            ],
            Lines(Problem.Maximise(DepartureA - 2 * N + 5, subjectTo: (DepartureA + DepartureB <= 10).WithName("capacity") & (DepartureA + N >= 2) & (DepartureA - 2 * DepartureB).EqualTo(0) & (DepartureA <= 8) & N.Between(0, 3) & !OccupiesA).Encode().ToMps()));

    [Fact]
    public void NamesAreMadeSafeAndKeptApart() {
        var awkward = new[] { Variable.Continuous("x y"), Variable.Continuous("x-y"), Variable.Continuous("2nd"), Variable.Continuous("e1") };
        var lines = Lines(Problem.Minimise(awkward.Sum(), subjectTo: awkward.AllOf(variable => variable >= 1) & (awkward.Sum() >= 9).WithName("sum") & (awkward.Sum() <= 99).WithName("sum")).Encode().ToLp());

        Assert.Equal(" obj: 1 x_2nd + 1 x_e1 + 1 x_y + 1 x_y_2", lines[2]);
        Assert.Contains(lines, line => line.StartsWith(" sum: ", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith(" sum_2: ", StringComparison.Ordinal));
    }

    [Fact]
    public void ARowWithTwoSidesIsARangeInMpsAndTwoRowsInLp() {
        var programme = Problem.Minimise(DepartureA, subjectTo: (DepartureA >= 0) & (DepartureB >= 0)).Encode() with {
            Rows = [new LinearRow(Solution.Empty.Values.Add(DepartureA, 1).Add(DepartureB, 1), 2, 6)],
        };

        Assert.Contains("    rng c0 4", Lines(programme.ToMps()));
        Assert.Contains(" L  c0", Lines(programme.ToMps()));
        Assert.Equal(2, Lines(programme.ToLp()).Count(line => line.StartsWith(" c", StringComparison.Ordinal) && line.Contains("<=")));
    }
}
