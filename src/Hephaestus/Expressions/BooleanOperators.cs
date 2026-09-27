namespace Hephaestus;

/// <summary>
/// Logical operators over formulas, whatever their theory. As with the linear operators, these only
/// build data; normal forms are computed later, when a model is encoded for a solver. Operands of
/// different theories meet in the one that extends the other: <c>flag &amp; (x &lt;= 4)</c> is a formula
/// of <see cref="ILinearArithmetic"/>.
/// </summary>
public static class BooleanOperators {
    extension<TTheory>(IBooleanExpression<TTheory>) where TTheory : class, ILogic {
        public static IBooleanExpression<TTheory> operator !(IBooleanExpression<TTheory> operand) => new Negation<TTheory>(operand);
        public static IBooleanExpression<TTheory> operator &(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right) => new Conjunction<TTheory>(left, right);
        public static IBooleanExpression<TTheory> operator |(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right) => new Disjunction<TTheory>(left, right);
        public static IBooleanExpression<TTheory> operator ^(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right) => new Negation<TTheory>(new Equivalence<TTheory>(left, right));

        // Plain truth values mix in on either side, for constraints that depend on known data: isUrgent & (start <= cutoff).
        public static IBooleanExpression<TTheory> operator &(IBooleanExpression<TTheory> left, bool right) => new Conjunction<TTheory>(left, Truth(right));
        public static IBooleanExpression<TTheory> operator &(bool left, IBooleanExpression<TTheory> right) => new Conjunction<TTheory>(Truth(left), right);
        public static IBooleanExpression<TTheory> operator |(IBooleanExpression<TTheory> left, bool right) => new Disjunction<TTheory>(left, Truth(right));
        public static IBooleanExpression<TTheory> operator |(bool left, IBooleanExpression<TTheory> right) => new Disjunction<TTheory>(Truth(left), right);
        public static IBooleanExpression<TTheory> operator ^(IBooleanExpression<TTheory> left, bool right) => new Negation<TTheory>(new Equivalence<TTheory>(left, Truth(right)));
        public static IBooleanExpression<TTheory> operator ^(bool left, IBooleanExpression<TTheory> right) => new Negation<TTheory>(new Equivalence<TTheory>(Truth(left), right));
    }

    extension<TTheory>(IBooleanExpression<TTheory> expression) where TTheory : class, ILogic {
        /// <summary>The constraint that <paramref name="consequent"/> holds whenever this expression does.</summary>
        public IBooleanExpression<TTheory> Implies(IBooleanExpression<TTheory> consequent) => new Implication<TTheory>(expression, consequent);

        /// <summary>The constraint that this expression and <paramref name="other"/> hold or fail together.</summary>
        public IBooleanExpression<TTheory> Iff(IBooleanExpression<TTheory> other) => new Equivalence<TTheory>(expression, other);

        /// <inheritdoc cref="Implies{TTheory}(IBooleanExpression{TTheory}, IBooleanExpression{TTheory})"/>
        public IBooleanExpression<TTheory> Implies(bool consequent) => new Implication<TTheory>(expression, Truth(consequent));

        /// <inheritdoc cref="Iff{TTheory}(IBooleanExpression{TTheory}, IBooleanExpression{TTheory})"/>
        public IBooleanExpression<TTheory> Iff(bool other) => new Equivalence<TTheory>(expression, Truth(other));
    }

    extension(bool condition) {
        /// <summary>The constraint that <paramref name="consequent"/> holds if this known condition does: <c>job.IsUrgent.Implies(start &lt;= cutoff)</c>.</summary>
        public IBooleanExpression<TTheory> Implies<TTheory>(IBooleanExpression<TTheory> consequent) where TTheory : class, ILogic => new Implication<TTheory>(Truth(condition), consequent);

        /// <summary>The constraint that <paramref name="other"/> holds exactly when this known condition does.</summary>
        public IBooleanExpression<TTheory> Iff<TTheory>(IBooleanExpression<TTheory> other) where TTheory : class, ILogic => new Equivalence<TTheory>(Truth(condition), other);
    }

    extension<TTheory>(IEnumerable<IBooleanExpression<TTheory>> expressions) where TTheory : class, ILogic {
        /// <summary>
        /// The conjunction of all expressions, built as a balanced tree. An empty sequence is
        /// trivially true.
        /// </summary>
        public IBooleanExpression<TTheory> AllOf() => Balanced.Fold<IBooleanExpression<TTheory>>([.. expressions], Truth(true), (left, right) => left & right);

        /// <summary>
        /// The disjunction of all expressions, built as a balanced tree. An empty sequence is
        /// trivially false.
        /// </summary>
        public IBooleanExpression<TTheory> AnyOf() => Balanced.Fold<IBooleanExpression<TTheory>>([.. expressions], Truth(false), (left, right) => left | right);
    }

    extension<T>(IEnumerable<T> items) {
        /// <summary>The conjunction of the expressions selected from each item.</summary>
        public IBooleanExpression<TTheory> AllOf<TTheory>(Func<T, IBooleanExpression<TTheory>> selector) where TTheory : class, ILogic => items.Select(selector).AllOf();

        /// <summary>The disjunction of the expressions selected from each item.</summary>
        public IBooleanExpression<TTheory> AnyOf<TTheory>(Func<T, IBooleanExpression<TTheory>> selector) where TTheory : class, ILogic => items.Select(selector).AnyOf();
    }

    /// <summary>A plain truth value as a formula, which is one of pure logic and so of every theory.</summary>
    private static BooleanConstant Truth(bool value) => value ? BooleanConstant.True : BooleanConstant.False;
}
