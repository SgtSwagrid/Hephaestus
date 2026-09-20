using static Hephaestus.Piecewise;

namespace Hephaestus.Tests;

/// <summary>Plain numbers as typed quantities: they read back in their own type, and kinds of number do not mix.</summary>
public sealed class NumberQuantityTests {
    private static readonly Quantity<int> Jobs = Variable.Integer<int>("jobs");
    private static readonly Quantity<int> Machines = Variable.Integer<int>("machines");
    private static readonly Quantity<long> Units = Variable.Integer<long>("units");
    private static readonly Quantity<decimal> Cost = Variable.Continuous<decimal>("cost");
    private static readonly IntegerVariable JobsUnderneath = Variable.Integer("jobs");
    private static readonly IntegerVariable MachinesUnderneath = Variable.Integer("machines");

    private static void AssertSameConstraint(IBooleanExpression expected, IBooleanExpression actual) =>
        Assert.Equal(expected.Format(), actual.Format());

    [Fact]
    public void TheyAreOrdinaryVariablesUnderneath() {
        Assert.Equal(JobsUnderneath, Jobs.Expression);
        Assert.Equal(Variable.Continuous("cost"), Cost.Expression);
        Assert.Equal(new WholeNumberProjection<int>(), Jobs.Projection);
        Assert.IsType<IntegerVariable>(Problem.Minimise(Jobs).SubjectTo(Jobs >= 2).Encode().Columns.Single().Variable);
    }

    [Fact]
    public void TheyCombineWithEachOtherAndWithPlainNumbersOfTheirOwnType() {
        AssertSameConstraint(JobsUnderneath + 2 <= MachinesUnderneath, Jobs + 2 <= Machines);
        AssertSameConstraint(3 <= JobsUnderneath, 3 <= Jobs);
        AssertSameConstraint(2 * JobsUnderneath - MachinesUnderneath >= 1, 2 * Jobs - Machines >= 1);
        AssertSameConstraint((0 <= JobsUnderneath) & (JobsUnderneath <= 12), Jobs.Between(0, 12));
        AssertSameConstraint(JobsUnderneath.EqualTo(4), Jobs.EqualTo(4));
        AssertSameConstraint(Variable.Continuous("cost") <= 99.5, Cost <= 99.5m);
        AssertSameConstraint(Variable.Integer("units") >= 5000000000, Units >= 5_000_000_000L);
    }

    [Fact]
    public void TheyAreReadBackInTheirOwnTypeAndWholeNumbersAreMadeWhole() {
        var solution = new Solution(Solution.Empty.Values.Add(JobsUnderneath, 2.9999999).Add(MachinesUnderneath, 4).Add(Variable.Integer("units"), 5e9).Add(Variable.Continuous("cost"), 12.35), 0);

        Assert.Equal(3, solution.Value(Jobs));
        Assert.Equal(7, solution.Value(Jobs + Machines));
        Assert.Equal(4, solution.Value(Max(Jobs, Machines)));
        Assert.Equal(1, solution.Value(Abs(Jobs - Machines)));
        Assert.Equal(5_000_000_000L, solution.Value(Units));
        Assert.Equal(12.35m, solution.Value(Cost));
        Assert.Equal(24.7m, solution.Value(2 * Cost));
    }

    [Fact]
    public void TheyTakeTheirPlaceInStartsObjectivesAndSums() {
        Assert.Equal(6, Solution.Empty.With(Jobs, 6).Values[JobsUnderneath]);
        Assert.Equal(new Prioritised(Objective.Minimise(JobsUnderneath), 2), Objective.Minimise(Jobs, tolerance: 2));
        Assert.Equal((JobsUnderneath + MachinesUnderneath).Normalise(), new[] { Jobs, Machines }.Sum().Expression.Normalise());
        Assert.Equal((JobsUnderneath / 4).Normalise(), Jobs.In(4).Normalise());
    }
}
