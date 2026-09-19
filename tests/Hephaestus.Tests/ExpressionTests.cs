namespace Hephaestus.Tests;

/// <summary>Operators build plain data, exactly as written; nothing is simplified at construction.</summary>
public sealed class ExpressionTests {
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly ContinuousVariable Y = Variable.Continuous("y");
    private static readonly BinaryVariable A = Variable.Binary("a");
    private static readonly BinaryVariable B = Variable.Binary("b");

    [Fact]
    public void ArithmeticOperatorsBuildTheTreeThatWasWritten() {
        Assert.Equal(new Sum(X, Y), X + Y);
        Assert.Equal(new Sum(X, new Constant(5)), X + 5);
        Assert.Equal(new Sum(new Constant(5), X), 5 + X);
        Assert.Equal(new Sum(X, new Product(-1, Y)), X - Y);
        Assert.Equal(new Sum(X, new Constant(-5)), X - 5);
        Assert.Equal(new Product(-1, X), -X);
        Assert.Equal(new Product(2, X), 2 * X);
        Assert.Equal(new Product(2, X), X * 2);
        Assert.Equal(new Product(0.25, X), X / 4);
    }

    [Fact]
    public void NothingIsSimplifiedAtConstruction() {
        Assert.Equal(new Sum(new Sum(X, new Constant(0)), new Product(-1, X)), X + 0 - X);
        Assert.Equal(new Negation(new Negation(A)), !!A);
    }

    [Fact]
    public void ComparisonOperatorsBuildComparisons() {
        Assert.Equal(new Comparison(X, Relation.LessThanOrEqual, Y), X <= Y);
        Assert.Equal(new Comparison(X, Relation.GreaterThanOrEqual, new Constant(3)), X >= 3);
        Assert.Equal(new Comparison(new Constant(3), Relation.LessThan, X), 3 < X);
        Assert.Equal(new Comparison(X, Relation.GreaterThan, Y), X > Y);
        Assert.Equal(new Comparison(X, Relation.Equal, Y), X.EqualTo(Y));
        Assert.Equal(new Comparison(X, Relation.NotEqual, new Constant(1)), X.NotEqualTo(1));
    }

    [Fact]
    public void LogicalOperatorsBuildTheTreeThatWasWritten() {
        Assert.Equal(new Conjunction(A, B), A & B);
        Assert.Equal(new Disjunction(A, B), A | B);
        Assert.Equal(new Negation(A), !A);
        Assert.Equal(new Negation(new Equivalence(A, B)), A ^ B);
        Assert.Equal(new Implication(A, X <= 1), A.Implies(X <= 1));
        Assert.Equal(new Equivalence(A, X <= 1), A.Iff(X <= 1));
    }

    [Fact]
    public void PlainTruthValuesMixInOnEitherSide() {
        Assert.Equal(new Conjunction(A, BooleanConstant.True), A & true);
        Assert.Equal(new Conjunction(BooleanConstant.False, A), false & A);
        Assert.Equal(new Disjunction(A, BooleanConstant.False), A | false);
        Assert.Equal(new Disjunction(BooleanConstant.True, X <= 1), true | (X <= 1));
        Assert.Equal(new Negation(new Equivalence(A, BooleanConstant.True)), A ^ true);
        Assert.Equal(new Negation(new Equivalence(BooleanConstant.False, A)), false ^ A);
        Assert.Equal(new Implication(A, BooleanConstant.False), A.Implies(false));
        Assert.Equal(new Equivalence(A, BooleanConstant.True), A.Iff(true));
        Assert.Equal(new Implication(BooleanConstant.True, X <= 1), true.Implies(X <= 1));
        Assert.Equal(new Equivalence(BooleanConstant.False, A), false.Iff(A));
    }

    [Fact]
    public void ABinaryVariableIsBothANumberAndATruthValue() {
        Assert.Equal(new Comparison(new Sum(A, B), Relation.LessThanOrEqual, new Constant(1)), A + B <= 1);
        Assert.Equal(new Disjunction(new Negation(new Conjunction(A, B)), X <= Y), !(A & B) | (X <= Y));
    }

    [Fact]
    public void ExpressionsAreValuesWithStructuralEquality() {
        Assert.Equal(X + 2 * Y <= 10, X + 2 * Y <= 10);
        Assert.NotEqual<ILinearExpression>(X + Y, Y + X);
        Assert.Equal((X + Y).Normalise(), (Y + X).Normalise());
        Assert.Equal(Variable.Continuous("x"), X);
        Assert.NotEqual<IVariable>(Variable.Integer("x"), X);
    }

    [Fact]
    public void SumsOverCollectionsAreBalancedSoTheyStayShallow() {
        var terms = Enumerable.Range(0, 4).Select(index => Variable.Continuous($"t{index}")).ToList();

        Assert.Equal(new Sum(new Sum(terms[0], terms[1]), new Sum(terms[2], terms[3])), terms.Sum());
        Assert.Equal(new Sum(new Product(2, terms[0]), new Product(2, terms[1])), terms.Take(2).Sum(term => 2 * term));
        Assert.Equal(new Constant(0), Array.Empty<ILinearExpression>().Sum());
    }

    [Fact]
    public void JunctionsOverCollectionsAreBalancedWithTheRightIdentities() {
        var flags = Enumerable.Range(0, 4).Select(index => Variable.Binary($"f{index}")).ToList();

        Assert.Equal(new Conjunction(new Conjunction(flags[0], flags[1]), new Conjunction(flags[2], flags[3])), flags.AllOf());
        Assert.Equal(new Disjunction(new Disjunction(flags[0], flags[1]), new Disjunction(flags[2], flags[3])), flags.AnyOf());
        Assert.Equal(new Conjunction(!flags[0], !flags[1]), flags.Take(2).AllOf(flag => !flag));
        Assert.Equal(BooleanConstant.True, Array.Empty<IBooleanExpression>().AllOf());
        Assert.Equal(BooleanConstant.False, Array.Empty<IBooleanExpression>().AnyOf());
    }

    [Fact]
    public void BetweenIsAPairOfInequalitiesWhoseBoundsMayBeNumbersOrExpressions() {
        Assert.Equal(new Conjunction(0 <= X, X <= 10), X.Between(0, 10));
        Assert.Equal(new Conjunction(Y <= X, X <= 10), X.Between(Y, 10));
        Assert.Equal(new Conjunction(0 <= X, X <= Y), X.Between(0, Y));
        Assert.Equal(new Conjunction(Y <= X, X <= Y + 5), X.Between(Y, Y + 5));
    }
}
