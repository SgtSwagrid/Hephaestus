using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A boolean expression that goes by a name. It means exactly what <see cref="Expression"/> means;
/// the name is what it is called wherever it is written out, on its own or within a larger expression.
/// </summary>
public sealed record NamedConstraint(
    string Name,
    IBooleanExpression Expression
) : IBooleanExpression;

/// <summary>A linear expression that goes by a name, as a <see cref="NamedConstraint"/> does.</summary>
public sealed record NamedTerm(
    string Name,
    ILinearExpression Expression
) : ILinearExpression;

/// <summary>
/// Names for expressions, and the constraints that a problem is made of. A problem has one
/// constraint, but that constraint is usually a conjunction, and its <c>Conjuncts</c> are what a
/// modeller thinks of as "the constraints": the units in which an infeasibility is explained and a
/// shadow price is quoted. Each goes by the name it was given, or else by the way it is written.
/// </summary>
public static class Names {
    extension(IBooleanExpression expression) {
        /// <summary>The same expression, going by <paramref name="name"/>.</summary>
        public IBooleanExpression WithName(string name) => new NamedConstraint(name, expression);

        /// <summary>The name the expression was given, or else the expression as written.</summary>
        public string Name => expression is NamedConstraint named ? named.Name : expression.Format();

        /// <summary>
        /// The constraints that this one is a conjunction of, in the order written: a tree of
        /// <c>&amp;</c> is taken apart, and anything else, a named conjunction included, is one constraint.
        /// </summary>
        public ImmutableArray<IBooleanExpression> Conjuncts => [.. Collect(new Step(expression, []))];
    }

    extension(ILinearExpression expression) {
        /// <summary>The same expression, going by <paramref name="name"/>.</summary>
        public ILinearExpression WithName(string name) => new NamedTerm(name, expression);
    }

    extension(BinaryVariable variable) {
        /// <summary>The same truth value, going by <paramref name="name"/>. (A binary variable is both kinds of expression; a name for it is a name for what it asserts.)</summary>
        public IBooleanExpression WithName(string name) => new NamedConstraint(name, variable);
    }

    extension<T>(Quantity<T> quantity) {
        /// <summary>The same quantity, going by <paramref name="name"/>.</summary>
        public Quantity<T> WithName(string name) => quantity with { Expression = new NamedTerm(name, quantity.Expression) };
    }

    extension<T, TDelta>(Point<T, TDelta> point) {
        /// <summary>The same point, going by <paramref name="name"/>.</summary>
        public Point<T, TDelta> WithName(string name) => point with { Expression = new NamedTerm(name, point.Expression) };
    }

    private sealed record Step(
        IBooleanExpression Expression,
        ImmutableList<IBooleanExpression> Found
    );

    private static ImmutableList<IBooleanExpression> Collect(Step step) => DeepRecursion.Guard(CollectUnguarded, step);

    private static ImmutableList<IBooleanExpression> CollectUnguarded(Step step) =>
        step.Expression switch {
            Conjunction conjunction => Collect(new Step(conjunction.Right, Collect(step with { Expression = conjunction.Left }))),
            BooleanConstant { Value: true } => step.Found,
            _ => step.Found.Add(step.Expression),
        };
}
