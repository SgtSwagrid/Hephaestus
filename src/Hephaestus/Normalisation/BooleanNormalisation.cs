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
        public INormalForm Normalise(double epsilon) => Convert(new Step(expression, true, epsilon));
    }

    private sealed record Step(
        IBooleanExpression Expression,
        bool Polarity,
        double Epsilon
    );

    private sealed record Collection(
        Step Step,
        bool IsConjunctive,
        ImmutableList<INormalForm> Into
    );

    private static INormalForm Convert(Step step) => DeepRecursion.Guard(ConvertUnguarded, step);

    private static INormalForm ConvertUnguarded(Step step) =>
        step.Expression switch {
            BooleanConstant constant => constant.Value == step.Polarity ? True : False,
            BinaryVariable variable => new Literal(variable, step.Polarity),
            Comparison comparison => OfComparison(comparison, step),
            Negation negation => Convert(step with { Expression = negation.Operand, Polarity = !step.Polarity }),
            NamedConstraint named => Convert(step with { Expression = named.Expression }),
            Conjunction => OfJunction(step, isConjunctive: step.Polarity),
            Disjunction => OfJunction(step, isConjunctive: !step.Polarity),
            Implication implication => Convert(step with { Expression = !implication.Antecedent | implication.Consequent }),
            Equivalence equivalence => Convert(step with { Expression = equivalence.Left.Implies(equivalence.Right) & equivalence.Right.Implies(equivalence.Left) }),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {step.Expression.GetType().Name}."),
        };

    private static INormalForm OfJunction(Step step, bool isConjunctive) =>
        Junction(Collect(new Collection(step, isConjunctive, [])), isConjunctive);

    /// <summary>Gathers the operands of a maximal run of same-kind junctions, left to right.</summary>
    private static ImmutableList<INormalForm> Collect(Collection collection) => DeepRecursion.Guard(CollectUnguarded, collection);

    private static ImmutableList<INormalForm> CollectUnguarded(Collection collection) =>
        collection.Step.Expression switch {
            Conjunction conjunction when collection.Step.Polarity == collection.IsConjunctive => CollectBoth(collection, conjunction.Left, conjunction.Right),
            Disjunction disjunction when collection.Step.Polarity != collection.IsConjunctive => CollectBoth(collection, disjunction.Left, disjunction.Right),
            _ => Splice(collection.Into, Convert(collection.Step), collection.IsConjunctive),
        };

    private static ImmutableList<INormalForm> CollectBoth(Collection collection, IBooleanExpression left, IBooleanExpression right) =>
        Collect(collection with {
            Step = collection.Step with { Expression = right },
            Into = Collect(collection with { Step = collection.Step with { Expression = left } }),
        });

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

    private static INormalForm OfComparison(Comparison comparison, Step step) =>
        OfRelation(
            (comparison.Left - comparison.Right).Normalise(),
            step.Polarity ? comparison.Relation : Opposite(comparison.Relation),
            step.Epsilon);

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

    private static Relation Opposite(Relation relation) =>
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
