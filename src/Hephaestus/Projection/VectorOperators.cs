using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// Vectors combined entry by entry. <c>Zip</c> and <c>Select</c> serve vectors of anything; the
/// operators on vectors of quantities are those two applied to the operators of a quantity, so
/// <c>u + v &lt; [4, 5, 6]</c> is <c>(u0 + v0 &lt; 4) &amp; (u1 + v1 &lt; 5) &amp; (u2 + v2 &lt; 6)</c>.
/// Plain arrays and plain values mix in on either side; a plain value stands for itself in every
/// entry. A comparison holds when it holds in every entry, so the negation of <c>u &lt;= w</c> is
/// that some entry exceeds, not <c>u &gt; w</c>.
/// </summary>
public static class VectorOperators {
    extension<TElement>(Vector<TElement> vector) {
        /// <summary>The number of elements.</summary>
        public int Dimension => vector.Elements.Length;

        /// <summary>Each element, mapped: <c>u.Select(entry =&gt; entry &gt;= 0.0)</c>.</summary>
        public Vector<TOther> Select<TOther>(Func<TElement, TOther> selector) => new([.. vector.Elements.Select(selector)]);

        /// <summary>The elements of this vector and <paramref name="other"/>, paired entry by entry.</summary>
        /// <exception cref="ArgumentException">The vectors have different dimensions.</exception>
        public Vector<(TElement, TOther)> Zip<TOther>(Vector<TOther> other) => new([.. Componentwise.Paired(vector.Elements, other.Elements)]);
    }

    extension<TFirst, TSecond>(Vector<(TFirst, TSecond)> pairs) {
        /// <summary>Each pair, mapped from its two halves: <c>u.Zip(v).Select((a, b) =&gt; a + b)</c>.</summary>
        public Vector<TOther> Select<TOther>(Func<TFirst, TSecond, TOther> selector) => pairs.Select(pair => selector(pair.Item1, pair.Item2));
    }

    extension<T>(Vector<Quantity<T>>) {
        public static Vector<Quantity<T>> operator +(Vector<Quantity<T>> left, Vector<Quantity<T>> right) => left.Zip(right).Select((l, r) => l + r);
        public static Vector<Quantity<T>> operator +(Vector<Quantity<T>> left, ImmutableArray<T> right) => left.Zip(Vector.Of(right)).Select((l, r) => l + r);
        public static Vector<Quantity<T>> operator +(ImmutableArray<T> left, Vector<Quantity<T>> right) => Vector.Of(left).Zip(right).Select((l, r) => l + r);
        public static Vector<Quantity<T>> operator +(Vector<Quantity<T>> left, T right) => left.Select(l => l + right);
        public static Vector<Quantity<T>> operator +(T left, Vector<Quantity<T>> right) => right.Select(r => left + r);

        public static Vector<Quantity<T>> operator -(Vector<Quantity<T>> operand) => operand.Select(entry => -entry);
        public static Vector<Quantity<T>> operator -(Vector<Quantity<T>> left, Vector<Quantity<T>> right) => left.Zip(right).Select((l, r) => l - r);
        public static Vector<Quantity<T>> operator -(Vector<Quantity<T>> left, ImmutableArray<T> right) => left.Zip(Vector.Of(right)).Select((l, r) => l - r);
        public static Vector<Quantity<T>> operator -(ImmutableArray<T> left, Vector<Quantity<T>> right) => Vector.Of(left).Zip(right).Select((l, r) => l - r);
        public static Vector<Quantity<T>> operator -(Vector<Quantity<T>> left, T right) => left.Select(l => l - right);
        public static Vector<Quantity<T>> operator -(T left, Vector<Quantity<T>> right) => right.Select(r => left - r);

        public static Vector<Quantity<T>> operator *(double factor, Vector<Quantity<T>> vector) => vector.Select(entry => factor * entry);
        public static Vector<Quantity<T>> operator *(Vector<Quantity<T>> vector, double factor) => vector.Select(entry => entry * factor);
        public static Vector<Quantity<T>> operator /(Vector<Quantity<T>> vector, double divisor) => vector.Select(entry => entry / divisor);

        public static IBooleanExpression operator <=(Vector<Quantity<T>> left, Vector<Quantity<T>> right) => left.Zip(right).Select((l, r) => l <= r).AllOf();
        public static IBooleanExpression operator <=(Vector<Quantity<T>> left, ImmutableArray<T> right) => left.Zip(Vector.Of(right)).Select((l, r) => l <= r).AllOf();
        public static IBooleanExpression operator <=(ImmutableArray<T> left, Vector<Quantity<T>> right) => Vector.Of(left).Zip(right).Select((l, r) => l <= r).AllOf();
        public static IBooleanExpression operator <=(Vector<Quantity<T>> left, T right) => left.Select(l => l <= right).AllOf();
        public static IBooleanExpression operator <=(T left, Vector<Quantity<T>> right) => right.Select(r => left <= r).AllOf();

        public static IBooleanExpression operator >=(Vector<Quantity<T>> left, Vector<Quantity<T>> right) => left.Zip(right).Select((l, r) => l >= r).AllOf();
        public static IBooleanExpression operator >=(Vector<Quantity<T>> left, ImmutableArray<T> right) => left.Zip(Vector.Of(right)).Select((l, r) => l >= r).AllOf();
        public static IBooleanExpression operator >=(ImmutableArray<T> left, Vector<Quantity<T>> right) => Vector.Of(left).Zip(right).Select((l, r) => l >= r).AllOf();
        public static IBooleanExpression operator >=(Vector<Quantity<T>> left, T right) => left.Select(l => l >= right).AllOf();
        public static IBooleanExpression operator >=(T left, Vector<Quantity<T>> right) => right.Select(r => left >= r).AllOf();

        public static IBooleanExpression operator <(Vector<Quantity<T>> left, Vector<Quantity<T>> right) => left.Zip(right).Select((l, r) => l < r).AllOf();
        public static IBooleanExpression operator <(Vector<Quantity<T>> left, ImmutableArray<T> right) => left.Zip(Vector.Of(right)).Select((l, r) => l < r).AllOf();
        public static IBooleanExpression operator <(ImmutableArray<T> left, Vector<Quantity<T>> right) => Vector.Of(left).Zip(right).Select((l, r) => l < r).AllOf();
        public static IBooleanExpression operator <(Vector<Quantity<T>> left, T right) => left.Select(l => l < right).AllOf();
        public static IBooleanExpression operator <(T left, Vector<Quantity<T>> right) => right.Select(r => left < r).AllOf();

        public static IBooleanExpression operator >(Vector<Quantity<T>> left, Vector<Quantity<T>> right) => left.Zip(right).Select((l, r) => l > r).AllOf();
        public static IBooleanExpression operator >(Vector<Quantity<T>> left, ImmutableArray<T> right) => left.Zip(Vector.Of(right)).Select((l, r) => l > r).AllOf();
        public static IBooleanExpression operator >(ImmutableArray<T> left, Vector<Quantity<T>> right) => Vector.Of(left).Zip(right).Select((l, r) => l > r).AllOf();
        public static IBooleanExpression operator >(Vector<Quantity<T>> left, T right) => left.Select(l => l > right).AllOf();
        public static IBooleanExpression operator >(T left, Vector<Quantity<T>> right) => right.Select(r => left > r).AllOf();
    }

    extension<T>(Vector<Quantity<T>> vector) {
        /// <summary>The constraint that this equals <paramref name="other"/> in every entry.</summary>
        public IBooleanExpression EqualTo(Vector<Quantity<T>> other) => vector.Zip(other).Select((l, r) => l.EqualTo(r)).AllOf();

        /// <inheritdoc cref="EqualTo{T}(Vector{Quantity{T}}, Vector{Quantity{T}})"/>
        public IBooleanExpression EqualTo(ImmutableArray<T> other) => vector.Zip(Vector.Of(other)).Select((l, r) => l.EqualTo(r)).AllOf();

        /// <summary>The constraint that this differs from <paramref name="other"/> in some entry.</summary>
        public IBooleanExpression NotEqualTo(Vector<Quantity<T>> other) => vector.Zip(other).Select((l, r) => l.NotEqualTo(r)).AnyOf();

        /// <inheritdoc cref="NotEqualTo{T}(Vector{Quantity{T}}, Vector{Quantity{T}})"/>
        public IBooleanExpression NotEqualTo(ImmutableArray<T> other) => vector.Zip(Vector.Of(other)).Select((l, r) => l.NotEqualTo(r)).AnyOf();

        /// <summary>The sum of the entries, under the projection of the first.</summary>
        /// <exception cref="InvalidOperationException">The vector is empty, so there is no projection to sum under.</exception>
        public Quantity<T> Sum() => vector.Elements.Sum();

        /// <summary>The sum of the entries, each weighted by the corresponding entry of <paramref name="weights"/>.</summary>
        /// <exception cref="ArgumentException">There is not one weight for each entry.</exception>
        public Quantity<T> Dot(ImmutableArray<double> weights) => vector.Zip(Vector.Of(weights)).Select((entry, weight) => weight * entry).Sum();
    }

    extension(Vector<IBooleanExpression> constraints) {
        /// <summary>The constraint that every entry holds.</summary>
        public IBooleanExpression AllOf() => constraints.Elements.AllOf();

        /// <summary>The constraint that some entry holds.</summary>
        public IBooleanExpression AnyOf() => constraints.Elements.AnyOf();
    }
}
