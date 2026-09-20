namespace Hephaestus;

/// <summary>
/// What every projected expression can do, whether it is an amount or a position: be compared,
/// with another of its kind or with a plain value, and be measured under another projection. Each
/// reduces to the corresponding operator on the underlying linear expressions, after bringing the
/// right-hand side into the left-hand side's projection.
/// </summary>
public static class TypedComparisons {
    extension<TValue>(ILinearlyEncodable<TValue>) {
        public static IBooleanExpression operator <=(ILinearlyEncodable<TValue> left, ILinearlyEncodable<TValue> right) => left.Expression <= right.In(left.Projection);
        public static IBooleanExpression operator <=(ILinearlyEncodable<TValue> left, TValue right) => left.Expression <= left.Projection.Encode(right);
        public static IBooleanExpression operator <=(TValue left, ILinearlyEncodable<TValue> right) => right.Projection.Encode(left) <= right.Expression;

        public static IBooleanExpression operator >=(ILinearlyEncodable<TValue> left, ILinearlyEncodable<TValue> right) => left.Expression >= right.In(left.Projection);
        public static IBooleanExpression operator >=(ILinearlyEncodable<TValue> left, TValue right) => left.Expression >= left.Projection.Encode(right);
        public static IBooleanExpression operator >=(TValue left, ILinearlyEncodable<TValue> right) => right.Projection.Encode(left) >= right.Expression;

        public static IBooleanExpression operator <(ILinearlyEncodable<TValue> left, ILinearlyEncodable<TValue> right) => left.Expression < right.In(left.Projection);
        public static IBooleanExpression operator <(ILinearlyEncodable<TValue> left, TValue right) => left.Expression < left.Projection.Encode(right);
        public static IBooleanExpression operator <(TValue left, ILinearlyEncodable<TValue> right) => right.Projection.Encode(left) < right.Expression;

        public static IBooleanExpression operator >(ILinearlyEncodable<TValue> left, ILinearlyEncodable<TValue> right) => left.Expression > right.In(left.Projection);
        public static IBooleanExpression operator >(ILinearlyEncodable<TValue> left, TValue right) => left.Expression > left.Projection.Encode(right);
        public static IBooleanExpression operator >(TValue left, ILinearlyEncodable<TValue> right) => right.Projection.Encode(left) > right.Expression;
    }

    extension<TValue>(ILinearlyEncodable<TValue> expression) {
        /// <summary>The constraint that this equals <paramref name="other"/>.</summary>
        public IBooleanExpression EqualTo(ILinearlyEncodable<TValue> other) => expression.Expression.EqualTo(other.In(expression.Projection));

        /// <inheritdoc cref="EqualTo{TValue}(ILinearlyEncodable{TValue}, ILinearlyEncodable{TValue})"/>
        public IBooleanExpression EqualTo(TValue other) => expression.Expression.EqualTo(expression.Projection.Encode(other));

        /// <summary>The constraint that this differs from <paramref name="other"/>.</summary>
        public IBooleanExpression NotEqualTo(ILinearlyEncodable<TValue> other) => expression.Expression.NotEqualTo(other.In(expression.Projection));

        /// <inheritdoc cref="NotEqualTo{TValue}(ILinearlyEncodable{TValue}, ILinearlyEncodable{TValue})"/>
        public IBooleanExpression NotEqualTo(TValue other) => expression.Expression.NotEqualTo(expression.Projection.Encode(other));

        /// <summary>The constraint <c>lower &lt;= this &lt;= upper</c>.</summary>
        public IBooleanExpression Between(TValue lower, TValue upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between{TValue}(ILinearlyEncodable{TValue}, TValue, TValue)"/>
        public IBooleanExpression Between(ILinearlyEncodable<TValue> lower, ILinearlyEncodable<TValue> upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between{TValue}(ILinearlyEncodable{TValue}, TValue, TValue)"/>
        public IBooleanExpression Between(TValue lower, ILinearlyEncodable<TValue> upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between{TValue}(ILinearlyEncodable{TValue}, TValue, TValue)"/>
        public IBooleanExpression Between(ILinearlyEncodable<TValue> lower, TValue upper) => lower <= expression & expression <= upper;

        /// <summary>
        /// The plain linear expression that measures this under another projection, for example a
        /// duration in minutes for use in a cost function.
        /// </summary>
        public ILinearExpression In(IProjection<TValue> projection) => Projecting.Convert(expression.Expression, expression.Projection, projection);
    }
}
