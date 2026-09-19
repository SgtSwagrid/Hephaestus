namespace Hephaestus;

/// <summary>Reads expressions off a solution. Any expression can be read, not just variables.</summary>
public static class Evaluation {
    extension(Solution solution) {
        /// <summary>The value of a linear expression under this solution.</summary>
        /// <exception cref="KeyNotFoundException">The expression mentions a variable the solved problem did not.</exception>
        public double Value(ILinearExpression expression) => Evaluate(new LinearStep(solution, expression));

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

        /// <summary>This solution with a value for one more variable.</summary>
        public Solution With(IVariable variable, double value) => solution with { Values = solution.Values.SetItem(variable, value) };

        /// <summary>This solution with a binary variable set or not.</summary>
        public Solution With(BinaryVariable variable, bool value) => solution.With(variable, value ? 1 : 0);

        /// <summary>This solution with a value for a typed variable.</summary>
        /// <exception cref="ArgumentException">The quantity is a compound expression rather than a variable, so no one value can be given to it.</exception>
        public Solution With<T>(Quantity<T> variable, T value) => solution.With(variable.Expression, variable.Projection.Encode(value));

        /// <inheritdoc cref="With{T}(Solution, Quantity{T}, T)"/>
        public Solution With<T, TDelta>(Point<T, TDelta> variable, T value) => solution.With(variable.Expression, variable.Projection.Encode(value));

        private Solution With(ILinearExpression expression, double value) =>
            expression is IVariable variable
                ? solution.With(variable, value)
                : throw new ArgumentException($"A starting value can be given to a variable, but '{expression.Format()}' is a compound expression.", nameof(expression));

        private double ValueOf(IVariable variable) =>
            solution.Values.TryGetValue(variable, out var value)
                ? value
                : throw new KeyNotFoundException($"The solution has no value for '{variable.Name}': the variable does not occur in the problem that was solved.");
    }

    private sealed record LinearStep(
        Solution Solution,
        ILinearExpression Expression
    );

    private static double Evaluate(LinearStep step) => DeepRecursion.Guard(EvaluateUnguarded, step);

    private static double EvaluateUnguarded(LinearStep step) =>
        step.Expression switch {
            Constant constant => constant.Value,
            IVariable variable => step.Solution.ValueOf(variable),
            Product product => product.Coefficient * Evaluate(step with { Expression = product.Expression }),
            NamedTerm named => Evaluate(step with { Expression = named.Expression }),
            Sum sum => Evaluate(step with { Expression = sum.Left }) + Evaluate(step with { Expression = sum.Right }),
            Maximum maximum => Math.Max(Evaluate(step with { Expression = maximum.Left }), Evaluate(step with { Expression = maximum.Right })),
            Minimum minimum => Math.Min(Evaluate(step with { Expression = minimum.Left }), Evaluate(step with { Expression = minimum.Right })),
            AbsoluteValue absolute => Math.Abs(Evaluate(step with { Expression = absolute.Operand })),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {step.Expression.GetType().Name}."),
        };

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
            NamedConstraint named => Holds(step with { Expression = named.Expression }),
            Conjunction conjunction => Holds(step with { Expression = conjunction.Left }) && Holds(step with { Expression = conjunction.Right }),
            Disjunction disjunction => Holds(step with { Expression = disjunction.Left }) || Holds(step with { Expression = disjunction.Right }),
            Implication implication => !Holds(step with { Expression = implication.Antecedent }) || Holds(step with { Expression = implication.Consequent }),
            Equivalence equivalence => Holds(step with { Expression = equivalence.Left }) == Holds(step with { Expression = equivalence.Right }),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {step.Expression.GetType().Name}."),
        };
}
