using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A formula that goes by a name. It means exactly what <see cref="Expression"/> means; the name is
/// what it is called wherever it is written out, on its own or within a larger formula.
/// </summary>
public interface INamedConstraint<in TTheory> : IBooleanExpression<TTheory> {
    /// <summary>What it is called.</summary>
    string Name { get; }

    /// <summary>What it means.</summary>
    IBooleanExpression<TTheory> Expression { get; }
}

/// <inheritdoc cref="INamedConstraint{TTheory}"/>
/// <remarks>A class rather than a record, as the connectives are, so that it is equal to itself in whichever theory it is seen.</remarks>
public sealed class NamedConstraint<TTheory>(string name, IBooleanExpression<TTheory> expression) : INamedConstraint<TTheory> {
    /// <inheritdoc/>
    public string Name => name;

    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Expression => expression;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is INamedConstraint<IEveryTheory> other && Name == other.Name && Expression.Equals(other.Expression);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(NamedConstraint<>), Name, Expression);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>A linear expression that goes by a name, as a <see cref="NamedConstraint{TTheory}"/> does.</summary>
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
    extension<TTheory>(IBooleanExpression<TTheory> expression) {
        /// <summary>The same expression, going by <paramref name="name"/>.</summary>
        public IBooleanExpression<TTheory> WithName(string name) => new NamedConstraint<TTheory>(name, expression);

        /// <summary>The name the expression was given, or else the expression as written.</summary>
        public string Name => expression is INamedConstraint<TTheory> named ? named.Name : expression.Format();

        /// <summary>
        /// The constraints that this one is a conjunction of, in the order written: a tree of
        /// <c>&amp;</c> is taken apart, and anything else, a named conjunction included, is one constraint.
        /// </summary>
        public ImmutableArray<IBooleanExpression<TTheory>> Conjuncts => [.. Collect(expression, [])];
    }

    extension(ILinearExpression expression) {
        /// <summary>The same expression, going by <paramref name="name"/>.</summary>
        public ILinearExpression WithName(string name) => new NamedTerm(name, expression);
    }

    extension<T>(Quantity<T> quantity) {
        /// <summary>The same quantity, going by <paramref name="name"/>.</summary>
        public Quantity<T> WithName(string name) => quantity with { Expression = new NamedTerm(name, quantity.Expression) };
    }

    extension<T, TDelta>(Point<T, TDelta> point) {
        /// <summary>The same point, going by <paramref name="name"/>.</summary>
        public Point<T, TDelta> WithName(string name) => point with { Expression = new NamedTerm(name, point.Expression) };
    }

    private static ImmutableList<IBooleanExpression<TTheory>> Collect<TTheory>(IBooleanExpression<TTheory> expression, ImmutableList<IBooleanExpression<TTheory>> found) =>
        DeepRecursion.Guard(CollectUnguarded, expression, found);

    private static ImmutableList<IBooleanExpression<TTheory>> CollectUnguarded<TTheory>(IBooleanExpression<TTheory> expression, ImmutableList<IBooleanExpression<TTheory>> found) =>
        expression switch {
            IConjunction<TTheory> conjunction => Collect(conjunction.Right, Collect(conjunction.Left, found)),
            BooleanConstant { Value: true } => found,
            _ => found.Add(expression),
        };
}
