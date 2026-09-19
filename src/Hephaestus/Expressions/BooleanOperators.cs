namespace Hephaestus;

/// <summary>
/// Logical operators over <see cref="IBooleanExpression"/>. As with the linear operators, these
/// only build data; normal forms are computed later, when a model is encoded for a solver.
/// </summary>
public static class BooleanOperators {
    extension(IBooleanExpression) {
        public static IBooleanExpression operator !(IBooleanExpression operand) => new Negation(operand);
        public static IBooleanExpression operator &(IBooleanExpression left, IBooleanExpression right) => new Conjunction(left, right);
        public static IBooleanExpression operator |(IBooleanExpression left, IBooleanExpression right) => new Disjunction(left, right);
        public static IBooleanExpression operator ^(IBooleanExpression left, IBooleanExpression right) => new Negation(new Equivalence(left, right));

        // Plain truth values mix in on either side, for constraints that depend on known data: isFreight & (departure >= curfew).
        public static IBooleanExpression operator &(IBooleanExpression left, bool right) => new Conjunction(left, new BooleanConstant(right));
        public static IBooleanExpression operator &(bool left, IBooleanExpression right) => new Conjunction(new BooleanConstant(left), right);
        public static IBooleanExpression operator |(IBooleanExpression left, bool right) => new Disjunction(left, new BooleanConstant(right));
        public static IBooleanExpression operator |(bool left, IBooleanExpression right) => new Disjunction(new BooleanConstant(left), right);
        public static IBooleanExpression operator ^(IBooleanExpression left, bool right) => new Negation(new Equivalence(left, new BooleanConstant(right)));
        public static IBooleanExpression operator ^(bool left, IBooleanExpression right) => new Negation(new Equivalence(new BooleanConstant(left), right));
    }

    extension(IBooleanExpression expression) {
        /// <summary>The constraint that <paramref name="consequent"/> holds whenever this expression does.</summary>
        public IBooleanExpression Implies(IBooleanExpression consequent) => new Implication(expression, consequent);

        /// <summary>The constraint that this expression and <paramref name="other"/> hold or fail together.</summary>
        public IBooleanExpression Iff(IBooleanExpression other) => new Equivalence(expression, other);

        /// <inheritdoc cref="Implies(IBooleanExpression, IBooleanExpression)"/>
        public IBooleanExpression Implies(bool consequent) => new Implication(expression, new BooleanConstant(consequent));

        /// <inheritdoc cref="Iff(IBooleanExpression, IBooleanExpression)"/>
        public IBooleanExpression Iff(bool other) => new Equivalence(expression, new BooleanConstant(other));
    }

    extension(bool condition) {
        /// <summary>The constraint that <paramref name="consequent"/> holds if this known condition does: <c>train.IsFreight.Implies(departure &gt;= curfew)</c>.</summary>
        public IBooleanExpression Implies(IBooleanExpression consequent) => new Implication(new BooleanConstant(condition), consequent);

        /// <summary>The constraint that <paramref name="other"/> holds exactly when this known condition does.</summary>
        public IBooleanExpression Iff(IBooleanExpression other) => new Equivalence(new BooleanConstant(condition), other);
    }

    extension(IEnumerable<IBooleanExpression> expressions) {
        /// <summary>
        /// The conjunction of all expressions, built as a balanced tree. An empty sequence is
        /// trivially true.
        /// </summary>
        public IBooleanExpression AllOf() => Balanced.Fold<IBooleanExpression>([.. expressions], BooleanConstant.True, (left, right) => left & right);

        /// <summary>
        /// The disjunction of all expressions, built as a balanced tree. An empty sequence is
        /// trivially false.
        /// </summary>
        public IBooleanExpression AnyOf() => Balanced.Fold<IBooleanExpression>([.. expressions], BooleanConstant.False, (left, right) => left | right);
    }

    extension<T>(IEnumerable<T> items) {
        /// <summary>The conjunction of the expressions selected from each item.</summary>
        public IBooleanExpression AllOf(Func<T, IBooleanExpression> selector) => items.Select(selector).AllOf();

        /// <summary>The disjunction of the expressions selected from each item.</summary>
        public IBooleanExpression AnyOf(Func<T, IBooleanExpression> selector) => items.Select(selector).AnyOf();
    }
}
