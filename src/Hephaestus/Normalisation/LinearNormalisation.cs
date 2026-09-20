namespace Hephaestus;

/// <summary>The pass that flattens an as-written linear expression into its <see cref="AffineForm"/>.</summary>
public static class LinearNormalisation {
    extension(ILinearExpression expression) {
        /// <summary>The normal form of this expression.</summary>
        /// <exception cref="ModellingException">The expression contains a NaN or infinite number, or a piecewise-linear function, which has no affine form.</exception>
        public AffineForm Normalise() => Checked(expression, Accumulate(expression, 1, AffineForm.Zero));
    }

    private static AffineForm Accumulate(ILinearExpression expression, double scale, AffineForm into) =>
        DeepRecursion.Guard(AccumulateUnguarded, expression, scale, into);

    private static AffineForm AccumulateUnguarded(ILinearExpression expression, double scale, AffineForm into) =>
        expression switch {
            Constant constant => into.Plus(scale * constant.Value),
            IVariable variable => into.PlusTerm(variable, scale),
            Product product => Accumulate(product.Expression, scale * product.Coefficient, into),
            NamedTerm named => Accumulate(named.Expression, scale, into),
            Sum sum => Accumulate(sum.Right, scale, Accumulate(sum.Left, scale, into)),
            Maximum or Minimum or AbsoluteValue or Conditional => throw new ModellingException($"The expression '{expression.Format()}' is piecewise linear, so it has no affine form of its own. It is lowered to linear form when the problem that contains it is encoded."),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {expression.GetType().Name}."),
        };

    private static AffineForm Checked(ILinearExpression expression, AffineForm form) =>
        double.IsFinite(form.Constant) && form.Coefficients.Values.All(double.IsFinite)
            ? form
            : throw new ModellingException($"The expression '{expression.Format()}' contains a NaN or infinite number (a division by zero, perhaps).");
}
