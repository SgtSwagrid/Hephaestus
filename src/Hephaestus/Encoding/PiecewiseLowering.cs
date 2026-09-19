using System.Collections.Immutable;

namespace Hephaestus;

/// <summary><c>Variable</c> stands for <c>max(Left, Right)</c>.</summary>
public sealed record MaximumDefinition(
    IVariable Variable,
    ILinearExpression Left,
    ILinearExpression Right
);

/// <summary>
/// A problem freed of piecewise-linear functions, and the variables that were introduced to free
/// it, innermost first. A definition records what its variable stands for; the constraints that
/// tie the two together are already part of the problem.
/// </summary>
public sealed record LinearisedProblem(
    IProblem Problem,
    ImmutableArray<MaximumDefinition> Definitions
) {
    /// <summary>The variables that were introduced.</summary>
    public ImmutableSortedSet<IVariable> Auxiliaries => Definitions.Select(definition => definition.Variable).ToImmutableSortedSet(VariableOrder.Comparer);
}

/// <summary>
/// The pass that lowers <see cref="Maximum"/>, <see cref="Minimum"/> and <see cref="AbsoluteValue"/>
/// to linear form. All three are maxima (<c>min(a, b) = -max(-a, -b)</c> and <c>|e| = max(e, -e)</c>),
/// so each becomes a variable <c>m</c> standing for some <c>max(a, b)</c>; equal maxima share one.
/// What ties <c>m</c> to its operands depends on how the problem uses it. Where a smaller <c>m</c>
/// would make the problem easier, <c>m &gt;= a &amp; m &gt;= b</c> stops it from cheating, and costs no
/// binary variable. Only where a larger <c>m</c> would help is <c>m &lt;= a | m &lt;= b</c> needed too.
/// </summary>
public static class PiecewiseLowering {
    extension(IProblem problem) {
        /// <summary>An equivalent problem without piecewise-linear functions. A problem that has none is returned as it is.</summary>
        /// <exception cref="ModellingException">An expression is not finite.</exception>
        public LinearisedProblem Linearise(EncodingOptions? options = null) => Lower(problem, options ?? EncodingOptions.Default);
    }

    /// <summary>Identifies a maximum by the affine forms of its operands, so that <c>max(x + y, z)</c> and <c>max(y + x, z)</c> are one.</summary>
    private sealed record Shape(
        AffineForm Left,
        AffineForm Right
    );

    private sealed record Lifting(
        ImmutableDictionary<Shape, IVariable> Known,
        ImmutableList<MaximumDefinition> Definitions,
        ImmutableHashSet<string> Reserved,
        string Prefix,
        int NextIndex
    );

    private sealed record Lifted<T>(
        T Expression,
        Lifting State
    );

    private sealed record Step<T>(
        T Expression,
        Lifting State
    );

    /// <summary>How the rest of the problem leans on a variable: whether it would gain from one that is too small, too large, or both.</summary>
    [Flags]
    private enum Demand {
        None = 0,
        AtLeast = 1,
        AtMost = 2,
        Exactly = AtLeast | AtMost,
    }

    private static LinearisedProblem Lower(IProblem problem, EncodingOptions options) {
        var start = new Lifting(ImmutableDictionary<Shape, IVariable>.Empty, [], [.. problem.Variables.Select(variable => variable.Name)], options.PiecewisePrefix, 0);
        var constraint = Lift(new Step<IBooleanExpression>(problem.Constraint, start));
        var objective = Lift(new Step<ILinearExpression>(problem.Objective, constraint.State));
        return objective.State.Definitions.IsEmpty
            ? new LinearisedProblem(problem, [])
            : Defined(problem.With(objective.Expression, constraint.Expression), objective.State.Definitions, options);
    }

    private static LinearisedProblem Defined(IProblem lifted, ImmutableList<MaximumDefinition> definitions, EncodingOptions options) {
        var auxiliaries = definitions.Select(definition => definition.Variable).ToImmutableSortedSet(VariableOrder.Comparer);
        var demands = DemandsOf(lifted.Constraint, auxiliaries, options).Aggregate(DemandsOf(lifted.Objective.Normalise(), lifted.Sense, auxiliaries), Record);
        // An inner maximum is only leant on by the problem and by maxima introduced after it, so going backwards meets every demand in time.
        var tied = definitions.Reverse().Aggregate(new Tying(demands, []), (tying, definition) => Tie(tying, definition, auxiliaries, options));
        return new LinearisedProblem(lifted.With(lifted.Objective, lifted.Constraint & tied.Constraints.AllOf()), [.. definitions]);
    }

    private sealed record Tying(
        ImmutableDictionary<IVariable, Demand> Demands,
        ImmutableList<IBooleanExpression> Constraints
    );

    private static Tying Tie(Tying tying, MaximumDefinition definition, ImmutableSortedSet<IVariable> auxiliaries, EncodingOptions options) {
        var constraint = Tie(definition, tying.Demands.GetValueOrDefault(definition.Variable));
        return new Tying(DemandsOf(constraint, auxiliaries, options).Aggregate(tying.Demands, Record), tying.Constraints.Add(constraint));
    }

    private static IBooleanExpression Tie(MaximumDefinition definition, Demand demand) =>
        (demand.HasFlag(Demand.AtLeast) ? (definition.Variable >= definition.Left) & (definition.Variable >= definition.Right) : BooleanConstant.True)
        & (demand.HasFlag(Demand.AtMost) ? (definition.Variable <= definition.Left) | (definition.Variable <= definition.Right) : BooleanConstant.True);

    private static ImmutableDictionary<IVariable, Demand> Record(ImmutableDictionary<IVariable, Demand> demands, KeyValuePair<IVariable, Demand> demand) =>
        demands.SetItem(demand.Key, demands.GetValueOrDefault(demand.Key) | demand.Value);

    private static IEnumerable<KeyValuePair<IVariable, Demand>> DemandsOf(IBooleanExpression constraint, ImmutableSortedSet<IVariable> auxiliaries, EncodingOptions options) =>
        Atoms(constraint.Normalise(options.StrictnessEpsilon)).SelectMany(atom => DemandsOf(atom.Expression, atom.IsEquality, auxiliaries));

    /// <summary>In <c>&#8230; + c&#183;m &lt;= 0</c> a positive <c>c</c> rewards a small <c>m</c>, a negative one a large <c>m</c>, and an equation both.</summary>
    private static IEnumerable<KeyValuePair<IVariable, Demand>> DemandsOf(AffineForm form, bool isEquality, ImmutableSortedSet<IVariable> auxiliaries) =>
        form.Coefficients
            .Where(term => auxiliaries.Contains(term.Key))
            .Select(term => KeyValuePair.Create(term.Key, isEquality ? Demand.Exactly : term.Value > 0 ? Demand.AtLeast : Demand.AtMost));

    private static ImmutableDictionary<IVariable, Demand> DemandsOf(AffineForm objective, ObjectiveSense sense, ImmutableSortedSet<IVariable> auxiliaries) =>
        DemandsOf(sense == ObjectiveSense.Minimise ? objective : objective.Negated, isEquality: false, auxiliaries).ToImmutableDictionary();

    private static IEnumerable<Atom> Atoms(INormalForm formula) =>
        formula switch {
            Atom atom => [atom],
            All all => all.Operands.SelectMany(Atoms),
            Any any => any.Operands.SelectMany(Atoms),
            _ => [],
        };

    private static Lifted<ILinearExpression> Lift(Step<ILinearExpression> step) => DeepRecursion.Guard(LiftUnguarded, step);

    private static Lifted<ILinearExpression> LiftUnguarded(Step<ILinearExpression> step) =>
        step.Expression switch {
            Product product => Rebuilt(product, Lift(step with { Expression = product.Expression })),
            Sum sum => Rebuilt(sum, Both(step.State, sum.Left, sum.Right)),
            Maximum maximum => Named(Both(step.State, maximum.Left, maximum.Right), isNegated: false),
            Minimum minimum => Named(Both(step.State, -minimum.Left, -minimum.Right), isNegated: true),
            AbsoluteValue absolute => Named(Both(step.State, absolute.Operand, -absolute.Operand), isNegated: false),
            _ => new Lifted<ILinearExpression>(step.Expression, step.State),
        };

    private static Lifted<(ILinearExpression Left, ILinearExpression Right)> Both(Lifting state, ILinearExpression left, ILinearExpression right) {
        var first = Lift(new Step<ILinearExpression>(left, state));
        var second = Lift(new Step<ILinearExpression>(right, first.State));
        return new Lifted<(ILinearExpression, ILinearExpression)>((first.Expression, second.Expression), second.State);
    }

    private static Lifted<ILinearExpression> Rebuilt(Product product, Lifted<ILinearExpression> operand) =>
        new(ReferenceEquals(operand.Expression, product.Expression) ? product : product with { Expression = operand.Expression }, operand.State);

    private static Lifted<ILinearExpression> Rebuilt(Sum sum, Lifted<(ILinearExpression Left, ILinearExpression Right)> operands) =>
        new(ReferenceEquals(operands.Expression.Left, sum.Left) && ReferenceEquals(operands.Expression.Right, sum.Right) ? sum : new Sum(operands.Expression.Left, operands.Expression.Right), operands.State);

    /// <summary>The variable that stands for the maximum of the two operands, negated if it is really a minimum that is wanted.</summary>
    private static Lifted<ILinearExpression> Named(Lifted<(ILinearExpression Left, ILinearExpression Right)> operands, bool isNegated) {
        var shape = new Shape(operands.Expression.Left.Normalise(), operands.Expression.Right.Normalise());
        var state = operands.State.Known.ContainsKey(shape) ? operands.State : Introduce(operands.State, shape, operands.Expression.Left, operands.Expression.Right);
        return new Lifted<ILinearExpression>(isNegated ? -state.Known[shape] : state.Known[shape], state);
    }

    private static Lifting Introduce(Lifting state, Shape shape, ILinearExpression left, ILinearExpression right) {
        var index = Enumerable.Range(state.NextIndex, int.MaxValue - state.NextIndex).First(candidate => !state.Reserved.Contains(state.Prefix + candidate));
        // The maximum of whole numbers is a whole number, which matters to solvers that know no others.
        IVariable variable = shape.Left.IsIntegral && shape.Right.IsIntegral ? new IntegerVariable(state.Prefix + index) : new ContinuousVariable(state.Prefix + index);
        return state with {
            Known = state.Known.Add(shape, variable),
            Definitions = state.Definitions.Add(new MaximumDefinition(variable, left, right)),
            NextIndex = index + 1,
        };
    }

    private static Lifted<IBooleanExpression> Lift(Step<IBooleanExpression> step) => DeepRecursion.Guard(LiftUnguarded, step);

    private static Lifted<IBooleanExpression> LiftUnguarded(Step<IBooleanExpression> step) =>
        step.Expression switch {
            Comparison comparison => Rebuilt(comparison, Both(step.State, comparison.Left, comparison.Right)),
            Negation negation => Rebuilt(negation, Lift(step with { Expression = negation.Operand })),
            Conjunction conjunction => Rebuilt(conjunction, conjunction.Left, conjunction.Right, step.State, (left, right) => new Conjunction(left, right)),
            Disjunction disjunction => Rebuilt(disjunction, disjunction.Left, disjunction.Right, step.State, (left, right) => new Disjunction(left, right)),
            Implication implication => Rebuilt(implication, implication.Antecedent, implication.Consequent, step.State, (left, right) => new Implication(left, right)),
            Equivalence equivalence => Rebuilt(equivalence, equivalence.Left, equivalence.Right, step.State, (left, right) => new Equivalence(left, right)),
            _ => new Lifted<IBooleanExpression>(step.Expression, step.State),
        };

    private static Lifted<IBooleanExpression> Rebuilt(Comparison comparison, Lifted<(ILinearExpression Left, ILinearExpression Right)> sides) =>
        new(ReferenceEquals(sides.Expression.Left, comparison.Left) && ReferenceEquals(sides.Expression.Right, comparison.Right) ? comparison : comparison with { Left = sides.Expression.Left, Right = sides.Expression.Right }, sides.State);

    private static Lifted<IBooleanExpression> Rebuilt(Negation negation, Lifted<IBooleanExpression> operand) =>
        new(ReferenceEquals(operand.Expression, negation.Operand) ? negation : new Negation(operand.Expression), operand.State);

    /// <summary>Lifts both operands of a connective, keeping the original node when neither changed.</summary>
    private static Lifted<IBooleanExpression> Rebuilt(IBooleanExpression original, IBooleanExpression left, IBooleanExpression right, Lifting state, Func<IBooleanExpression, IBooleanExpression, IBooleanExpression> rebuild) {
        var first = Lift(new Step<IBooleanExpression>(left, state));
        var second = Lift(new Step<IBooleanExpression>(right, first.State));
        return new Lifted<IBooleanExpression>(ReferenceEquals(first.Expression, left) && ReferenceEquals(second.Expression, right) ? original : rebuild(first.Expression, second.Expression), second.State);
    }
}
