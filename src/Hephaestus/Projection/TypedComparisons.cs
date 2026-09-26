namespace Hephaestus;

/// <summary>
/// What every typed expression can do, whatever it is made of: be compared, with another of its
/// type or with a plain value, after bringing the right-hand side into the left-hand side's
/// projection. Comparison is entry by entry: numbers are compared as numbers and truths by
/// implication, false being less than true, so <c>&lt;=</c> between two pairs is <c>&lt;=</c>
/// between both halves, and two values differ where any of their entries do. A quantity, having
/// one entry, compares as its underlying expression does.
/// </summary>
public static class TypedComparisons {
    extension<TValue>(IEncodable<TValue>) {
        public static IBooleanExpression operator <=(IEncodable<TValue> left, IEncodable<TValue> right) => Relate(left, Relation.LessThanOrEqual, right);
        public static IBooleanExpression operator <=(IEncodable<TValue> left, TValue right) => Relate(left, Relation.LessThanOrEqual, right);
        public static IBooleanExpression operator <=(TValue left, IEncodable<TValue> right) => Relate(left, Relation.LessThanOrEqual, right);

        public static IBooleanExpression operator >=(IEncodable<TValue> left, IEncodable<TValue> right) => Relate(left, Relation.GreaterThanOrEqual, right);
        public static IBooleanExpression operator >=(IEncodable<TValue> left, TValue right) => Relate(left, Relation.GreaterThanOrEqual, right);
        public static IBooleanExpression operator >=(TValue left, IEncodable<TValue> right) => Relate(left, Relation.GreaterThanOrEqual, right);

        public static IBooleanExpression operator <(IEncodable<TValue> left, IEncodable<TValue> right) => Relate(left, Relation.LessThan, right);
        public static IBooleanExpression operator <(IEncodable<TValue> left, TValue right) => Relate(left, Relation.LessThan, right);
        public static IBooleanExpression operator <(TValue left, IEncodable<TValue> right) => Relate(left, Relation.LessThan, right);

        public static IBooleanExpression operator >(IEncodable<TValue> left, IEncodable<TValue> right) => Relate(left, Relation.GreaterThan, right);
        public static IBooleanExpression operator >(IEncodable<TValue> left, TValue right) => Relate(left, Relation.GreaterThan, right);
        public static IBooleanExpression operator >(TValue left, IEncodable<TValue> right) => Relate(left, Relation.GreaterThan, right);
    }

    extension<TValue>(IEncodable<TValue> expression) {
        /// <summary>The constraint that this equals <paramref name="other"/>.</summary>
        public IBooleanExpression EqualTo(IEncodable<TValue> other) => Relate(expression, Relation.Equal, other);

        /// <inheritdoc cref="EqualTo{TValue}(IEncodable{TValue}, IEncodable{TValue})"/>
        public IBooleanExpression EqualTo(TValue other) => Relate(expression, Relation.Equal, other);

        /// <summary>The constraint that this differs from <paramref name="other"/>.</summary>
        public IBooleanExpression NotEqualTo(IEncodable<TValue> other) => Relate(expression, Relation.NotEqual, other);

        /// <inheritdoc cref="NotEqualTo{TValue}(IEncodable{TValue}, IEncodable{TValue})"/>
        public IBooleanExpression NotEqualTo(TValue other) => Relate(expression, Relation.NotEqual, other);

        /// <summary>The constraint <c>lower &lt;= this &lt;= upper</c>.</summary>
        public IBooleanExpression Between(TValue lower, TValue upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between{TValue}(IEncodable{TValue}, TValue, TValue)"/>
        public IBooleanExpression Between(IEncodable<TValue> lower, IEncodable<TValue> upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between{TValue}(IEncodable{TValue}, TValue, TValue)"/>
        public IBooleanExpression Between(TValue lower, IEncodable<TValue> upper) => lower <= expression & expression <= upper;

        /// <inheritdoc cref="Between{TValue}(IEncodable{TValue}, TValue, TValue)"/>
        public IBooleanExpression Between(IEncodable<TValue> lower, TValue upper) => lower <= expression & expression <= upper;
    }

    extension<TValue>(ILinearlyEncodable<TValue> expression) {
        /// <summary>
        /// The plain linear expression that measures this under another projection, for example a
        /// duration in minutes for use in a cost function.
        /// </summary>
        public ILinearExpression In(IProjection<TValue> projection) => Projecting.Convert(expression.Expression, expression.Projection, projection);
    }

    private static IBooleanExpression Relate<TValue>(IEncodable<TValue> left, Relation relation, IEncodable<TValue> right) =>
        Componentwise.Relate(left.Components, relation, Componentwise.Convert(right.Components, right.Projection, left.Projection, like: left.Components));

    private static IBooleanExpression Relate<TValue>(IEncodable<TValue> left, Relation relation, TValue right) =>
        Componentwise.Relate(left.Components, relation, Componentwise.Constants(left.Components, left.Projection.Encode(right)));

    private static IBooleanExpression Relate<TValue>(TValue left, Relation relation, IEncodable<TValue> right) =>
        Componentwise.Relate(Componentwise.Constants(right.Components, right.Projection.Encode(left)), relation, right.Components);
}
