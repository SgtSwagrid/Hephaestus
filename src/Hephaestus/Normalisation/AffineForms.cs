namespace Hephaestus;

/// <summary>Arithmetic and analysis over <see cref="AffineForm"/> and <see cref="Interval"/>.</summary>
public static class AffineForms {
    extension(AffineForm form) {
        /// <summary>Whether the form has no variables.</summary>
        public bool IsConstant => form.Coefficients.IsEmpty;

        /// <summary>
        /// Whether the form can only take whole-number values: whole coefficients on integral
        /// variables and a whole constant.
        /// </summary>
        public bool IsIntegral =>
            IsWhole(form.Constant) && form.Coefficients.All(term => term.Key.IsIntegral && IsWhole(term.Value));

        /// <summary>This form with <paramref name="coefficient"/> &#183; <paramref name="variable"/> added.</summary>
        public AffineForm PlusTerm(IVariable variable, double coefficient) =>
            form.Coefficients.GetValueOrDefault(variable) + coefficient is var total && total == 0
                ? form with { Coefficients = form.Coefficients.Remove(variable) }
                : form with { Coefficients = form.Coefficients.SetItem(variable, total) };

        /// <summary>This form shifted by a constant.</summary>
        public AffineForm Plus(double constant) => form with { Constant = form.Constant + constant };

        /// <summary>The sum of two forms.</summary>
        public AffineForm Plus(AffineForm other) =>
            other.Coefficients.Aggregate(form.Plus(other.Constant), (sum, term) => sum.PlusTerm(term.Key, term.Value));

        /// <summary>This form scaled by a factor.</summary>
        public AffineForm Times(double factor) =>
            factor == 0
                ? AffineForm.Zero
                : new AffineForm(
                    form.Coefficients.SetItems(form.Coefficients.Select(term => KeyValuePair.Create(term.Key, term.Value * factor))),
                    form.Constant * factor);

        /// <summary>This form with every sign flipped.</summary>
        public AffineForm Negated => form.Times(-1);

        /// <summary>The value of the form under an assignment of its variables.</summary>
        public double Evaluate(Func<IVariable, double> valueOf) =>
            form.Coefficients.Aggregate(form.Constant, (total, term) => total + term.Value * valueOf(term.Key));

        /// <summary>The tightest interval containing every value of the form, given an interval for each variable.</summary>
        public Interval Range(Func<IVariable, Interval> boundsOf) =>
            form.Coefficients.Aggregate(
                new Interval(form.Constant, form.Constant),
                (range, term) => range.Plus(boundsOf(term.Key).Times(term.Value)));
    }

    extension(Interval interval) {
        /// <summary>Whether the interval contains no numbers at all.</summary>
        public bool IsEmpty => interval.Lower > interval.Upper;

        /// <summary>The interval of sums of a number from each interval.</summary>
        public Interval Plus(Interval other) => new(interval.Lower + other.Lower, interval.Upper + other.Upper);

        /// <summary>The interval scaled by a non-zero factor.</summary>
        public Interval Times(double factor) =>
            factor >= 0
                ? new Interval(interval.Lower * factor, interval.Upper * factor)
                : new Interval(interval.Upper * factor, interval.Lower * factor);

        /// <summary>The numbers common to both intervals.</summary>
        public Interval Intersect(Interval other) => new(Math.Max(interval.Lower, other.Lower), Math.Min(interval.Upper, other.Upper));
    }

    extension(IVariable variable) {
        /// <summary>Whether the variable is restricted to whole numbers.</summary>
        public bool IsIntegral => variable is IntegerVariable or BinaryVariable;

        /// <summary>The bounds a variable has by virtue of its kind alone.</summary>
        public Interval IntrinsicBounds => variable is BinaryVariable ? Interval.Unit : Interval.Unbounded;
    }

    private static bool IsWhole(double value) => Math.Floor(value) == value;
}
