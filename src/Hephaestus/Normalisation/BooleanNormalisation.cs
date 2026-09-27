using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// The pass that turns a formula as written into negation normal form, exactly: nothing is
/// approximated, so a strict inequality stays strict and a disequation stays one. Its relations
/// must be free of piecewise-linear functions, which is what lowering sees to first.
/// </summary>
internal static class BooleanNormalisation {
    public static INormalForm True { get; } = new All([]);

    public static INormalForm False { get; } = new Any([]);

    extension(IBooleanExpression<ILinearArithmetic> expression) {
        /// <summary>The formula in negation normal form.</summary>
        /// <exception cref="ModellingException">A relation holds a piecewise-linear function, or a number that is not finite.</exception>
        public INormalForm Normalise() => Convert(expression, true);
    }

    /// <summary>What a run of same-kind junctions is being gathered under; none of it changes as the run is walked.</summary>
    private sealed record Gathering(
        bool IsConjunctive,
        bool Polarity
    );

    private static INormalForm Convert(IBooleanExpression<ILinearArithmetic> expression, bool polarity) =>
        DeepRecursion.Guard(ConvertUnguarded, expression, polarity);

    private static INormalForm ConvertUnguarded(IBooleanExpression<ILinearArithmetic> expression, bool polarity) =>
        expression switch {
            BooleanConstant constant => constant.Value == polarity ? True : False,
            BinaryVariable variable => new Literal(variable, polarity),
            LinearRelation relation => OfRelation((relation.Left - relation.Right).Normalise(), polarity ? relation.Relation : Opposite(relation.Relation)),
            INegation<ILinearArithmetic> negation => Convert(negation.Operand, !polarity),
            INamedConstraint<ILinearArithmetic> named => Convert(named.Expression, polarity),
            IConjunction<ILinearArithmetic> => OfJunction(expression, new Gathering(IsConjunctive: polarity, polarity)),
            IDisjunction<ILinearArithmetic> => OfJunction(expression, new Gathering(IsConjunctive: !polarity, polarity)),
            IImplication<ILinearArithmetic> implication => Convert(!implication.Antecedent | implication.Consequent, polarity),
            IEquivalence<ILinearArithmetic> equivalence => Convert(equivalence.Left.Implies(equivalence.Right) & equivalence.Right.Implies(equivalence.Left), polarity),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };

    private static INormalForm OfJunction(IBooleanExpression<ILinearArithmetic> expression, Gathering gathering) =>
        Junction(Collect(expression, gathering, []), gathering.IsConjunctive);

    /// <summary>Gathers the operands of a maximal run of same-kind junctions, left to right.</summary>
    private static ImmutableList<INormalForm> Collect(IBooleanExpression<ILinearArithmetic> expression, Gathering gathering, ImmutableList<INormalForm> into) =>
        DeepRecursion.Guard(CollectUnguarded, expression, gathering, into);

    private static ImmutableList<INormalForm> CollectUnguarded(IBooleanExpression<ILinearArithmetic> expression, Gathering gathering, ImmutableList<INormalForm> into) =>
        expression switch {
            IConjunction<ILinearArithmetic> conjunction when gathering.Polarity == gathering.IsConjunctive => CollectBoth(conjunction.Left, conjunction.Right, gathering, into),
            IDisjunction<ILinearArithmetic> disjunction when gathering.Polarity != gathering.IsConjunctive => CollectBoth(disjunction.Left, disjunction.Right, gathering, into),
            _ => Splice(into, Convert(expression, gathering.Polarity), gathering.IsConjunctive),
        };

    private static ImmutableList<INormalForm> CollectBoth(IBooleanExpression<ILinearArithmetic> left, IBooleanExpression<ILinearArithmetic> right, Gathering gathering, ImmutableList<INormalForm> into) =>
        Collect(right, gathering, Collect(left, gathering, into));

    private static ImmutableList<INormalForm> Splice(ImmutableList<INormalForm> into, INormalForm operand, bool isConjunctive) =>
        operand switch {
            All all when isConjunctive => into.AddRange(all.Operands),
            Any any when !isConjunctive => into.AddRange(any.Operands),
            _ => into.Add(operand),
        };

    /// <summary>
    /// Builds a junction, absorbing constants: a false operand falsifies an <see cref="All"/>, a
    /// true operand satisfies an <see cref="Any"/>, and a lone operand stands for itself.
    /// (Neutral constants have no operands, so <see cref="Splice"/> has already dropped them.)
    /// </summary>
    private static INormalForm Junction(ImmutableList<INormalForm> operands, bool isConjunctive) =>
        operands.Contains(isConjunctive ? False : True) ? (isConjunctive ? False : True)
        : operands.Count == 1 ? operands[0]
        : isConjunctive ? new All(operands)
        : new Any(operands);

    /// <summary><c>difference ~ 0</c>, facing the way it was written; a constant difference is settled on the spot.</summary>
    private static INormalForm OfRelation(AffineForm difference, Relation relation) =>
        difference.IsConstant
            ? (Holds(relation, difference.Constant) ? True : False)
            : new AffineRelation(difference, relation);

    /// <summary>The relation that holds exactly when this one does not.</summary>
    internal static Relation Opposite(Relation relation) =>
        relation switch {
            Relation.LessThan => Relation.GreaterThanOrEqual,
            Relation.LessThanOrEqual => Relation.GreaterThan,
            Relation.Equal => Relation.NotEqual,
            Relation.NotEqual => Relation.Equal,
            Relation.GreaterThanOrEqual => Relation.LessThan,
            Relation.GreaterThan => Relation.LessThanOrEqual,
            _ => throw new NotSupportedException($"Unknown relation: {relation}."),
        };

    internal static bool Holds(Relation relation, double difference, double tolerance = 0) =>
        relation switch {
            Relation.LessThan => difference < -tolerance,
            Relation.LessThanOrEqual => difference <= tolerance,
            Relation.Equal => Math.Abs(difference) <= tolerance,
            Relation.NotEqual => Math.Abs(difference) > tolerance,
            Relation.GreaterThanOrEqual => difference >= -tolerance,
            Relation.GreaterThan => difference > tolerance,
            _ => throw new NotSupportedException($"Unknown relation: {relation}."),
        };
}
