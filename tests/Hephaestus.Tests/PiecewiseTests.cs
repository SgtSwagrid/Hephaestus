using System.Collections.Immutable;
using static Hephaestus.Piecewise;

namespace Hephaestus.Tests;

public sealed class PiecewiseTests {
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly ContinuousVariable Y = Variable.Continuous("y");
    private static readonly ContinuousVariable Z = Variable.Continuous("z");
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly IntegerVariable M = Variable.Integer("m");
    private static readonly IBooleanExpression Box = X.Between(0, 10) & Y.Between(0, 10);

    private static IEnumerable<string> Rows(IProblem problem) => problem.Encode().Rows.Select(row => row.Format()).Order(StringComparer.Ordinal);

    private static IEnumerable<string> Auxiliaries(IProblem problem) => problem.Encode().Columns.Where(column => column.IsAuxiliary).Select(column => column.Variable.Name).Order(StringComparer.Ordinal);

    [Fact]
    public void TheFunctionsOnlyBuildData() {
        Assert.Equal(new Maximum(X, Y), Max(X, Y));
        Assert.Equal(new Minimum(X, new Constant(5)), Min(X, 5));
        Assert.Equal(new Maximum(new Constant(5), X), Max(5, X));
        Assert.Equal(new AbsoluteValue(X - Y), Abs(X - Y));
        Assert.Equal(new Maximum(new Maximum(X, Y), new Maximum(Z, N)), Max([X, Y, Z, N]));
        Assert.Equal(new Minimum(X, new Minimum(Y, Z)), Min([X, Y, Z]));
        Assert.Throws<InvalidOperationException>(() => Max(Array.Empty<ILinearExpression>()));
    }

    [Fact]
    public void TheyAreWrittenTheWayTheyAreRead() {
        Assert.Equal("max(x, y + 1) <= 2*abs(x - y)", (Max(X, Y + 1) <= 2 * Abs(X - Y)).Format());
        Assert.Equal("-min(x, 3)", (-Min(X, 3)).Format());
    }

    [Fact]
    public void TheirVariablesAreThoseOfTheirOperands() =>
        Assert.Equal<IVariable>([X, Y, Z], [.. (Max(X, Y) + Abs(Z) <= 1).Variables]);

    [Fact]
    public void TheyAreReadOffASolutionDirectly() {
        var solution = new Solution(ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer).Add(X, 3).Add(Y, -7), 0);

        Assert.Equal(3, solution.Value(Max(X, Y)));
        Assert.Equal(-7, solution.Value(Min(X, Y)));
        Assert.Equal(10, solution.Value(Abs(X - Y)));
        Assert.True(solution.Value(Abs(Y) >= 2 * Max(X, 0)));
    }

    [Fact]
    public void TheyHaveNoAffineFormOfTheirOwn() =>
        Assert.Contains("piecewise", Assert.Throws<ModellingException>(() => (X + Max(X, Y)).Normalise()).Message);

    [Fact]
    public void AMaximumThatIsPushedDownNeedsNoBinaryVariable() {
        var problem = Problem.Minimise(Max(X, Y), subjectTo: Box & (Max(X, Y) <= 8));

        Assert.Equal(["_max0"], Auxiliaries(problem));
        Assert.Equal(["-_max0 + x <= 0", "-_max0 + y <= 0"], Rows(problem));
        Assert.Equal(8, problem.Encode().Columns.Single(column => column.IsAuxiliary).UpperBound);
    }

    [Fact]
    public void AMaximumThatIsPushedUpNeedsTheChoiceOfWhichOperandItEquals() {
        var problem = Problem.Maximise(Max(X, Y), subjectTo: Box & (X + Y <= 12));

        Assert.Equal(["_aux0", "_max0"], Auxiliaries(problem));
        // Neither conditional row could be relaxed without an upper bound for _max0. It has the largest of its operands' upper bounds: 10.
        Assert.Equal(["-10*_aux0 + _max0 - y <= 0", "10*_aux0 + _max0 - x <= 10", "x + y <= 12"], Rows(problem));
    }

    [Fact]
    public void AMinimumIsAMaximumInDisguise() {
        var problem = Problem.Maximise(Min(X, Y), subjectTo: Box);

        // min(x, y) = -max(-x, -y), and maximising it pushes that maximum down.
        Assert.Equal("-_max0", problem.Encode().Objective.Format());
        Assert.Equal(["-_max0 - x <= 0", "-_max0 - y <= 0"], Rows(problem));
    }

    [Fact]
    public void AnAbsoluteValueBoundedFromAboveIsTwoPlainRows() =>
        Assert.Equal(["-_max0 + x - y <= 0", "-_max0 - x + y <= 0"], Rows(Problem.Satisfy(Box & (Abs(X - Y) <= 3))));

    [Fact]
    public void UsedBothWaysItIsTiedBothWays() =>
        Assert.Equal(["_aux0", "_max0"], Auxiliaries(Problem.Minimise(Abs(X - 5), subjectTo: X.Between(0, 10) & (Abs(X - 5) >= 2))));

    [Fact]
    public void EqualFunctionsShareOneVariableHoweverTheyWereWritten() =>
        Assert.Equal(["_max0", "_max1"], Auxiliaries(Problem.Satisfy(Box & (Max(X + Y, 3) <= 8) & (Max(Y + X, 3) + X <= 9) & (Abs(X) <= 4))));

    [Fact]
    public void FunctionsNestAndTheInnerOneIsTiedAsTheOuterOneNeeds() =>
        // Bounding max(x, min(y, z)) from above pushes the minimum down too, which it can only resist by choosing which operand it equals.
        Assert.Equal(["_aux0", "_max0", "_max1"], Auxiliaries(Problem.Satisfy(Box & Z.Between(0, 10) & (Max(X, Min(Y, Z)) <= 8))));

    [Fact]
    public void TheMaximumOfWholeNumbersIsAWholeNumber() {
        var encoded = Problem.Minimise(Max(N, 2 * M) + Max(N, 0.5 * M), subjectTo: N.Between(0, 5) & M.Between(0, 5)).Encode();

        Assert.IsType<IntegerVariable>(encoded.Columns.Single(column => column.Variable.Name == "_max0").Variable);
        Assert.IsType<ContinuousVariable>(encoded.Columns.Single(column => column.Variable.Name == "_max1").Variable);
    }

    [Fact]
    public void NamesStepAroundThoseAlreadyInUse() =>
        Assert.Equal(["_max1"], Auxiliaries(Problem.Minimise(Max(X, Variable.Continuous("_max0")), subjectTo: Box & Variable.Continuous("_max0").Between(0, 1))));

    [Fact]
    public void AProblemWithoutThemIsLeftAlone() {
        var problem = Problem.Minimise(X, subjectTo: Box);

        Assert.Same(problem, problem.Linearise().Problem);
        Assert.Empty(problem.Linearise().Definitions);
    }

    [Fact]
    public void TypedExpressionsHaveThemToo() {
        var origin = new DateTime(2026, 9, 19, 8, 0, 0);
        var (first, second) = (Variable.DateTime("first", origin), Variable.DateTime("second", origin.AddHours(1)));
        var delay = Variable.TimeSpan("delay", unit: TimeSpan.FromMinutes(1));
        var solution = new Solution(ImmutableSortedDictionary.Create<IVariable, double>(VariableOrder.Comparer).Add(Variable.Continuous("first"), 5400).Add(Variable.Continuous("second"), 600).Add(Variable.Continuous("delay"), -3), 0);

        Assert.Equal(origin.AddMinutes(90), solution.Value(Max(first, second)));
        Assert.Equal(origin.AddMinutes(70), solution.Value(Min(first, second)));
        Assert.Equal(origin.AddMinutes(100), solution.Value(Max(first, origin.AddMinutes(100))));
        Assert.Equal(TimeSpan.FromMinutes(3), solution.Value(Abs(delay)));
        Assert.Equal(TimeSpan.FromMinutes(20), solution.Value(Max(delay, first - second)));
        Assert.Equal(TimeSpan.FromMinutes(-3), solution.Value(Min([delay, first - second, Abs(delay)])));
    }
}
