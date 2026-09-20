using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>What a variable introduced by the lowering stands for. The cases are <see cref="MaximumDefinition"/> and <see cref="ConditionalDefinition"/>.</summary>
public interface IDefinition {
    /// <summary>The variable that was introduced.</summary>
    IVariable Variable { get; }
}

/// <summary><c>Variable</c> stands for <c>max(Left, Right)</c>.</summary>
public sealed record MaximumDefinition(
    IVariable Variable,
    ILinearExpression Left,
    ILinearExpression Right
) : IDefinition;

/// <summary><c>Variable</c> stands for <c>Then</c> if <c>Condition</c> holds, and for <c>Otherwise</c> if not.</summary>
public sealed record ConditionalDefinition(
    IVariable Variable,
    IBooleanExpression Condition,
    ILinearExpression Then,
    ILinearExpression Otherwise
) : IDefinition;

/// <summary>
/// A problem freed of piecewise-linear functions, and the variables that were introduced to free
/// it, innermost first. A definition records what its variable stands for; the constraints that
/// tie the two together are already part of the problem.
/// </summary>
public sealed record LinearisedProblem(
    IOneShotProblem Problem,
    ImmutableArray<IDefinition> Definitions
) {
    /// <summary>The variables that were introduced.</summary>
    public ImmutableSortedSet<IVariable> Auxiliaries => Definitions.Select(definition => definition.Variable).ToImmutableSortedSet(VariableOrder.Comparer);
}

/// <summary>A constraint of a lowered problem, beside the one it was written as, where anybody wrote it.</summary>
public sealed record LoweredConstraint(
    IBooleanExpression Lowered,
    IBooleanExpression? Written
);

/// <summary>
/// The pass that lowers <see cref="Maximum"/>, <see cref="Minimum"/> and <see cref="AbsoluteValue"/>
/// to linear form. All three are maxima (<c>min(a, b) = -max(-a, -b)</c> and <c>|e| = max(e, -e)</c>),
/// so each becomes a variable <c>m</c> standing for some <c>max(a, b)</c>; equal maxima share one.
/// What ties <c>m</c> to its operands depends on how the problem uses it. Where a smaller <c>m</c>
/// would make the problem easier, <c>m &gt;= a &amp; m &gt;= b</c> stops it from cheating, and costs no
/// binary variable. Only where a larger <c>m</c> would help is <c>m &lt;= a | m &lt;= b</c> needed too.
/// </summary>
public static class PiecewiseLowering {
    extension(IOneShotProblem problem) {
        /// <summary>An equivalent problem without piecewise-linear functions. A problem that has none is returned as it is.</summary>
        /// <exception cref="ModellingException">An expression is not finite.</exception>
        public LinearisedProblem Linearise(EncodingOptions? options = null) => Lower(problem, options ?? EncodingOptions.Default);
    }

    extension(LinearisedProblem linearised) {
        /// <summary>
        /// The constraints of the lowered problem, each beside the one it was written as. Lowering
        /// keeps the constraints in the order they were written and adds its own after them: what
        /// ties a maximum to its operands, which nobody wrote and which nothing is put down to.
        /// </summary>
        /// <param name="original">The constraint the problem was lowered from.</param>
        public ImmutableArray<LoweredConstraint> Constraints(IBooleanExpression original) =>
            Paired(linearised.Problem.Constraint.Conjuncts, original.Conjuncts);
    }

    private static ImmutableArray<LoweredConstraint> Paired(ImmutableArray<IBooleanExpression> lowered, ImmutableArray<IBooleanExpression> written) =>
        [.. lowered.Select((constraint, index) => new LoweredConstraint(constraint, index < written.Length ? written[index] : null))];

    /// <summary>
    /// Identifies a maximum by the affine forms of its operands, in a fixed order, so that
    /// <c>max(x + y, z)</c>, <c>max(y + x, z)</c> and <c>max(z, x + y)</c> are all one.
    /// </summary>
    private sealed record Shape(
        AffineForm Left,
        AffineForm Right
    );

    /// <summary>Identifies a conditional by its condition as written and the affine forms of its branches.</summary>
    private sealed record Choice(
        IBooleanExpression Condition,
        AffineForm Then,
        AffineForm Otherwise
    );

    private sealed record Lifting(
        ImmutableDictionary<object, IVariable> Known,
        ImmutableList<IDefinition> Definitions,
        ImmutableHashSet<string> Reserved,
        EncodingOptions Options,
        int NextIndex
    );

    private sealed record Lifted<T>(
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

    private static LinearisedProblem Lower(IOneShotProblem problem, EncodingOptions options) {
        var start = new Lifting(ImmutableDictionary<object, IVariable>.Empty, [], [.. problem.Variables.Select(variable => variable.Name)], options, 0);
        var constraint = Lift(problem.Constraint, start);
        var objective = Lift(problem.Objective.Expression, constraint.State);
        return objective.State.Definitions.IsEmpty
            ? new LinearisedProblem(problem, [])
            : Defined(problem.With(objective.Expression, constraint.Expression), objective.State.Definitions, options);
    }

    private static LinearisedProblem Defined(IOneShotProblem lifted, ImmutableList<IDefinition> definitions, EncodingOptions options) {
        var auxiliaries = definitions.Select(definition => definition.Variable).ToImmutableSortedSet(VariableOrder.Comparer);
        var demands = DemandsOf(lifted.Constraint, auxiliaries, options).Aggregate(DemandsOf(lifted.Objective.Expression.Normalise(), lifted.Sense, auxiliaries), Record);
        // An inner maximum is only leant on by the problem and by maxima introduced after it, so going backwards meets every demand in time.
        var tied = definitions.Reverse().Aggregate(new Tying(demands, []), (tying, definition) => Tie(tying, definition, auxiliaries, options));
        return new LinearisedProblem(lifted.With(lifted.Objective.Expression, lifted.Constraint & tied.Constraints.AllOf()), [.. definitions]);
    }

    private sealed record Tying(
        ImmutableDictionary<IVariable, Demand> Demands,
        ImmutableList<IBooleanExpression> Constraints
    );

    private static Tying Tie(Tying tying, IDefinition definition, ImmutableSortedSet<IVariable> auxiliaries, EncodingOptions options) {
        var constraint = Tie(definition, tying.Demands.GetValueOrDefault(definition.Variable));
        return new Tying(DemandsOf(constraint, auxiliaries, options).Aggregate(tying.Demands, Record), tying.Constraints.Add(constraint));
    }

    private static IBooleanExpression Tie(IDefinition definition, Demand demand) =>
        definition switch {
            MaximumDefinition maximum => Tie(maximum, demand),
            ConditionalDefinition conditional => Tie(conditional, demand),
            _ => throw new NotSupportedException($"Unknown kind of definition: {definition.GetType().Name}."),
        };

    /// <summary>
    /// Whichever branch the condition selects, the variable is held to it: exactly, or only from the
    /// side on which the problem could otherwise cheat. With a binary variable for a condition, that is
    /// two conditional rows and nothing more.
    /// </summary>
    private static IBooleanExpression Tie(ConditionalDefinition definition, Demand demand) =>
        demand == Demand.None
            ? BooleanConstant.True
            : definition.Condition.Implies(Held(definition.Variable, demand, definition.Then)) & (!definition.Condition).Implies(Held(definition.Variable, demand, definition.Otherwise));

    private static IBooleanExpression Held(IVariable variable, Demand demand, ILinearExpression to) =>
        demand switch {
            Demand.AtLeast => variable >= to,
            Demand.AtMost => variable <= to,
            _ => variable.EqualTo(to),
        };

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

    private static Lifted<ILinearExpression> Lift(ILinearExpression expression, Lifting state) => DeepRecursion.Guard(LiftUnguarded, expression, state);

    private static Lifted<ILinearExpression> LiftUnguarded(ILinearExpression expression, Lifting state) =>
        expression switch {
            Product product => Rebuilt(product, Lift(product.Expression, state)),
            NamedTerm named => Rebuilt(named, Lift(named.Expression, state)),
            Sum sum => Rebuilt(sum, Both(state, sum.Left, sum.Right)),
            Maximum maximum => Named(Both(state, maximum.Left, maximum.Right), isNegated: false),
            Minimum minimum => Named(Both(state, -minimum.Left, -minimum.Right), isNegated: true),
            AbsoluteValue absolute => Named(Both(state, absolute.Operand, -absolute.Operand), isNegated: false),
            Conditional conditional => Chosen(Lift(conditional.Condition, state), conditional),
            _ => new Lifted<ILinearExpression>(expression, state),
        };

    private static Lifted<(ILinearExpression Left, ILinearExpression Right)> Both(Lifting state, ILinearExpression left, ILinearExpression right) {
        var first = Lift(left, state);
        var second = Lift(right, first.State);
        return new Lifted<(ILinearExpression, ILinearExpression)>((first.Expression, second.Expression), second.State);
    }
    private static Lifted<ILinearExpression> Rebuilt(Product product, Lifted<ILinearExpression> operand) =>
        new(ReferenceEquals(operand.Expression, product.Expression) ? product : product with { Expression = operand.Expression }, operand.State);

    private static Lifted<ILinearExpression> Rebuilt(NamedTerm named, Lifted<ILinearExpression> operand) =>
        new(ReferenceEquals(operand.Expression, named.Expression) ? named : named with { Expression = operand.Expression }, operand.State);

    private static Lifted<ILinearExpression> Rebuilt(Sum sum, Lifted<(ILinearExpression Left, ILinearExpression Right)> operands) =>
        new(ReferenceEquals(operands.Expression.Left, sum.Left) && ReferenceEquals(operands.Expression.Right, sum.Right) ? sum : new Sum(operands.Expression.Left, operands.Expression.Right), operands.State);

    /// <summary>The variable that stands for the maximum of the two operands, negated if it is really a minimum that is wanted.</summary>
    private static Lifted<ILinearExpression> Named(Lifted<(ILinearExpression Left, ILinearExpression Right)> operands, bool isNegated) {
        var shape = ShapeOf(operands.Expression.Left.Normalise(), operands.Expression.Right.Normalise());
        var state = Introduced(operands.State, shape, operands.State.Options.PiecewisePrefix, shape.Left.IsIntegral && shape.Right.IsIntegral, variable => new MaximumDefinition(variable, operands.Expression.Left, operands.Expression.Right));
        return new Lifted<ILinearExpression>(isNegated ? -state.Known[shape] : state.Known[shape], state);
    }

    /// <summary>A maximum is the same maximum either way round, so its operands are put in a fixed order before it is looked up.</summary>
    private static Shape ShapeOf(AffineForm left, AffineForm right) =>
        Compared(left, right) <= 0 ? new Shape(left, right) : new Shape(right, left);

    /// <summary>Any total order on forms will do, so long as it is the same one every time: by length, then term by term, then by constant.</summary>
    private static int Compared(AffineForm left, AffineForm right) =>
        left.Coefficients.Count - right.Coefficients.Count is var byLength and not 0 ? byLength
        : left.Coefficients.Zip(right.Coefficients, Compared).FirstOrDefault(order => order != 0) is var byTerm and not 0 ? byTerm
        : left.Constant.CompareTo(right.Constant);

    private static int Compared(KeyValuePair<IVariable, double> left, KeyValuePair<IVariable, double> right) =>
        VariableOrder.Comparer.Compare(left.Key, right.Key) is var byVariable and not 0 ? byVariable : left.Value.CompareTo(right.Value);

    /// <summary>The variable that stands for whichever branch the condition selects.</summary>
    private static Lifted<ILinearExpression> Chosen(Lifted<IBooleanExpression> condition, Conditional conditional) {
        var branches = Both(condition.State, conditional.Then, conditional.Otherwise);
        var choice = new Choice(condition.Expression, branches.Expression.Left.Normalise(), branches.Expression.Right.Normalise());
        var state = Introduced(branches.State, choice, branches.State.Options.ConditionalPrefix, choice.Then.IsIntegral && choice.Otherwise.IsIntegral, variable => new ConditionalDefinition(variable, condition.Expression, branches.Expression.Left, branches.Expression.Right));
        return new Lifted<ILinearExpression>(state.Known[choice], state);
    }

    /// <summary>The state with a variable for <paramref name="key"/>, introduced now unless an equal expression has been met before.</summary>
    private static Lifting Introduced(Lifting state, object key, string prefix, bool isIntegral, Func<IVariable, IDefinition> define) {
        if (state.Known.ContainsKey(key)) {
            return state;
        }
        var fresh = FreshNames.After(state.NextIndex, prefix, state.Reserved.Contains);
        // A choice among whole numbers is a whole number, which matters to solvers that know no others.
        IVariable variable = isIntegral ? new IntegerVariable(fresh.Name) : new ContinuousVariable(fresh.Name);
        return state with { Known = state.Known.Add(key, variable), Definitions = state.Definitions.Add(define(variable)), NextIndex = fresh.Index + 1 };
    }

    private static Lifted<IBooleanExpression> Lift(IBooleanExpression expression, Lifting state) => DeepRecursion.Guard(LiftUnguarded, expression, state);

    private static Lifted<IBooleanExpression> LiftUnguarded(IBooleanExpression expression, Lifting state) =>
        expression switch {
            Comparison comparison => Rebuilt(comparison, Both(state, comparison.Left, comparison.Right)),
            Negation negation => Rebuilt(negation, Lift(negation.Operand, state)),
            NamedConstraint named => Rebuilt(named, Lift(named.Expression, state)),
            Conjunction conjunction => Rebuilt(conjunction, conjunction.Left, conjunction.Right, state, (left, right) => new Conjunction(left, right)),
            Disjunction disjunction => Rebuilt(disjunction, disjunction.Left, disjunction.Right, state, (left, right) => new Disjunction(left, right)),
            Implication implication => Rebuilt(implication, implication.Antecedent, implication.Consequent, state, (left, right) => new Implication(left, right)),
            Equivalence equivalence => Rebuilt(equivalence, equivalence.Left, equivalence.Right, state, (left, right) => new Equivalence(left, right)),
            _ => new Lifted<IBooleanExpression>(expression, state),
        };
    private static Lifted<IBooleanExpression> Rebuilt(Comparison comparison, Lifted<(ILinearExpression Left, ILinearExpression Right)> sides) =>
        new(ReferenceEquals(sides.Expression.Left, comparison.Left) && ReferenceEquals(sides.Expression.Right, comparison.Right) ? comparison : comparison with { Left = sides.Expression.Left, Right = sides.Expression.Right }, sides.State);

    private static Lifted<IBooleanExpression> Rebuilt(NamedConstraint named, Lifted<IBooleanExpression> operand) =>
        new(ReferenceEquals(operand.Expression, named.Expression) ? named : named with { Expression = operand.Expression }, operand.State);

    private static Lifted<IBooleanExpression> Rebuilt(Negation negation, Lifted<IBooleanExpression> operand) =>
        new(ReferenceEquals(operand.Expression, negation.Operand) ? negation : new Negation(operand.Expression), operand.State);

    /// <summary>Lifts both operands of a connective, keeping the original node when neither changed.</summary>
    private static Lifted<IBooleanExpression> Rebuilt(IBooleanExpression original, IBooleanExpression left, IBooleanExpression right, Lifting state, Func<IBooleanExpression, IBooleanExpression, IBooleanExpression> rebuild) {
        var first = Lift(left, state);
        var second = Lift(right, first.State);
        return new Lifted<IBooleanExpression>(ReferenceEquals(first.Expression, left) && ReferenceEquals(second.Expression, right) ? original : rebuild(first.Expression, second.Expression), second.State);
    }
}
