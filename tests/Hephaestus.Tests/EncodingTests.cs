namespace Hephaestus.Tests;

public sealed class EncodingTests {
    private static readonly ContinuousVariable DepartureA = Variable.Continuous("departureA");
    private static readonly ContinuousVariable DepartureB = Variable.Continuous("departureB");
    private static readonly BinaryVariable OccupiesA = Variable.Binary("occupiesA");
    private static readonly BinaryVariable OccupiesB = Variable.Binary("occupiesB");
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly ContinuousVariable Y = Variable.Continuous("y");
    private static readonly IntegerVariable N = Variable.Integer("n");

    private const double Headway = 120;
    private const double Horizon = 3600;

    private static IBooleanExpression HeadwayConstraint =>
        DepartureA.Between(0, Horizon)
        & DepartureB.Between(0, Horizon)
        & (!(OccupiesA & OccupiesB) | (DepartureA + Headway <= DepartureB) | (DepartureB + Headway <= DepartureA));

    [Fact]
    public void TheHeadwayExampleBecomesTheTextbookEitherOrWithASingleAuxiliary() {
        var encoded = Problem.Satisfy(HeadwayConstraint).Encode();

        Assert.Equal(["_aux0"], encoded.Columns.Where(column => column.IsAuxiliary).Select(column => column.Variable.Name));
        Assert.Equal(
            [
                // _aux0 = 1 forces A before B ...
                "3720*_aux0 + departureA - departureB <= 3600",
                // ... and otherwise, if both trains occupy the track, B goes before A.
                "-3720*_aux0 - departureA + departureB + 3720*occupiesA + 3720*occupiesB <= 7320",
            ],
            encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void BigMIsTheHandComputedValueFromTheOriginalStory() {
        // The story's command passed `_headwayTime + ModelEndTime`; here it falls out of the bounds.
        var encoded = Problem.Satisfy(HeadwayConstraint).Encode();

        Assert.All(
            encoded.Rows.Where(row => row.Coefficients.Keys.Any(variable => variable.Name.StartsWith("_aux")) && row.Coefficients.ContainsKey(DepartureA)),
            row => Assert.Contains(row.Coefficients.Values, coefficient => Math.Abs(coefficient) == Headway + Horizon));
    }

    [Fact]
    public void StatedBoundsBecomeColumnBoundsRatherThanRows() {
        var encoded = Problem.Satisfy(X.Between(2, 10) & (X + Y <= 12) & (Y >= -1)).Encode();

        Assert.Equal(new Column(X, 2, 10, IsAuxiliary: false), encoded.Columns.Single(column => column.Variable.Equals(X)));
        Assert.Equal(new Column(Y, -1, double.PositiveInfinity, IsAuxiliary: false), encoded.Columns.Single(column => column.Variable.Equals(Y)));
        Assert.Equal(["x + y <= 12"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void BoundsOnWholeNumbersRoundInwards() {
        var encoded = Problem.Satisfy((2 * N <= 9) & (N > 0.5) & (N < 4)).Encode();

        Assert.Equal(new Column(N, 1, 3, IsAuxiliary: false), encoded.Columns.Single());
    }

    [Fact]
    public void RoundingInwardsForgivesDustButNotGenuineFractionsHoweverLargeTheNumber() {
        var encoded = Problem.Satisfy((N <= 999_999.9995) & (N >= 0.1 + 0.2 - 0.3 + 5)).Encode();

        Assert.Equal(new Column(N, 5, 999_999, IsAuxiliary: false), encoded.Columns.Single());
    }

    [Fact]
    public void BinaryVariablesAssertedAtTheTopLevelAreFixedByTheirBounds() {
        var encoded = Problem.Satisfy(OccupiesA & !OccupiesB).Encode();

        Assert.Equal([new Column(OccupiesA, 1, 1, false), new Column(OccupiesB, 0, 0, false)], encoded.Columns);
        Assert.Empty(encoded.Rows);
    }

    [Fact]
    public void BigMUsesBoundsThatAreOnlyImplied() {
        var arrival = Variable.Continuous("arrival");
        var run = Variable.Continuous("run");
        var late = Variable.Binary("late");
        var constraint =
            DepartureA.Between(0, 100)
            & run.Between(10, 20)
            & arrival.EqualTo(DepartureA + run)
            & late.Iff(arrival >= 90);

        var encoded = Problem.Satisfy(constraint).Encode();

        // arrival is never bounded directly, but lies in [10, 120] because departureA and run are bounded.
        Assert.Contains("-arrival + 80*late <= -10", encoded.Rows.Select(row => row.Format()));
        Assert.Contains("arrival - 30.0001*late <= 89.9999", encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void EachGuardGetsABigMDerivedWithThatGuardOff() {
        // A row that counts the very binary guarding it: with occupiesA off the row reaches 95, not the 105 it reaches unconditionally.
        var constraint = X.Between(0, 100) & OccupiesA.Implies(X + 10 * (ILinearExpression)OccupiesA <= 5);

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Equal(["105*occupiesA + x <= 100"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void TheGuardsOfOneRowGetBigMValuesOfTheirOwn() {
        var constraint = N.Between(0, 6) & (!OccupiesA | !OccupiesB | (N + 3 * (ILinearExpression)OccupiesA <= 4));

        var encoded = Problem.Satisfy(constraint).Encode();

        // With occupiesA off the row reaches 2 and with occupiesB off it reaches 5, so the two slacks are weighted apart.
        Assert.Equal(["n + 5*occupiesA + 5*occupiesB <= 11"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void AGuardThatTheRowHoldsWithoutIsNotRelaxedAgainstAtAll() {
        // Without occupiesA the row is 0 <= x, which the bounds already say, so no big-M is needed and the row stays unconditional.
        var constraint = X.Between(0, 10) & OccupiesA.Implies(5 * (ILinearExpression)OccupiesA <= X);

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Equal(["5*occupiesA - x <= 0"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void TheRelaxedRowsAdmitExactlyTheAssignmentsTheConstraintDoes() {
        var constraint = N.Between(0, 6) & (!OccupiesA | !OccupiesB | (N + 3 * (ILinearExpression)OccupiesA <= 4));

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.All(
            Assignments(encoded.Columns),
            assignment => Assert.Equal(assignment.Value(constraint), Admits(encoded, assignment)));
    }

    /// <summary>Every whole-number assignment within the column bounds, which is all of them when every column is bounded and whole.</summary>
    private static IEnumerable<Solution> Assignments(IEnumerable<Column> columns) =>
        columns.Aggregate<Column, IEnumerable<Solution>>(
            [Solution.Empty],
            (partial, column) => partial.SelectMany(assignment =>
                Enumerable
                    .Range((int)column.LowerBound, (int)(column.UpperBound - column.LowerBound) + 1)
                    .Select(value => assignment.With(column.Variable, value))));

    private static bool Admits(MilpProblem problem, Solution assignment) =>
        problem.Rows.All(row => Within(row, new AffineForm(row.Coefficients, 0).Evaluate(variable => assignment.Value(variable))));

    private static bool Within(LinearRow row, double activity) => row.LowerBound <= activity && activity <= row.UpperBound;

    [Fact]
    public void AnUnderivableBigMIsALoudErrorNamingTheCulprits() {
        var constraint = X.Between(0, 10) & ((X + Y <= 5) | (X >= 8));

        var exception = Assert.Throws<ModellingException>(() => Problem.Satisfy(constraint).Encode());

        Assert.Contains("'y'", exception.Message);
        Assert.DoesNotContain("'x'", exception.Message);
    }

    [Fact]
    public void AFallbackBigMIsUsedOnlyWhereNoneCanBeDerived() {
        var constraint = X.Between(0, 10) & ((X + Y <= 5) | (X >= 8));

        var encoded = Problem.Satisfy(constraint).Encode(new EncodingOptions(FallbackBigM: 1e6));

        Assert.Equal(["1000000*_aux0 + x + y <= 1000005", "-8*_aux0 - x <= -8"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void ABigMBeyondTheStatedLimitIsALoudErrorNamingTheWidestVariables() {
        var constraint = X.Between(0, 1e9) & (OccupiesA | (X <= 5));

        var exception = Assert.Throws<ModellingException>(() => Problem.Satisfy(constraint).Encode(new EncodingOptions(MaximumBigM: 1e6)));

        Assert.Contains("'x'", exception.Message);
        Assert.Contains(nameof(EncodingOptions.MaximumBigM), exception.Message);
    }

    [Fact]
    public void ABigMWithinTheStatedLimitPassesWithoutComment() {
        var constraint = X.Between(0, 1e9) & (OccupiesA | (X <= 5));

        var encoded = Problem.Satisfy(constraint).Encode(new EncodingOptions(MaximumBigM: 1e10));

        Assert.Equal(["-999999995*occupiesA + x <= 5"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void NoLimitIsTheDefault() =>
        Assert.Equal(
            ["-999999995*occupiesA + x <= 5"],
            Problem.Satisfy(X.Between(0, 1e9) & (OccupiesA | (X <= 5))).Encode().Rows.Select(row => row.Format()));

    [Fact]
    public void EqualSubformulasShareOneAuxiliary() {
        var constraint = X.Between(0, 10) & Y.Between(0, 10) & ((X + Y <= 5) | (X >= 8)) & ((Y + X <= 5) | (Y >= 8));

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Single(encoded.Columns, column => column.IsAuxiliary);
    }

    [Fact]
    public void ConditionalRowsThatCanNeverBindAreDropped() {
        var constraint = X.Between(0, 10) & (OccupiesA | (X <= 20));

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Empty(encoded.Rows);
    }

    [Fact]
    public void ConditionalRowsThatCanNeverHoldForbidTheirGuards() {
        var constraint = X.Between(0, 10) & (OccupiesA | (X >= 20));

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Equal(["-occupiesA <= -1"], encoded.Rows.Select(row => row.Format()));
    }

    [Fact]
    public void AuxiliaryNamesStepAroundNamesAlreadyInUse() {
        var squatter = Variable.Continuous("_aux0");
        var constraint = squatter.Between(0, 1) & X.Between(0, 10) & ((X <= 3) | (X >= 7));

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Equal(["_aux1"], encoded.Columns.Where(column => column.IsAuxiliary).Select(column => column.Variable.Name));
    }

    [Fact]
    public void VariablesOfDifferentKindsMayNotShareAName() =>
        Assert.Throws<ModellingException>(() => Problem.Satisfy((Variable.Integer("x") <= 3) & (X >= 1)).Encode());

    [Fact]
    public void EvidentInfeasibilityIsRecognisedWithoutASolver() {
        Assert.True(Problem.Satisfy((X <= 1) & (X >= 2)).Encode().IsTriviallyInfeasible);
        Assert.True(Problem.Satisfy(BooleanConstant.False).Encode().IsTriviallyInfeasible);
        Assert.True(Problem.Satisfy((X <= 1) & !(X <= 1 | OccupiesA | !OccupiesA)).Encode().IsTriviallyInfeasible);
        Assert.False(Problem.Satisfy((X <= 2) & (X >= 1)).Encode().IsTriviallyInfeasible);
    }

    [Fact]
    public void TheObjectiveIsCarriedAcrossInNormalForm() {
        var encoded = Problem.Maximise(2 * (X + Y) - X + 7).SubjectTo(X.Between(0, 1) & Y.Between(0, 1)).Encode();

        Assert.Equal(ObjectiveSense.Maximise, encoded.Sense);
        Assert.Equal("x + 2*y + 7", encoded.Objective.Format());
    }

    [Fact]
    public void VariablesThatOnlyAppearInTheObjectiveStillGetColumns() {
        var encoded = Problem.Minimise(X + Y).SubjectTo(X >= 0).Encode();

        Assert.Equal([X, Y], encoded.Columns.Select(column => column.Variable));
    }

    [Fact]
    public void AHundredThousandConstraintsEncodeWithoutDrama() {
        var variables = Enumerable.Range(0, 1000).Select(index => Variable.Continuous($"v{index:D4}")).ToList();
        var constraint = Enumerable.Range(0, 100_000).AllOf(index => variables[index % 1000] + variables[(index + 1) % 1000] <= index);

        var encoded = Problem.Satisfy(constraint).Encode();

        Assert.Equal(100_000, encoded.Rows.Length);
    }

    [Fact]
    public void TheEncodedProgrammeCanBePrinted() {
        var encoded = Problem.Minimise(X).SubjectTo(X.Between(0, 10) & ((X <= 3) | (X >= 7))).Encode();

        Assert.Equal(
            string.Join(Environment.NewLine, [
                "minimise x",
                "subject to",
                "  7*_aux0 + x <= 10",
                "  -7*_aux0 - x <= -7",
                "where",
                "  0 <= x <= 10, continuous",
                "  0 <= _aux0 <= 1, binary",
            ]),
            encoded.Format());
    }
}
