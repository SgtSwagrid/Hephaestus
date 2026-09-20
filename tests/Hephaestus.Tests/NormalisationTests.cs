using System.Collections.Immutable;

namespace Hephaestus.Tests;

public sealed class NormalisationTests {
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly ContinuousVariable Y = Variable.Continuous("y");
    private static readonly IntegerVariable N = Variable.Integer("n");
    private static readonly BinaryVariable A = Variable.Binary("a");
    private static readonly BinaryVariable B = Variable.Binary("b");

    private static AffineForm Form(double constant, params (IVariable Variable, double Coefficient)[] terms) =>
        terms.Aggregate(AffineForm.Zero.Plus(constant), (form, term) => form.PlusTerm(term.Variable, term.Coefficient));

    [Fact]
    public void LinearExpressionsFlattenToTheirAffineForm() {
        Assert.Equal(Form(6, (X, 1)), (2 * (X + 3) - X).Normalise());
        Assert.Equal(Form(-1, (X, 0.5), (Y, -3)), ((X - 2) / 2 - 3 * Y).Normalise());
        Assert.Equal(Form(0, (X, 1)), X.Normalise());
    }

    [Fact]
    public void TermsThatCancelDisappear() {
        Assert.Equal(AffineForm.Zero, (X - X).Normalise());
        Assert.Equal(Form(0, (Y, 1)), (X + Y - X).Normalise());
        Assert.Equal(AffineForm.Zero, (0 * (X + 7)).Normalise());
    }

    [Fact]
    public void ExpressionsThatDenoteTheSameFunctionHaveEqualForms() {
        Assert.Equal((X + Y).Normalise(), (Y + X).Normalise());
        Assert.Equal((2 * (X + Y)).Normalise().GetHashCode(), (2 * Y + 2 * X).Normalise().GetHashCode());
        Assert.NotEqual((X + Y).Normalise(), (X - Y).Normalise());
    }

    [Fact]
    public void NonFiniteNumbersAreRejectedWhenNormalising() {
        Assert.Throws<ModellingException>(() => (X / 0).Normalise());
        Assert.Throws<ModellingException>(() => (X + double.NaN).Normalise());
    }

    [Fact]
    public void AVeryLongChainOfAdditionsDoesNotOverflowTheStack() {
        var chain = Enumerable.Range(0, 200_000).Aggregate<int, ILinearExpression>(new Constant(0), (sum, index) => sum + Variable.Continuous($"v{index % 100}"));

        var form = chain.Normalise();

        Assert.Equal(100, form.Coefficients.Count);
        Assert.All(form.Coefficients.Values, coefficient => Assert.Equal(2000, coefficient));
    }

    [Fact]
    public void RangesFollowFromIntervalArithmetic() {
        var bounds = ImmutableDictionary<IVariable, Interval>.Empty.Add(X, new Interval(0, 10)).Add(Y, new Interval(-2, 3));

        Assert.Equal(new Interval(5 - 9, 5 + 20 + 6), (2 * X - 3 * Y + 5).Normalise().Range(variable => bounds[variable]));
        Assert.Equal(new Interval(double.NegativeInfinity, double.PositiveInfinity), X.Normalise().Range(_ => Interval.Unbounded));
    }

    [Fact]
    public void IntegralityIsRecognised() {
        Assert.True((2 * N + A - 3).Normalise().IsIntegral);
        Assert.False((0.5 * N).Normalise().IsIntegral);
        Assert.False((N + X).Normalise().IsIntegral);
        Assert.False((N + 0.5).Normalise().IsIntegral);
    }

    [Fact]
    public void NegationsArePushedToTheLeaves() {
        var atom = new Atom(Form(-1, (X, 1)), IsEquality: false);
        var flipped = new Atom(Form(1 + 1e-4, (X, -1)), IsEquality: false);

        Assert.Equal(new All([new Literal(A, true), atom]), (A & (X <= 1)).Normalise(1e-4));
        Assert.Equal(new Any([new Literal(A, false), flipped]), (!(A & (X <= 1))).Normalise(1e-4));
        Assert.Equal(new Literal(A, true), (!!A).Normalise(1e-4));
    }

    [Fact]
    public void NestedJunctionsOfTheSameKindAreFlattened() {
        var c = Variable.Binary("c");

        Assert.Equal(new Any([new Literal(A, true), new Literal(B, true), new Literal(c, true)]), (A | (B | c)).Normalise(1e-4));
        Assert.Equal(new Any([new Literal(A, false), new Literal(B, false), new Literal(c, true)]), (!(A & B) | c).Normalise(1e-4));
        Assert.Equal(new Any([new Literal(A, false), new Literal(B, true)]), A.Implies(B).Normalise(1e-4));
    }

    [Fact]
    public void ConstantsAreAbsorbed() {
        Assert.Equal(BooleanNormalisation.True, (A | BooleanConstant.True).Normalise(1e-4));
        Assert.Equal(new Literal(A, true), (A & BooleanConstant.True).Normalise(1e-4));
        Assert.Equal(BooleanNormalisation.False, (A & (new Constant(2) <= 1)).Normalise(1e-4));
        Assert.Equal(new Literal(A, true), (A | (X - X > 0)).Normalise(1e-4));
    }

    [Fact]
    public void StrictnessOverWholeNumbersIsExactAndOverRealsUsesEpsilon() {
        Assert.Equal(new Atom(Form(-2, (N, 1)), IsEquality: false), (N < 3).Normalise(1e-4));
        Assert.Equal(new Atom(Form(-3 + 1e-4, (X, 1)), IsEquality: false), (X < 3).Normalise(1e-4));
        Assert.Equal(new Atom(Form(4, (N, -1)), IsEquality: false), (!(N <= 3)).Normalise(1e-4));
    }

    [Fact]
    public void EqualitiesAreSignNormalisedAndDisequalitiesSplit() {
        Assert.Equal(X.EqualTo(Y).Normalise(1e-4), Y.EqualTo(X).Normalise(1e-4));
        Assert.Equal(
            new Any([new Atom(Form(-2, (N, 1)), IsEquality: false), new Atom(Form(4, (N, -1)), IsEquality: false)]),
            N.NotEqualTo(3).Normalise(1e-4));
    }

    [Theory]
    [InlineData(Relation.LessThan, Relation.GreaterThanOrEqual)]
    [InlineData(Relation.LessThanOrEqual, Relation.GreaterThan)]
    [InlineData(Relation.Equal, Relation.NotEqual)]
    [InlineData(Relation.NotEqual, Relation.Equal)]
    [InlineData(Relation.GreaterThanOrEqual, Relation.LessThan)]
    [InlineData(Relation.GreaterThan, Relation.LessThanOrEqual)]
    public void EveryRelationHasAnOpposite(Relation relation, Relation opposite) {
        Assert.Equal(opposite, BooleanNormalisation.Opposite(relation));
        Assert.Equal(relation, BooleanNormalisation.Opposite(opposite));
    }

    [Fact]
    public void AnUnknownRelationHasNoOpposite() =>
        Assert.Throws<NotSupportedException>(() => BooleanNormalisation.Opposite((Relation)99));

    [Fact]
    public void AVeryLongChainOfConjunctionsDoesNotOverflowTheStack() {
        var chain = Enumerable.Range(0, 100_000).Aggregate<int, IBooleanExpression>(BooleanConstant.True, (all, index) => all & (Variable.Continuous($"v{index}") <= index));

        var normalised = Assert.IsType<All>(chain.Normalise(1e-4));

        Assert.Equal(100_000, normalised.Operands.Count);
    }
}
