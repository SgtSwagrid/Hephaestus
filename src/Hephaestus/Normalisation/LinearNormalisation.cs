namespace Hephaestus;

/// <summary>The pass that flattens an as-written linear expression into its <see cref="AffineForm"/>.</summary>
public static class LinearNormalisation {
    extension(ILinearExpression expression) {
        /// <summary>The normal form of this expression.</summary>
        /// <exception cref="ModellingException">The expression contains a NaN or infinite number, or a piecewise-linear function, which has no affine form.</exception>
        public AffineForm Normalise() => Checked(expression, Accumulate(new Step(expression, 1, AffineForm.Zero)));
    }

    private sealed record Step(
        ILinearExpression Expression,
        double Scale,
        AffineForm Into
    );

    private static AffineForm Accumulate(Step step) => DeepRecursion.Guard(AccumulateUnguarded, step);

    private static AffineForm AccumulateUnguarded(Step step) =>
        step.Expression switch {
            Constant constant => step.Into.Plus(step.Scale * constant.Value),
            IVariable variable => step.Into.PlusTerm(variable, step.Scale),
            Product product => Accumulate(step with { Expression = product.Expression, Scale = step.Scale * product.Coefficient }),
            NamedTerm named => Accumulate(step with { Expression = named.Expression }),
            Sum sum => Accumulate(step with { Expression = sum.Right, Into = Accumulate(step with { Expression = sum.Left }) }),
            Maximum or Minimum or AbsoluteValue => throw new ModellingException($"The expression '{step.Expression.Format()}' is piecewise linear, so it has no affine form of its own. It is lowered to linear form when the problem that contains it is encoded."),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {step.Expression.GetType().Name}."),
        };

    private static AffineForm Checked(ILinearExpression expression, AffineForm form) =>
        double.IsFinite(form.Constant) && form.Coefficients.Values.All(double.IsFinite)
            ? form
            : throw new ModellingException($"The expression '{expression.Format()}' contains a NaN or infinite number (a division by zero, perhaps).");
}
