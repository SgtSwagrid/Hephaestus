using static Hephaestus.Piecewise;

namespace Hephaestus.Contracts;

/// <summary>
/// What shadow prices must mean, whichever backend supplies the dual values. Each price is checked
/// against the definition: raise the right-hand side of the constraint a little, solve again, and
/// see how far the optimum moved. That also pins down every backend's sign convention.
/// </summary>
public abstract class SensitivityContract {
    /// <summary>The solver for the problem itself.</summary>
    protected abstract ISolver Solver { get; }

    /// <summary>The backend for the linear programme at the solution.</summary>
    protected abstract IMilpBackend Backend { get; }

    private static readonly ContinuousVariable Doors = Variable.Continuous("doors");
    private static readonly ContinuousVariable Windows = Variable.Continuous("windows");
    private static readonly ContinuousVariable StartA = Variable.Continuous("startA");
    private static readonly ContinuousVariable StartB = Variable.Continuous("startB");
    private static readonly BinaryVariable Express = Variable.Binary("express");

    private const double Step = 0.01;

    private double Optimum(IOneShotProblem problem) => Assert.IsType<Optimal>(Solver.Solve(problem)).Solution.ObjectiveValue;

    private ShadowPrices Prices(IOneShotProblem problem) => problem.ShadowPrices(Assert.IsType<Optimal>(Solver.Solve(problem)).Solution, Backend);

    /// <summary>The rate at which the optimum moves as <paramref name="loosened"/> takes the place of <paramref name="constraint"/>.</summary>
    private double Rate(IOneShotProblem problem, IBooleanExpression constraint, IBooleanExpression loosened) =>
        (Optimum(Problem.Optimise(problem.Objective).SubjectTo(problem.Constraint.Conjuncts.Replace(constraint, loosened))) - Optimum(problem)) / Step;

    [Fact]
    public void ThePricesOfATextbookLinearProgrammeAreItsDualValues() {
        var (plant1, plant2, plant3) = ((Doors <= 4).WithName("plant 1"), (2 * Windows <= 12).WithName("plant 2"), (3 * Doors + 2 * Windows <= 18).WithName("plant 3"));
        var problem = Problem.Maximise(3 * Doors + 5 * Windows).SubjectTo((Doors >= 0) & (Windows >= 0) & plant1 & plant2 & plant3);

        var prices = Prices(problem);

        Assert.Equal(0, prices.Of(plant1), precision: 6);
        Assert.Equal(1.5, prices.Of(plant2), precision: 6);
        Assert.Equal(1, prices.Of(plant3), precision: 6);
        Assert.Equal(0, prices.Of(Doors >= 0), precision: 6);
        Assert.Equal(prices.Of(plant2), Rate(problem, plant2, 2 * Windows <= 12 + Step), precision: 4);
        Assert.Equal(prices.Of(plant3), Rate(problem, plant3, 3 * Doors + 2 * Windows <= 18 + Step), precision: 4);
    }

    [Fact]
    public void APriceIsTheRateOfChangeWhicheverWayTheConstraintFacesAndWhicheverWayTheObjectiveGoes() {
        var (demand, blend, cap) = ((Doors + Windows >= 10).WithName("demand"), (Doors - 2 * Windows).EqualTo(1).WithName("blend"), (Windows <= 8).WithName("cap"));
        var minimise = Problem.Minimise(4 * Doors + 3 * Windows).SubjectTo(demand & blend & cap);
        var maximise = Problem.Maximise(-4 * Doors - 3 * Windows).SubjectTo(demand & blend & cap);

        Assert.Equal(Rate(minimise, demand, Doors + Windows >= 10 + Step), Prices(minimise).Of(demand), precision: 4);
        Assert.Equal(Rate(minimise, blend, (Doors - 2 * Windows).EqualTo(1 + Step)), Prices(minimise).Of(blend), precision: 4);
        Assert.Equal(Rate(maximise, demand, Doors + Windows >= 10 + Step), Prices(maximise).Of(demand), precision: 4);
        Assert.Equal(Rate(maximise, blend, (Doors - 2 * Windows).EqualTo(1 + Step)), Prices(maximise).Of(blend), precision: 4);
        Assert.True(Prices(minimise).Of(demand) > 0);
        Assert.Equal(0, Prices(minimise).Of(cap), precision: 6);
    }

    [Fact]
    public void WithLogicAndWholeNumbersThePricesAreThoseOfTheChoicesMade() {
        var release = (StartA >= 100).WithName("release");
        var changeover = ((StartA + 120 + 60 * Express <= StartB) | (StartB + 120 <= StartA)).WithName("changeover");
        var horizon = StartA.Between(0, 3600) & StartB.Between(0, 3600);
        var problem = Problem.Minimise(StartB + 2 * StartA - 50 * Express).SubjectTo(horizon & release & (StartB >= 200) & changeover & Express.Implies(StartB >= 300));

        var prices = Prices(problem);

        // B may not go before 200, so A goes first at 100 and B follows at 220; being an express would cost more than it earns.
        Assert.Equal(3, prices.Of(release), precision: 6);
        Assert.Equal(Rate(problem, release, StartA >= 100 + Step), prices.Of(release), precision: 4);
        // Raising the right-hand side of the side in force, startB, lets B go that much sooner.
        Assert.Equal(-1, prices.Of(changeover), precision: 6);
        Assert.Equal(0, prices.Of(horizon), precision: 6);
        Assert.Equal(0, prices.Of(StartB >= 200), precision: 6);
    }

    [Fact]
    public void PiecewiseFunctionsArePricedThroughTheSideThatIsInForce() {
        var latest = (Max(StartA, StartB) <= 500).WithName("latest");
        var (releaseA, releaseB) = ((StartA >= 100).WithName("release A"), (StartB >= 250).WithName("release B"));
        var problem = Problem.Minimise(Max(StartA, StartB) + 0.1 * StartA).SubjectTo(releaseA & releaseB & latest);

        var prices = Prices(problem);

        Assert.Equal(0.1, prices.Of(releaseA), precision: 6);
        Assert.Equal(1, prices.Of(releaseB), precision: 6);
        Assert.Equal(0, prices.Of(latest), precision: 6);
        Assert.Equal(1.1, prices.Of(releaseA & releaseB), precision: 6);
    }
}
