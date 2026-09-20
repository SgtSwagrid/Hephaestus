namespace Hephaestus;

/// <summary>Reads expressions off a solution. Any expression can be read, not just variables.</summary>
public static class Evaluation {
    /// <summary>
    /// The violation a comparison is forgiven when a constraint is read without one being named,
    /// since solvers work in floating point.
    /// </summary>
    public const double Tolerance = 1e-6;

    /// <summary>
    /// The decimal places, in units of the projection, that a number is rounded to before being
    /// decoded. Solvers are accurate to about a millionth of a unit, so without this a start at 240
    /// seconds reads as 08:03:59.99999999; read the underlying expression for the unrounded number.
    /// </summary>
    public const int DecimalPlaces = 5;

    extension(Solution solution) {
        /// <summary>
        /// The value of any expression under this solution, in whatever type it reads as: a number
        /// for a linear expression, a truth for a constraint, and its own type for anything read
        /// through a projection. Any expression can be read, not just variables.
        /// </summary>
        /// <exception cref="KeyNotFoundException">The expression mentions a variable the solved problem did not.</exception>
        public TValue Value<TValue>(IReadableExpression<TValue> expression) => expression.Read(solution);

        /// <summary>Whether a binary variable is set. (Read it as a number with <c>Value((ILinearExpression)variable)</c>, since it is both.)</summary>
        public bool Value(BinaryVariable variable) => solution.ValueOf(variable) > 0.5;

        /// <summary>
        /// Whether a boolean expression holds under this solution, forgiving comparisons violated by
        /// no more than <paramref name="tolerance"/>. Reading one without saying forgives
        /// <see cref="Tolerance"/>, which is nearly always what is wanted.
        /// </summary>
        public bool Value(IBooleanExpression expression, double tolerance) => Holds(solution, expression, tolerance);

        /// <summary>This solution with a value for one more variable.</summary>
        public Solution With(IVariable variable, double value) => solution with { Values = solution.Values.SetItem(variable, value) };

        /// <summary>This solution with a binary variable set or not.</summary>
        public Solution With(BinaryVariable variable, bool value) => solution.With(variable, value ? 1 : 0);

        /// <summary>This solution with a value for a typed variable.</summary>
        /// <exception cref="ArgumentException">The variable is a compound expression rather than a variable, so no one value can be given to it.</exception>
        public Solution With<TValue>(ILinearlyEncodable<TValue> variable, TValue value) => solution.With(variable.Expression, variable.Projection.Encode(value));

        private Solution With(ILinearExpression expression, double value) =>
            expression is IVariable variable
                ? solution.With(variable, value)
                : throw new ArgumentException($"A starting value can be given to a variable, but '{expression.Format()}' is a compound expression.", nameof(expression));

        private double ValueOf(IVariable variable) =>
            solution.Values.TryGetValue(variable, out var value)
                ? value
                : throw new KeyNotFoundException($"The solution has no value for '{variable.Name}': the variable does not occur in the problem that was solved.");
    }

    internal static double Evaluate(Solution solution, ILinearExpression expression) => DeepRecursion.Guard(EvaluateUnguarded, solution, expression);

    private static double EvaluateUnguarded(Solution solution, ILinearExpression expression) =>
        expression switch {
            Constant constant => constant.Value,
            IVariable variable => solution.ValueOf(variable),
            Product product => product.Coefficient * Evaluate(solution, product.Expression),
            NamedTerm named => Evaluate(solution, named.Expression),
            Sum sum => Evaluate(solution, sum.Left) + Evaluate(solution, sum.Right),
            Maximum maximum => Math.Max(Evaluate(solution, maximum.Left), Evaluate(solution, maximum.Right)),
            Minimum minimum => Math.Min(Evaluate(solution, minimum.Left), Evaluate(solution, minimum.Right)),
            AbsoluteValue absolute => Math.Abs(Evaluate(solution, absolute.Operand)),
            Conditional conditional => Evaluate(solution, solution.Value(conditional.Condition) ? conditional.Then : conditional.Otherwise),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {expression.GetType().Name}."),
        };

    internal static bool Holds(Solution solution, IBooleanExpression expression, double tolerance) =>
        DeepRecursion.Guard(HoldsUnguarded, solution, expression, tolerance);

    private static bool HoldsUnguarded(Solution solution, IBooleanExpression expression, double tolerance) =>
        expression switch {
            BooleanConstant constant => constant.Value,
            BinaryVariable variable => solution.Value(variable),
            Comparison comparison => BooleanNormalisation.Holds(comparison.Relation, solution.Value(comparison.Left - comparison.Right), tolerance),
            Negation negation => !Holds(solution, negation.Operand, tolerance),
            NamedConstraint named => Holds(solution, named.Expression, tolerance),
            Conjunction conjunction => Holds(solution, conjunction.Left, tolerance) && Holds(solution, conjunction.Right, tolerance),
            Disjunction disjunction => Holds(solution, disjunction.Left, tolerance) || Holds(solution, disjunction.Right, tolerance),
            Implication implication => !Holds(solution, implication.Antecedent, tolerance) || Holds(solution, implication.Consequent, tolerance),
            Equivalence equivalence => Holds(solution, equivalence.Left, tolerance) == Holds(solution, equivalence.Right, tolerance),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };
}
