namespace Hephaestus;

/// <summary>
/// A predicate of some theory: what a formula is about, beneath its logic. The atoms of linear
/// arithmetic are <see cref="ILinearRelation"/>s, and once a problem has been lowered,
/// <see cref="IAffineRelation"/>s. A formula names in its type the atoms it may contain.
/// </summary>
public interface IAtom;

/// <summary>
/// The atoms of no theory: nothing is one. A formula of pure logic, made of truths and binary
/// variables alone, is a formula over these; and since they lie beneath the atoms of every theory,
/// it is a formula over any theory's too, and mixes with any: <c>flag &amp; (x &lt;= 4)</c> is a
/// formula over <see cref="ILinearRelation"/>. The narrowest kind of atom of every theory is listed
/// here as a base, which is what puts this beneath them all.
/// </summary>
public interface IPropositional : IAffineRelation;

/// <summary>
/// A formula: truths combined by logic, over atoms of type <typeparamref name="TAtom"/>. The cases
/// are <see cref="BooleanConstant"/>, <see cref="BinaryVariable"/>, the atoms themselves, and the
/// connectives <see cref="INegation{TAtom}"/>, <see cref="IConjunction{TAtom}"/>,
/// <see cref="IDisjunction{TAtom}"/>, <see cref="IImplication{TAtom}"/>,
/// <see cref="IEquivalence{TAtom}"/> and <see cref="INamedConstraint{TAtom}"/>. Like linear
/// expressions, formulas are plain data kept exactly as written.
/// <para>
/// A formula over fewer kinds of atom is a formula over more: the type is covariant. So a connective
/// is recognised by its interface rather than its class, since a <c>Conjunction&lt;IPropositional&gt;</c>
/// seen as a formula over <see cref="ILinearRelation"/> is not a <c>Conjunction&lt;ILinearRelation&gt;</c>.
/// For the same reason the connectives are classes rather than records: a record is only ever equal
/// to its own instantiation, where <c>a &amp; b</c> is the same formula whichever atoms it is seen over.
/// </para>
/// </summary>
public interface IBooleanExpression<out TAtom> : IReadableExpression<bool> {
    bool IReadableExpression<bool>.Read(Solution solution) => Evaluation.Holds(solution, this, Evaluation.Tolerance);
}

/// <summary>A fixed truth value.</summary>
public sealed record BooleanConstant(bool Value) : IBooleanExpression<IPropositional> {
    /// <summary>The expression that always holds.</summary>
    public static BooleanConstant True { get; } = new(true);

    /// <summary>The expression that never holds.</summary>
    public static BooleanConstant False { get; } = new(false);
}

/// <summary>Holds exactly when the operand does not.</summary>
public interface INegation<out TAtom> : IBooleanExpression<TAtom> {
    /// <summary>The formula negated.</summary>
    IBooleanExpression<TAtom> Operand { get; }
}

/// <inheritdoc cref="INegation{TAtom}"/>
public sealed class Negation<TAtom>(IBooleanExpression<TAtom> operand) : INegation<TAtom> {
    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Operand => operand;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is INegation<object> other && Operand.Equals(other.Operand);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Negation<>), Operand);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds exactly when both sides hold.</summary>
public interface IConjunction<out TAtom> : IBooleanExpression<TAtom> {
    /// <summary>The left-hand side.</summary>
    IBooleanExpression<TAtom> Left { get; }

    /// <summary>The right-hand side.</summary>
    IBooleanExpression<TAtom> Right { get; }
}

/// <inheritdoc cref="IConjunction{TAtom}"/>
public sealed class Conjunction<TAtom>(IBooleanExpression<TAtom> left, IBooleanExpression<TAtom> right) : IConjunction<TAtom> {
    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Left => left;

    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Right => right;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IConjunction<object> other && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Conjunction<>), Left, Right);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds exactly when at least one side holds.</summary>
public interface IDisjunction<out TAtom> : IBooleanExpression<TAtom> {
    /// <summary>The left-hand side.</summary>
    IBooleanExpression<TAtom> Left { get; }

    /// <summary>The right-hand side.</summary>
    IBooleanExpression<TAtom> Right { get; }
}

/// <inheritdoc cref="IDisjunction{TAtom}"/>
public sealed class Disjunction<TAtom>(IBooleanExpression<TAtom> left, IBooleanExpression<TAtom> right) : IDisjunction<TAtom> {
    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Left => left;

    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Right => right;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IDisjunction<object> other && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Disjunction<>), Left, Right);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds unless the antecedent holds and the consequent does not.</summary>
public interface IImplication<out TAtom> : IBooleanExpression<TAtom> {
    /// <summary>What is supposed.</summary>
    IBooleanExpression<TAtom> Antecedent { get; }

    /// <summary>What follows.</summary>
    IBooleanExpression<TAtom> Consequent { get; }
}

/// <inheritdoc cref="IImplication{TAtom}"/>
public sealed class Implication<TAtom>(IBooleanExpression<TAtom> antecedent, IBooleanExpression<TAtom> consequent) : IImplication<TAtom> {
    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Antecedent => antecedent;

    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Consequent => consequent;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IImplication<object> other && Antecedent.Equals(other.Antecedent) && Consequent.Equals(other.Consequent);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Implication<>), Antecedent, Consequent);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}

/// <summary>Holds exactly when both sides have the same truth value.</summary>
public interface IEquivalence<out TAtom> : IBooleanExpression<TAtom> {
    /// <summary>The left-hand side.</summary>
    IBooleanExpression<TAtom> Left { get; }

    /// <summary>The right-hand side.</summary>
    IBooleanExpression<TAtom> Right { get; }
}

/// <inheritdoc cref="IEquivalence{TAtom}"/>
public sealed class Equivalence<TAtom>(IBooleanExpression<TAtom> left, IBooleanExpression<TAtom> right) : IEquivalence<TAtom> {
    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Left => left;

    /// <inheritdoc/>
    public IBooleanExpression<TAtom> Right => right;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IEquivalence<object> other && Left.Equals(other.Left) && Right.Equals(other.Right);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(nameof(Equivalence<>), Left, Right);

    /// <inheritdoc/>
    public override string ToString() => this.Format();
}
