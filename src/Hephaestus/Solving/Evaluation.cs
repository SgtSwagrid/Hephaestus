namespace Hephaestus;

/// <summary>Reads expressions off a solution. Any expression can be read, not just variables.</summary>
public static class Evaluation {
    extension(Solution solution) {
        /// <summary>The value of a linear expression under this solution.</summary>
        /// <exception cref="KeyNotFoundException">The expression mentions a variable the solved problem did not.</exception>
        public double Value(ILinearExpression expression) => expression.Normalise().Evaluate(solution.ValueOf);

        /// <summary>Whether a binary variable is set. (Read it as a number with <c>Value((ILinearExpression)variable)</c>.)</summary>
        public bool Value(BinaryVariable variable) => solution.ValueOf(variable) > 0.5;

        /// <summary>
        /// Whether a boolean expression holds under this solution. Comparisons are forgiven
        /// violations up to <paramref name="tolerance"/>, since solvers work in floating point.
        /// </summary>
        public bool Value(IBooleanExpression expression, double tolerance = 1e-6) => Holds(new Step(solution, expression, tolerance));

        /// <summary>The value of a typed quantity under this solution.</summary>
        /// <param name="quantity">The quantity to read.</param>
        /// <param name="decimalPlaces">
        /// The number of decimal places, in units of the projection, to which the underlying number is
        /// rounded before decoding. Solvers are accurate to about a millionth of a unit, so without
        /// this a departure at 240 seconds reads as 08:03:59.99999999. The unrounded number is
        /// <c>solution.Value(quantity.Expression)</c>.
        /// </param>
        public T Value<T>(Quantity<T> quantity, int decimalPlaces = 5) =>
            quantity.Projection.Decode(Math.Round(solution.Value(quantity.Expression), decimalPlaces));

        /// <summary>The value of a typed point under this solution.</summary>
        /// <param name="point">The point to read.</param>
        /// <param name="decimalPlaces"><inheritdoc cref="Value{T}(Solution, Quantity{T}, int)" path="/param[@name='decimalPlaces']"/></param>
        public T Value<T, TDelta>(Point<T, TDelta> point, int decimalPlaces = 5) =>
            point.Projection.Decode(Math.Round(solution.Value(point.Expression), decimalPlaces));

        private double ValueOf(IVariable variable) =>
            solution.Values.TryGetValue(variable, out var value)
                ? value
                : throw new KeyNotFoundException($"The solution has no value for '{variable.Name}': the variable does not occur in the problem that was solved.");
    }

    private sealed record Step(
        Solution Solution,
        IBooleanExpression Expression,
        double Tolerance
    );

    private static bool Holds(Step step) => DeepRecursion.Guard(HoldsUnguarded, step);

    private static bool HoldsUnguarded(Step step) =>
        step.Expression switch {
            BooleanConstant constant => constant.Value,
            BinaryVariable variable => step.Solution.Value(variable),
            Comparison comparison => BooleanNormalisation.Holds(comparison.Relation, step.Solution.Value(comparison.Left - comparison.Right), step.Tolerance),
            Negation negation => !Holds(step with { Expression = negation.Operand }),
            Conjunction conjunction => Holds(step with { Expression = conjunction.Left }) && Holds(step with { Expression = conjunction.Right }),
            Disjunction disjunction => Holds(step with { Expression = disjunction.Left }) || Holds(step with { Expression = disjunction.Right }),
            Implication implication => !Holds(step with { Expression = implication.Antecedent }) || Holds(step with { Expression = implication.Consequent }),
            Equivalence equivalence => Holds(step with { Expression = equivalence.Left }) == Holds(step with { Expression = equivalence.Right }),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {step.Expression.GetType().Name}."),
        };
}
