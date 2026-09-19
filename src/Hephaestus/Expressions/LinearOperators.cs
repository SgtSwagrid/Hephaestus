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

        public static IBooleanExpression operator <=(ILinearExpression left, ILinearExpression right) => new Comparison(left, Relation.LessThanOrEqual, right);
        public static IBooleanExpression operator <=(ILinearExpression left, double right) => new Comparison(left, Relation.LessThanOrEqual, new Constant(right));
        public static IBooleanExpression operator <=(double left, ILinearExpression right) => new Comparison(new Constant(left), Relation.LessThanOrEqual, right);

        public static IBooleanExpression operator >=(ILinearExpression left, ILinearExpression right) => new Comparison(left, Relation.GreaterThanOrEqual, right);
        public static IBooleanExpression operator >=(ILinearExpression left, double right) => new Comparison(left, Relation.GreaterThanOrEqual, new Constant(right));
        public static IBooleanExpression operator >=(double left, ILinearExpression right) => new Comparison(new Constant(left), Relation.GreaterThanOrEqual, right);

        public static IBooleanExpression operator <(ILinearExpression left, ILinearExpression right) => new Comparison(left, Relation.LessThan, right);
        public static IBooleanExpression operator <(ILinearExpression left, double right) => new Comparison(left, Relation.LessThan, new Constant(right));
        public static IBooleanExpression operator <(double left, ILinearExpression right) => new Comparison(new Constant(left), Relation.LessThan, right);

        public static IBooleanExpression operator >(ILinearExpression left, ILinearExpression right) => new Comparison(left, Relation.GreaterThan, right);
        public static IBooleanExpression operator >(ILinearExpression left, double right) => new Comparison(left, Relation.GreaterThan, new Constant(right));
        public static IBooleanExpression operator >(double left, ILinearExpression right) => new Comparison(new Constant(left), Relation.GreaterThan, right);
    }

    extension(ILinearExpression expression) {
        /// <summary>
        /// The constraint that this expression equals <paramref name="other"/>. (C# reserves
        /// <c>==</c> on records and interfaces for structural and reference equality.)
        /// </summary>
        public IBooleanExpression EqualTo(ILinearExpression other) => new Comparison(expression, Relation.Equal, other);

        /// <inheritdoc cref="EqualTo(ILinearExpression, ILinearExpression)"/>
        public IBooleanExpression EqualTo(double other) => new Comparison(expression, Relation.Equal, new Constant(other));

        /// <summary>The constraint that this expression differs from <paramref name="other"/>.</summary>
        public IBooleanExpression NotEqualTo(ILinearExpression other) => new Comparison(expression, Relation.NotEqual, other);

        /// <inheritdoc cref="NotEqualTo(ILinearExpression, ILinearExpression)"/>
        public IBooleanExpression NotEqualTo(double other) => new Comparison(expression, Relation.NotEqual, new Constant(other));

        /// <summary>The constraint <c>lower &lt;= expression &lt;= upper</c>.</summary>
        public IBooleanExpression Between(double lower, double upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between(ILinearExpression, double, double)"/>
        public IBooleanExpression Between(ILinearExpression lower, ILinearExpression upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between(ILinearExpression, double, double)"/>
        public IBooleanExpression Between(double lower, ILinearExpression upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between(ILinearExpression, double, double)"/>
        public IBooleanExpression Between(ILinearExpression lower, double upper) => lower <= expression & expression <= upper;
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
