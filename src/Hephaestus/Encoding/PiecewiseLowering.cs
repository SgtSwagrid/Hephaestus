using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>What a variable introduced by the lowering stands for. The cases are <see cref="MaximumDefinition"/> and <see cref="ConditionalDefinition"/>.</summary>
public interface IDefinition {
    /// <summary>The variable that was introduced.</summary>
    INumericVariable Variable { get; }
}

/// <summary><c>Variable</c> stands for <c>max(Left, Right)</c>.</summary>
public sealed record MaximumDefinition(
    INumericVariable Variable,
    ILinearExpression Left,
    ILinearExpression Right
) : IDefinition;

/// <summary><c>Variable</c> stands for <c>Then</c> if <c>Condition</c> holds, and for <c>Otherwise</c> if not.</summary>
public sealed record ConditionalDefinition(
    INumericVariable Variable,
    IBooleanExpression<IAffineRelation> Condition,
    ILinearExpression Then,
    ILinearExpression Otherwise
) : IDefinition;

/// <summary>
/// A problem freed of piecewise-linear functions: an objective without any, a constraint over
/// <see cref="IAffineRelation"/>s, and the variables that were introduced to free them, innermost
/// first. A definition records what its variable stands for; the constraints that tie the two
/// together are already part of the constraint. Its type is what tells a later pass that the
/// lowering has been done: a formula as written cannot be normalised, and this one can.
/// </summary>
public sealed record LinearisedProblem(
    ISingleObjective Objective,
    IBooleanExpression<IAffineRelation> Constraint,
    ImmutableArray<IDefinition> Definitions
) {
    /// <summary>The variables that were introduced.</summary>
    public ImmutableSortedSet<IVariable> Auxiliaries => Definitions.Select(definition => definition.Variable).ToImmutableSortedSet(VariableOrder.Comparer);
}

/// <summary>A constraint of a lowered problem, beside the one it was written as, where anybody wrote it.</summary>
public sealed record LoweredConstraint(
    IBooleanExpression<IAffineRelation> Lowered,
    IBooleanExpression<IAtom>? Written
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
        /// <summary>
        /// An equivalent problem without piecewise-linear functions, its relations brought to affine
        /// form. A problem that has none keeps its objective, and its constraint's shape.
        /// </summary>
        /// <exception cref="ModellingException">An expression is not finite, or an atom is of a kind that lowering does not know.</exception>
        public LinearisedProblem Linearise(EncodingOptions? options = null) => Lower(problem, options ?? EncodingOptions.Default);
    }

    extension(LinearisedProblem linearised) {
        /// <summary>
        /// The constraints of the lowered problem, each beside the one it was written as. Lowering
        /// keeps the constraints in the order they were written and adds its own after them: what
        /// ties a maximum to its operands, which nobody wrote and which nothing is put down to.
        /// </summary>
        /// <param name="original">The constraint the problem was lowered from.</param>
        public ImmutableArray<LoweredConstraint> Constraints(IBooleanExpression<IAtom> original) =>
            Paired(linearised.Constraint.Conjuncts, original.Conjuncts);

        /// <summary>Every variable mentioned in the lowered problem, the introduced ones included, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => linearised.Constraint.Variables.Union(linearised.Objective.Expression.Variables);
    }

    private static ImmutableArray<LoweredConstraint> Paired(ImmutableArray<IBooleanExpression<IAffineRelation>> lowered, ImmutableArray<IBooleanExpression<IAtom>> written) =>
        [.. lowered.Select((constraint, index) => new LoweredConstraint(constraint, index < written.Length ? written[index] : null))];

    /// <summary>
    /// Identifies a maximum by the affine forms of its operands, in a fixed order, so that
    /// <c>max(x + y, z)</c>, <c>max(y + x, z)</c> and <c>max(z, x + y)</c> are all one.
    /// </summary>
    private sealed record Shape(
        AffineForm Left,
        AffineForm Right
    );

    /// <summary>Identifies a conditional by its condition, lowered, and the affine forms of its branches.</summary>
    private sealed record Choice(
        IBooleanExpression<IAffineRelation> Condition,
        AffineForm Then,
        AffineForm Otherwise
    );

    private sealed record Lifting(
        ImmutableDictionary<object, INumericVariable> Known,
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
        var start = new Lifting(ImmutableDictionary<object, INumericVariable>.Empty, [], [.. problem.Variables.Select(variable => variable.Name)], options, 0);
        var constraint = Lift(problem.Constraint, start);
        var objective = Lift(problem.Objective.Expression, constraint.State);
        var lifted = new LinearisedProblem(problem.Objective.With(objective.Expression), constraint.Expression, []);
        return objective.State.Definitions.IsEmpty ? lifted : Defined(lifted, objective.State.Definitions, options);
    }

    private static LinearisedProblem Defined(LinearisedProblem lifted, ImmutableList<IDefinition> definitions, EncodingOptions options) {
        var auxiliaries = definitions.Select(definition => definition.Variable).ToImmutableSortedSet(VariableOrder.Comparer);
        var demands = DemandsOf(lifted.Constraint, auxiliaries, options).Aggregate(DemandsOf(lifted.Objective.Expression.Normalise(), lifted.Objective.Sense, auxiliaries), Record);
        // An inner maximum is only leant on by the problem and by maxima introduced after it, so going backwards meets every demand in time.
        var tied = definitions.Reverse().Aggregate(new Tying(demands, []), (tying, definition) => Tie(tying, definition, auxiliaries, options));
        return lifted with { Constraint = lifted.Constraint & tied.Constraints.AllOf(), Definitions = [.. definitions] };
    }

    private sealed record Tying(
        ImmutableDictionary<IVariable, Demand> Demands,
        ImmutableList<IBooleanExpression<IAffineRelation>> Constraints
    );

    private static Tying Tie(Tying tying, IDefinition definition, ImmutableSortedSet<IVariable> auxiliaries, EncodingOptions options) {
        var constraint = Tie(definition, tying.Demands.GetValueOrDefault(definition.Variable));
        return new Tying(DemandsOf(constraint, auxiliaries, options).Aggregate(tying.Demands, Record), tying.Constraints.Add(constraint));
    }

    private static IBooleanExpression<IAffineRelation> Tie(IDefinition definition, Demand demand) =>
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
    private static IBooleanExpression<IAffineRelation> Tie(ConditionalDefinition definition, Demand demand) =>
        demand == Demand.None
            ? BooleanConstant.True
            : definition.Condition.Implies(Held(definition.Variable, demand, definition.Then)) & (!definition.Condition).Implies(Held(definition.Variable, demand, definition.Otherwise));

    private static IBooleanExpression<IAffineRelation> Held(INumericVariable variable, Demand demand, ILinearExpression to) =>
        demand switch {
            Demand.AtLeast => Related(variable, Relation.GreaterThanOrEqual, to),
            Demand.AtMost => Related(variable, Relation.LessThanOrEqual, to),
            _ => Related(variable, Relation.Equal, to),
        };

    private static IBooleanExpression<IAffineRelation> Tie(MaximumDefinition definition, Demand demand) =>
        (demand.HasFlag(Demand.AtLeast) ? Related(definition.Variable, Relation.GreaterThanOrEqual, definition.Left) & Related(definition.Variable, Relation.GreaterThanOrEqual, definition.Right) : BooleanConstant.True)
        & (demand.HasFlag(Demand.AtMost) ? Related(definition.Variable, Relation.LessThanOrEqual, definition.Left) | Related(definition.Variable, Relation.LessThanOrEqual, definition.Right) : BooleanConstant.True);

    /// <summary>A relation between two expressions free of piecewise-linear functions, brought to affine form.</summary>
    private static IBooleanExpression<IAffineRelation> Related(ILinearExpression left, Relation relation, ILinearExpression right) =>
        new AffineRelation((left - right).Normalise(), relation);

    private static ImmutableDictionary<IVariable, Demand> Record(ImmutableDictionary<IVariable, Demand> demands, KeyValuePair<IVariable, Demand> demand) =>
        demands.SetItem(demand.Key, demands.GetValueOrDefault(demand.Key) | demand.Value);

    private static IEnumerable<KeyValuePair<IVariable, Demand>> DemandsOf(IBooleanExpression<IAffineRelation> constraint, ImmutableSortedSet<IVariable> auxiliaries, EncodingOptions options) =>
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
            // The indicator of a binary variable is its column; of anything else, a choice between one and zero.
            Indicator { Condition: BinaryVariable or INegation<ILinearRelation> { Operand: BinaryVariable } or BooleanConstant } => new Lifted<ILinearExpression>(expression, state),
            Indicator indicator => Chosen(Lift(indicator.Condition, state), new Conditional(indicator.Condition, new Constant(1), new Constant(0))),
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
    private static Lifted<ILinearExpression> Chosen(Lifted<IBooleanExpression<IAffineRelation>> condition, Conditional conditional) {
        var branches = Both(condition.State, conditional.Then, conditional.Otherwise);
        var choice = new Choice(condition.Expression, branches.Expression.Left.Normalise(), branches.Expression.Right.Normalise());
        var state = Introduced(branches.State, choice, branches.State.Options.ConditionalPrefix, choice.Then.IsIntegral && choice.Otherwise.IsIntegral, variable => new ConditionalDefinition(variable, condition.Expression, branches.Expression.Left, branches.Expression.Right));
        return new Lifted<ILinearExpression>(state.Known[choice], state);
    }

    /// <summary>The state with a variable for <paramref name="key"/>, introduced now unless an equal expression has been met before.</summary>
    private static Lifting Introduced(Lifting state, object key, string prefix, bool isIntegral, Func<INumericVariable, IDefinition> define) {
        if (state.Known.ContainsKey(key)) {
            return state;
        }
        var fresh = FreshNames.After(state.NextIndex, prefix, state.Reserved.Contains);
        // A choice among whole numbers is a whole number, which matters to solvers that know no others.
        INumericVariable variable = isIntegral ? new IntegerVariable(fresh.Name) : new ContinuousVariable(fresh.Name);
        return state with { Known = state.Known.Add(key, variable), Definitions = state.Definitions.Add(define(variable)), NextIndex = fresh.Index + 1 };
    }

    /// <summary>
    /// The formula with its piecewise-linear functions lifted out and its relations brought to affine
    /// form. Every node is rebuilt, since the lowered formula is over other atoms; its shape is kept,
    /// which is what lets a lowered conjunct be paired with the one it was written as.
    /// </summary>
    private static Lifted<IBooleanExpression<IAffineRelation>> Lift(IBooleanExpression<IAtom> expression, Lifting state) => DeepRecursion.Guard(LiftUnguarded, expression, state);

    private static Lifted<IBooleanExpression<IAffineRelation>> LiftUnguarded(IBooleanExpression<IAtom> expression, Lifting state) =>
        expression switch {
            BooleanConstant constant => new(constant, state),
            BinaryVariable variable => new(variable, state),
            LinearRelation relation => Related(relation.Relation, Both(state, relation.Left, relation.Right)),
            AffineRelation relation => new(relation, state),
            INegation<IAtom> negation => Rebuilt(Lift(negation.Operand, state), operand => new Negation<IAffineRelation>(operand)),
            INamedConstraint<IAtom> named => Rebuilt(Lift(named.Expression, state), operand => new NamedConstraint<IAffineRelation>(named.Name, operand)),
            IConjunction<IAtom> conjunction => Rebuilt(conjunction.Left, conjunction.Right, state, (left, right) => new Conjunction<IAffineRelation>(left, right)),
            IDisjunction<IAtom> disjunction => Rebuilt(disjunction.Left, disjunction.Right, state, (left, right) => new Disjunction<IAffineRelation>(left, right)),
            IImplication<IAtom> implication => Rebuilt(implication.Antecedent, implication.Consequent, state, (left, right) => new Implication<IAffineRelation>(left, right)),
            IEquivalence<IAtom> equivalence => Rebuilt(equivalence.Left, equivalence.Right, state, (left, right) => new Equivalence<IAffineRelation>(left, right)),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };

    private static Lifted<IBooleanExpression<IAffineRelation>> Related(Relation relation, Lifted<(ILinearExpression Left, ILinearExpression Right)> sides) =>
        new(Related(sides.Expression.Left, relation, sides.Expression.Right), sides.State);

    private static Lifted<IBooleanExpression<IAffineRelation>> Rebuilt(Lifted<IBooleanExpression<IAffineRelation>> operand, Func<IBooleanExpression<IAffineRelation>, IBooleanExpression<IAffineRelation>> rebuild) =>
        new(rebuild(operand.Expression), operand.State);

    private static Lifted<IBooleanExpression<IAffineRelation>> Rebuilt(
        IBooleanExpression<IAtom> left,
        IBooleanExpression<IAtom> right,
        Lifting state,
        Func<IBooleanExpression<IAffineRelation>, IBooleanExpression<IAffineRelation>, IBooleanExpression<IAffineRelation>> rebuild
    ) {
        var first = Lift(left, state);
        var second = Lift(right, first.State);
        return new(rebuild(first.Expression, second.Expression), second.State);
    }
}
