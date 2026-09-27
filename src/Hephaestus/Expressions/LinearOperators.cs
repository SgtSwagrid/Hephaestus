namespace Hephaestus;

/// <summary>
/// Arithmetic and comparison operators over <see cref="ILinearExpression"/>. Operators only build
/// data: <c>a + b</c> is <c>new Sum(a, b)</c>, nothing is simplified or flattened here.
/// </summary>
public static class LinearOperators {
    extension(ILinearExpression) {
        public static ILinearExpression operator +(ILinearExpression left, ILinearExpression right) => new Sum(left, right);
        public static ILinearExpression operator +(ILinearExpression left, double right) => new Sum(left, new Constant(right));
        public static ILinearExpression operator +(double left, ILinearExpression right) => new Sum(new Constant(left), right);

        public static ILinearExpression operator -(ILinearExpression operand) => new Product(-1, operand);
        public static ILinearExpression operator -(ILinearExpression left, ILinearExpression right) => new Sum(left, new Product(-1, right));
        public static ILinearExpression operator -(ILinearExpression left, double right) => new Sum(left, new Constant(-right));
        public static ILinearExpression operator -(double left, ILinearExpression right) => new Sum(new Constant(left), new Product(-1, right));

        public static ILinearExpression operator *(double coefficient, ILinearExpression expression) => new Product(coefficient, expression);
        public static ILinearExpression operator *(ILinearExpression expression, double coefficient) => new Product(coefficient, expression);
        public static ILinearExpression operator /(ILinearExpression expression, double divisor) => new Product(1 / divisor, expression);

        // The one product of two expressions that stays linear: by an indicator, it is the expression or nothing.
        public static ILinearExpression operator *(Indicator gate, ILinearExpression expression) => new Conditional(gate.Condition, expression, new Constant(0));
        public static ILinearExpression operator *(ILinearExpression expression, Indicator gate) => new Conditional(gate.Condition, expression, new Constant(0));

        public static IBooleanExpression<ILinearArithmetic> operator <=(ILinearExpression left, ILinearExpression right) => new LinearRelation(left, Relation.LessThanOrEqual, right);
        public static IBooleanExpression<ILinearArithmetic> operator <=(ILinearExpression left, double right) => new LinearRelation(left, Relation.LessThanOrEqual, new Constant(right));
        public static IBooleanExpression<ILinearArithmetic> operator <=(double left, ILinearExpression right) => new LinearRelation(new Constant(left), Relation.LessThanOrEqual, right);

        public static IBooleanExpression<ILinearArithmetic> operator >=(ILinearExpression left, ILinearExpression right) => new LinearRelation(left, Relation.GreaterThanOrEqual, right);
        public static IBooleanExpression<ILinearArithmetic> operator >=(ILinearExpression left, double right) => new LinearRelation(left, Relation.GreaterThanOrEqual, new Constant(right));
        public static IBooleanExpression<ILinearArithmetic> operator >=(double left, ILinearExpression right) => new LinearRelation(new Constant(left), Relation.GreaterThanOrEqual, right);

        public static IBooleanExpression<ILinearArithmetic> operator <(ILinearExpression left, ILinearExpression right) => new LinearRelation(left, Relation.LessThan, right);
        public static IBooleanExpression<ILinearArithmetic> operator <(ILinearExpression left, double right) => new LinearRelation(left, Relation.LessThan, new Constant(right));
        public static IBooleanExpression<ILinearArithmetic> operator <(double left, ILinearExpression right) => new LinearRelation(new Constant(left), Relation.LessThan, right);

        public static IBooleanExpression<ILinearArithmetic> operator >(ILinearExpression left, ILinearExpression right) => new LinearRelation(left, Relation.GreaterThan, right);
        public static IBooleanExpression<ILinearArithmetic> operator >(ILinearExpression left, double right) => new LinearRelation(left, Relation.GreaterThan, new Constant(right));
        public static IBooleanExpression<ILinearArithmetic> operator >(double left, ILinearExpression right) => new LinearRelation(new Constant(left), Relation.GreaterThan, right);
    }

    extension(Indicator) {
        /// <summary>The product of two indicators, which is one exactly when both conditions hold. (It settles which of them is the gate.)</summary>
        public static ILinearExpression operator *(Indicator gate, Indicator expression) => new Conditional(gate.Condition, expression, new Constant(0));
    }

    extension(IBooleanExpression<ILinearArithmetic> condition) {
        /// <summary>One when this holds, and zero when it does not: <c>needsSetup.Indicator * setupTime</c>, <c>flags.Sum(flag =&gt; flag.Indicator)</c>.</summary>
        public Indicator Indicator => new(condition);
    }

    extension(ILinearExpression expression) {
        /// <summary>
        /// The constraint that this expression equals <paramref name="other"/>. (C# reserves
        /// <c>==</c> on records and interfaces for structural and reference equality.)
        /// </summary>
        public IBooleanExpression<ILinearArithmetic> EqualTo(ILinearExpression other) => new LinearRelation(expression, Relation.Equal, other);

        /// <inheritdoc cref="EqualTo(ILinearExpression, ILinearExpression)"/>
        public IBooleanExpression<ILinearArithmetic> EqualTo(double other) => new LinearRelation(expression, Relation.Equal, new Constant(other));

        /// <summary>The constraint that this expression differs from <paramref name="other"/>.</summary>
        public IBooleanExpression<ILinearArithmetic> NotEqualTo(ILinearExpression other) => new LinearRelation(expression, Relation.NotEqual, other);

        /// <inheritdoc cref="NotEqualTo(ILinearExpression, ILinearExpression)"/>
        public IBooleanExpression<ILinearArithmetic> NotEqualTo(double other) => new LinearRelation(expression, Relation.NotEqual, new Constant(other));

        /// <summary>The constraint <c>lower &lt;= expression &lt;= upper</c>.</summary>
        public IBooleanExpression<ILinearArithmetic> Between(double lower, double upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between(ILinearExpression, double, double)"/>
        public IBooleanExpression<ILinearArithmetic> Between(ILinearExpression lower, ILinearExpression upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between(ILinearExpression, double, double)"/>
        public IBooleanExpression<ILinearArithmetic> Between(double lower, ILinearExpression upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between(ILinearExpression, double, double)"/>
        public IBooleanExpression<ILinearArithmetic> Between(ILinearExpression lower, double upper) => lower <= expression & expression <= upper;
    }

    extension(IEnumerable<ILinearExpression> terms) {
        /// <summary>
        /// The sum of all terms, built as a balanced tree so that very long sums stay shallow.
        /// An empty sequence sums to the constant zero.
        /// </summary>
        public ILinearExpression Sum() => Balanced.Fold<ILinearExpression>([.. terms], new Constant(0), (left, right) => left + right);
    }

    extension<T>(IEnumerable<T> items) {
        /// <summary>The sum of the expressions selected from each item.</summary>
        public ILinearExpression Sum(Func<T, ILinearExpression> selector) => items.Select(selector).Sum();
    }
}
