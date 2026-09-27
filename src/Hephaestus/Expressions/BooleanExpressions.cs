namespace Hephaestus;

/// <summary>
/// Propositional logic: the theory of truths and binary variables, joined by <c>&amp;</c>,
/// <c>|</c>, <c>!</c>, <c>=&gt;</c> and <c>&lt;=&gt;</c>. Every other theory extends it, adding atoms
/// of its own, as <see cref="ILinearArithmetic"/> adds relations between linear expressions. A theory
/// is only ever a type argument: <c>IBooleanExpression&lt;ILogic&gt;</c> is a formula of pure logic.
/// </summary>
public interface ILogic;

/// <summary>
/// A formula of theory <typeparamref name="TTheory"/>: truths combined by logic, over whatever atoms
/// the theory has. The cases are <see cref="BooleanConstant"/>, <see cref="BinaryVariable"/>, the
/// atoms of the theory (<see cref="LinearRelation"/>, for linear arithmetic), and the connectives
/// <see cref="INegation{TTheory}"/>, <see cref="IConjunction{TTheory}"/>,
/// <see cref="IDisjunction{TTheory}"/>, <see cref="IImplication{TTheory}"/>,
/// <see cref="IEquivalence{TTheory}"/> and <see cref="INamedConstraint{TTheory}"/>. Like linear
/// expressions, formulas are plain data kept exactly as written.
/// <para>
/// A formula of a theory is a formula of every theory that extends it: the type is contravariant,
/// so pure logic mixes with anything, and <c>flag &amp; (x &lt;= 4)</c> is a formula of linear
/// arithmetic. So a connective is recognised by its interface rather than its class, since a
/// <c>Conjunction&lt;ILogic&gt;</c> seen as a formula of linear arithmetic is not a
/// <c>Conjunction&lt;ILinearArithmetic&gt;</c>. For the same reason the connectives are classes, not
/// records: a record only ever equals its own instantiation, where <c>a &amp; b</c> is the same formula
/// in whichever theory it is seen.
/// </para>
/// </summary>
public interface IBooleanExpression<in TTheory> : IReadableExpression<bool> {
    bool IReadableExpression<bool>.Read(Solution solution) => Evaluation.Holds(solution, this, Evaluation.Tolerance);
}

/// <summary>
/// Every theory at once: what a formula of any theory can be seen as. Formulas are compared through
/// it, so that equality does not depend on the theory each was built in. A new theory is added here
/// as a base.
/// </summary>
internal interface IEveryTheory : ILinearArithmetic;

/// <summary>A fixed truth value.</summary>
public sealed record BooleanConstant(bool Value) : IBooleanExpression<ILogic> {
    /// <summary>The expression that always holds.</summary>
    public static BooleanConstant True { get; } = new(true);

    /// <summary>The expression that never holds.</summary>
    public static BooleanConstant False { get; } = new(false);
}

/// <summary>Holds exactly when the operand does not.</summary>
public interface INegation<in TTheory> : IBooleanExpression<TTheory> {
    /// <summary>The formula negated.</summary>
    IBooleanExpression<TTheory> Operand { get; }
}

/// <inheritdoc cref="INegation{TTheory}"/>
public sealed class Negation<TTheory>(IBooleanExpression<TTheory> operand) : INegation<TTheory> {
    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Operand => operand;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is INegation<IEveryTheory> other && Operand.Equals(other.Operand);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Negation<>), Operand);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds exactly when both sides hold.</summary>
public interface IConjunction<in TTheory> : IBooleanExpression<TTheory> {
    /// <summary>The left-hand side.</summary>
    IBooleanExpression<TTheory> Left { get; }

    /// <summary>The right-hand side.</summary>
    IBooleanExpression<TTheory> Right { get; }
}

/// <inheritdoc cref="IConjunction{TTheory}"/>
public sealed class Conjunction<TTheory>(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right) : IConjunction<TTheory> {
    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Left => left;

    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Right => right;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IConjunction<IEveryTheory> other && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Conjunction<>), Left, Right);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds exactly when at least one side holds.</summary>
public interface IDisjunction<in TTheory> : IBooleanExpression<TTheory> {
    /// <summary>The left-hand side.</summary>
    IBooleanExpression<TTheory> Left { get; }

    /// <summary>The right-hand side.</summary>
    IBooleanExpression<TTheory> Right { get; }
}

/// <inheritdoc cref="IDisjunction{TTheory}"/>
public sealed class Disjunction<TTheory>(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right) : IDisjunction<TTheory> {
    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Left => left;

    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Right => right;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IDisjunction<IEveryTheory> other && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Disjunction<>), Left, Right);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds unless the antecedent holds and the consequent does not.</summary>
public interface IImplication<in TTheory> : IBooleanExpression<TTheory> {
    /// <summary>What is supposed.</summary>
    IBooleanExpression<TTheory> Antecedent { get; }

    /// <summary>What follows.</summary>
    IBooleanExpression<TTheory> Consequent { get; }
}

/// <inheritdoc cref="IImplication{TTheory}"/>
public sealed class Implication<TTheory>(IBooleanExpression<TTheory> antecedent, IBooleanExpression<TTheory> consequent) : IImplication<TTheory> {
    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Antecedent => antecedent;

    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Consequent => consequent;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IImplication<IEveryTheory> other && Antecedent.Equals(other.Antecedent) && Consequent.Equals(other.Consequent);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Implication<>), Antecedent, Consequent);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds exactly when both sides have the same truth value.</summary>
public interface IEquivalence<in TTheory> : IBooleanExpression<TTheory> {
    /// <summary>The left-hand side.</summary>
    IBooleanExpression<TTheory> Left { get; }

    /// <summary>The right-hand side.</summary>
    IBooleanExpression<TTheory> Right { get; }
}

/// <inheritdoc cref="IEquivalence{TTheory}"/>
public sealed class Equivalence<TTheory>(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right) : IEquivalence<TTheory> {
    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Left => left;

    /// <inheritdoc/>
    public IBooleanExpression<TTheory> Right => right;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IEquivalence<IEveryTheory> other && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Equivalence<>), Left, Right);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}
