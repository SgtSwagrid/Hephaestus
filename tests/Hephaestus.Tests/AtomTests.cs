using System.Collections.Immutable;

namespace Hephaestus.Tests;

/// <summary>Formulas typed by the atoms they are over: pure logic, linear relations as written, and affine ones once lowered.</summary>
public sealed class AtomTests {
    private static readonly ContinuousVariable X = Variable.Continuous("x");
    private static readonly ContinuousVariable Y = Variable.Continuous("y");
    private static readonly BinaryVariable A = Variable.Binary("a");
    private static readonly BinaryVariable B = Variable.Binary("b");

    [Fact]
    public void PureLogicIsOverNoAtomsAndMixesWithAnyTheory() {
        IBooleanExpression<IPropositional> logic = !(A & B) | A.Implies(B);
        IBooleanExpression<ILinearRelation> mixed = logic & (X <= Y);
        IBooleanExpression<IAtom> anything = mixed;

        Assert.Equal("(!(a & b) | (a => b)) & (x <= y)", anything.Format());
    }

    [Fact]
    public void AFormulaIsTheSameFormulaWhicheverAtomsItIsSeenOver() {
        IBooleanExpression<IAtom> wide = new Conjunction<IAtom>(A, X <= Y);
        IBooleanExpression<ILinearRelation> narrow = A & (X <= Y);

        Assert.Equal(wide, narrow);
        Assert.Equal(wide.GetHashCode(), narrow.GetHashCode());
        Assert.NotEqual(wide, A | (X <= Y));
        Assert.Equal(Problem.Minimise(X).SubjectTo(A & (X <= Y)), Problem.Minimise(X).SubjectTo(A, X <= Y));
    }

    [Fact]
    public void LoweringGivesAffineRelationsOnly() {
        var linearised = Problem.Minimise(Piecewise.Max(X, Y)).SubjectTo(A.Implies(X - Y <= 3) & (Piecewise.Abs(X) <= 5)).Linearise();

        IBooleanExpression<IAffineRelation> lowered = linearised.Constraint;

        Assert.Equal("a => (x - y <= 3)", lowered.Conjuncts[0].Format());
        Assert.Equal(new AffineRelation(AffineForm.Zero.PlusTerm(X, 1).PlusTerm(Y, -1).Plus(-3), Relation.LessThanOrEqual), ((IImplication<IAffineRelation>)lowered.Conjuncts[0]).Consequent);
        Assert.NotEmpty(linearised.Definitions);
    }

    [Fact]
    public void AnAffineRelationIsALinearRelationOfANarrowerKind() {
        IBooleanExpression<IAffineRelation> lowered = Problem.Satisfy(A.Implies(X <= 3)).Linearise().Constraint;
        IBooleanExpression<ILinearRelation> seenAsWritten = lowered;
        var solution = Solution.Empty.With(A, true).With(X, 2);

        Assert.True(solution.Value(seenAsWritten));
        Assert.Equal(1, solution.Value(Piecewise.If(seenAsWritten, 1, 0)));
        Assert.IsNotAssignableFrom<IBooleanExpression<IAffineRelation>>(A.Implies(X <= 3));
    }

    [Fact]
    public void AConflictComesBackTypedLikeTheConstraintItWasFoundIn() {
        IBooleanExpression<ILinearRelation> constraint = (X >= 5).WithName("high") & (X <= 3).WithName("low") & Y.Between(0, 1);

        ImmutableArray<IBooleanExpression<ILinearRelation>> conflict = new Enumerating().FindConflict(constraint);

        Assert.Equal(["high", "low"], conflict.Select(conjunct => conjunct.Name));
    }

    /// <summary>Decides feasibility by evaluating a few candidate points: enough for the conflict above, and no solver needed.</summary>
    private sealed record Enumerating : ISolver {
        public ISolveResult Solve(IOneShotProblem problem, Solution? startingFrom = null, CancellationToken cancellationToken = default) =>
            new[] { 0.0, 4.0, 6.0 }
                .Select(value => Solution.Empty.With(X, value).With(Y, 0))
                .FirstOrDefault(solution => solution.Value(problem.Constraint)) is { } found
                ? new Optimal(found)
                : new Infeasible();
    }
}
