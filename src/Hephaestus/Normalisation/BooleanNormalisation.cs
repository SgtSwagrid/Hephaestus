using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>The pass that turns an as-written boolean expression into negation normal form.</summary>
internal static class BooleanNormalisation {
    public static INormalForm True { get; } = new All([]);

    public static INormalForm False { get; } = new Any([]);

    extension(IBooleanExpression expression) {
        /// <param name="epsilon">
        /// The gap that stands in for strictness over the reals: <c>e &lt; 0</c> becomes
        /// <c>e + epsilon &lt;= 0</c>. Whole-valued expressions use a gap of exactly one instead.
        /// </param>
        public INormalForm Normalise(double epsilon) => Convert(expression, true, epsilon);
    }

    /// <summary>What a run of same-kind junctions is being gathered under; none of it changes as the run is walked.</summary>
    private sealed record Gathering(
        bool IsConjunctive,
        bool Polarity,
        double Epsilon
    );

    private static INormalForm Convert(IBooleanExpression expression, bool polarity, double epsilon) =>
        DeepRecursion.Guard(ConvertUnguarded, expression, polarity, epsilon);

    private static INormalForm ConvertUnguarded(IBooleanExpression expression, bool polarity, double epsilon) =>
        expression switch {
            BooleanConstant constant => constant.Value == polarity ? True : False,
            BinaryVariable variable => new Literal(variable, polarity),
            Comparison comparison => OfComparison(comparison, polarity, epsilon),
            Negation negation => Convert(negation.Operand, !polarity, epsilon),
            NamedConstraint named => Convert(named.Expression, polarity, epsilon),
            Conjunction => OfJunction(expression, new Gathering(IsConjunctive: polarity, polarity, epsilon)),
            Disjunction => OfJunction(expression, new Gathering(IsConjunctive: !polarity, polarity, epsilon)),
            Implication implication => Convert(!implication.Antecedent | implication.Consequent, polarity, epsilon),
            Equivalence equivalence => Convert(equivalence.Left.Implies(equivalence.Right) & equivalence.Right.Implies(equivalence.Left), polarity, epsilon),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };

    private static INormalForm OfJunction(IBooleanExpression expression, Gathering gathering) =>
        Junction(Collect(expression, gathering, []), gathering.IsConjunctive);

    /// <summary>Gathers the operands of a maximal run of same-kind junctions, left to right.</summary>
    private static ImmutableList<INormalForm> Collect(IBooleanExpression expression, Gathering gathering, ImmutableList<INormalForm> into) =>
        DeepRecursion.Guard(CollectUnguarded, expression, gathering, into);

    private static ImmutableList<INormalForm> CollectUnguarded(IBooleanExpression expression, Gathering gathering, ImmutableList<INormalForm> into) =>
        expression switch {
            Conjunction conjunction when gathering.Polarity == gathering.IsConjunctive => CollectBoth(conjunction.Left, conjunction.Right, gathering, into),
            Disjunction disjunction when gathering.Polarity != gathering.IsConjunctive => CollectBoth(disjunction.Left, disjunction.Right, gathering, into),
            _ => Splice(into, Convert(expression, gathering.Polarity, gathering.Epsilon), gathering.IsConjunctive),
        };

    private static ImmutableList<INormalForm> CollectBoth(IBooleanExpression left, IBooleanExpression right, Gathering gathering, ImmutableList<INormalForm> into) =>
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

    private static INormalForm OfComparison(Comparison comparison, bool polarity, double epsilon) =>
        OfRelation(
            (comparison.Left - comparison.Right).Normalise(),
            polarity ? comparison.Relation : Opposite(comparison.Relation),
            epsilon);

    private static INormalForm OfRelation(AffineForm difference, Relation relation, double epsilon) =>
        difference.IsConstant
            ? (Holds(relation, difference.Constant) ? True : False)
            : relation switch {
                Relation.LessThanOrEqual => new Atom(difference, IsEquality: false),
                Relation.GreaterThanOrEqual => new Atom(difference.Negated, IsEquality: false),
                Relation.LessThan => new Atom(difference.Plus(Gap(difference, epsilon)), IsEquality: false),
                Relation.GreaterThan => new Atom(difference.Negated.Plus(Gap(difference, epsilon)), IsEquality: false),
                Relation.Equal => new Atom(WithPositiveLead(difference), IsEquality: true),
                Relation.NotEqual => new Any([
                    new Atom(difference.Plus(Gap(difference, epsilon)), IsEquality: false),
                    new Atom(difference.Negated.Plus(Gap(difference, epsilon)), IsEquality: false),
                ]),
                _ => throw new NotSupportedException($"Unknown relation: {relation}."),
            };

    private static double Gap(AffineForm difference, double epsilon) => difference.IsIntegral ? 1 : epsilon;

    /// <summary>Equalities are sign-symmetric; fixing the sign makes <c>a == b</c> and <c>b == a</c> the same atom.</summary>
    private static AffineForm WithPositiveLead(AffineForm form) =>
        form.Coefficients.First().Value < 0 ? form.Negated : form;

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
