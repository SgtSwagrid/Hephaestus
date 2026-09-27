namespace Hephaestus;

/// <summary>
/// Logical operators over formulas, whatever their atoms. As with the linear operators, these only
/// build data; normal forms are computed later, when a model is encoded for a solver. Operands over
/// different atoms meet over the larger: <c>flag &amp; (x &lt;= 4)</c> is over <see cref="ILinearRelation"/>.
/// </summary>
public static class BooleanOperators {
    extension<TAtom>(IBooleanExpression<TAtom>) {
        public static IBooleanExpression<TAtom> operator !(IBooleanExpression<TAtom> operand) => new Negation<TAtom>(operand);
        public static IBooleanExpression<TAtom> operator &(IBooleanExpression<TAtom> left, IBooleanExpression<TAtom> right) => new Conjunction<TAtom>(left, right);
        public static IBooleanExpression<TAtom> operator |(IBooleanExpression<TAtom> left, IBooleanExpression<TAtom> right) => new Disjunction<TAtom>(left, right);
        public static IBooleanExpression<TAtom> operator ^(IBooleanExpression<TAtom> left, IBooleanExpression<TAtom> right) => new Negation<TAtom>(new Equivalence<TAtom>(left, right));

        // Plain truth values mix in on either side, for constraints that depend on known data: isUrgent & (start <= cutoff).
        public static IBooleanExpression<TAtom> operator &(IBooleanExpression<TAtom> left, bool right) => new Conjunction<TAtom>(left, Propositions.Over<TAtom>(right));
        public static IBooleanExpression<TAtom> operator &(bool left, IBooleanExpression<TAtom> right) => new Conjunction<TAtom>(Propositions.Over<TAtom>(left), right);
        public static IBooleanExpression<TAtom> operator |(IBooleanExpression<TAtom> left, bool right) => new Disjunction<TAtom>(left, Propositions.Over<TAtom>(right));
        public static IBooleanExpression<TAtom> operator |(bool left, IBooleanExpression<TAtom> right) => new Disjunction<TAtom>(Propositions.Over<TAtom>(left), right);
        public static IBooleanExpression<TAtom> operator ^(IBooleanExpression<TAtom> left, bool right) => new Negation<TAtom>(new Equivalence<TAtom>(left, Propositions.Over<TAtom>(right)));
        public static IBooleanExpression<TAtom> operator ^(bool left, IBooleanExpression<TAtom> right) => new Negation<TAtom>(new Equivalence<TAtom>(Propositions.Over<TAtom>(left), right));
    }

    extension<TAtom>(IBooleanExpression<TAtom> expression) {
        /// <summary>The constraint that <paramref name="consequent"/> holds whenever this expression does.</summary>
        public IBooleanExpression<TAtom> Implies(IBooleanExpression<TAtom> consequent) => new Implication<TAtom>(expression, consequent);

        /// <summary>The constraint that this expression and <paramref name="other"/> hold or fail together.</summary>
        public IBooleanExpression<TAtom> Iff(IBooleanExpression<TAtom> other) => new Equivalence<TAtom>(expression, other);

        /// <inheritdoc cref="Implies{TAtom}(IBooleanExpression{TAtom}, IBooleanExpression{TAtom})"/>
        public IBooleanExpression<TAtom> Implies(bool consequent) => new Implication<TAtom>(expression, Propositions.Over<TAtom>(consequent));

        /// <inheritdoc cref="Iff{TAtom}(IBooleanExpression{TAtom}, IBooleanExpression{TAtom})"/>
        public IBooleanExpression<TAtom> Iff(bool other) => new Equivalence<TAtom>(expression, Propositions.Over<TAtom>(other));
    }

    extension(bool condition) {
        /// <summary>The constraint that <paramref name="consequent"/> holds if this known condition does: <c>job.IsUrgent.Implies(start &lt;= cutoff)</c>.</summary>
        public IBooleanExpression<TAtom> Implies<TAtom>(IBooleanExpression<TAtom> consequent) => new Implication<TAtom>(Propositions.Over<TAtom>(condition), consequent);

        /// <summary>The constraint that <paramref name="other"/> holds exactly when this known condition does.</summary>
        public IBooleanExpression<TAtom> Iff<TAtom>(IBooleanExpression<TAtom> other) => new Equivalence<TAtom>(Propositions.Over<TAtom>(condition), other);
    }

    extension<TAtom>(IEnumerable<IBooleanExpression<TAtom>> expressions) {
        /// <summary>
        /// The conjunction of all expressions, built as a balanced tree. An empty sequence is
        /// trivially true.
        /// </summary>
        public IBooleanExpression<TAtom> AllOf() => Balanced.Fold([.. expressions], Propositions.Over<TAtom>(true), (left, right) => left & right);

        /// <summary>
        /// The disjunction of all expressions, built as a balanced tree. An empty sequence is
        /// trivially false.
        /// </summary>
        public IBooleanExpression<TAtom> AnyOf() => Balanced.Fold([.. expressions], Propositions.Over<TAtom>(false), (left, right) => left | right);
    }

    extension<T>(IEnumerable<T> items) {
        /// <summary>The conjunction of the expressions selected from each item.</summary>
        public IBooleanExpression<TAtom> AllOf<TAtom>(Func<T, IBooleanExpression<TAtom>> selector) => items.Select(selector).AllOf();

        /// <summary>The disjunction of the expressions selected from each item.</summary>
        public IBooleanExpression<TAtom> AnyOf<TAtom>(Func<T, IBooleanExpression<TAtom>> selector) => items.Select(selector).AnyOf();
    }
}

/// <summary>
/// Formulas of pure logic, seen as formulas over atoms of some other kind. Every kind of atom lies
/// above <see cref="IPropositional"/>, so the conversion always succeeds; but C# has no way to say
/// that a type parameter lies above another, so generic code has to ask for it at run time.
/// </summary>
internal static class Propositions {
    /// <summary>A truth, as a formula over atoms of type <typeparamref name="TAtom"/>.</summary>
    public static IBooleanExpression<TAtom> Over<TAtom>(bool value) => Over<TAtom>(value ? BooleanConstant.True : BooleanConstant.False);

    /// <summary>A formula of pure logic, as one over atoms of type <typeparamref name="TAtom"/>.</summary>
    public static IBooleanExpression<TAtom> Over<TAtom>(IBooleanExpression<IPropositional> formula) => (IBooleanExpression<TAtom>)formula;
}
