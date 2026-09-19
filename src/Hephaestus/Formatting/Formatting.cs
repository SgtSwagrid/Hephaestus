using System.Collections.Immutable;
using System.Globalization;

namespace Hephaestus;

/// <summary>
/// Renders expressions the way they were written, for logs, error messages and code review:
/// <c>(departureA + 120 &lt;= departureB) | (departureB + 120 &lt;= departureA)</c>.
/// </summary>
public static class Formatting {
    extension(ILinearExpression expression) {
        /// <summary>The expression in mathematical notation.</summary>
        public string Format() => string.Concat(WriteLinear(new LinearStep(expression, [])));
    }

    extension(IBooleanExpression expression) {
        /// <summary>The expression in the same notation it is written in: <c>&amp;</c>, <c>|</c>, <c>!</c>, <c>=&gt;</c>, <c>&lt;=&gt;</c>.</summary>
        public string Format() => string.Concat(WriteBoolean(new BooleanStep(expression, 0, [])));
    }

    extension(BinaryVariable variable) {
        /// <summary>The variable's name. (A binary variable is both kinds of expression; this settles which rendering applies.)</summary>
        public string Format() => variable.Name;
    }

    extension(AffineForm form) {
        /// <summary>The form as a sum of terms, constant last.</summary>
        public string Format() =>
            form.IsConstant
                ? Number(form.Constant)
                : string.Concat(form.Coefficients.Select((term, index) => Term(term.Value, term.Key.Name, isFirst: index == 0)))
                    + (form.Constant == 0 ? "" : Signed(form.Constant, Number(Math.Abs(form.Constant)), isFirst: false));
    }

    extension(LinearRow row) {
        /// <summary>The row as an inequality, equation or range.</summary>
        public string Format() =>
            new AffineForm(row.Coefficients, 0).Format() is var body && row.LowerBound == row.UpperBound ? $"{body} == {Number(row.UpperBound)}"
            : double.IsNegativeInfinity(row.LowerBound) ? $"{body} <= {Number(row.UpperBound)}"
            : double.IsPositiveInfinity(row.UpperBound) ? $"{body} >= {Number(row.LowerBound)}"
            : $"{Number(row.LowerBound)} <= {body} <= {Number(row.UpperBound)}";
    }

    extension(GuardedRow row) {
        /// <summary>The row as <c>guards =&gt; inequality</c>.</summary>
        public string Format() =>
            (row.Guards.IsEmpty ? "" : string.Join(" & ", row.Guards.Select(guard => (guard.IsPositive ? "" : "!") + guard.Variable.Name)) + " => ")
            + $"{new AffineForm(row.Expression.Coefficients, 0).Format()} {(row.IsEquality ? "==" : "<=")} {Number(0 - row.Expression.Constant)}";
    }

    extension(IndicatorProblem problem) {
        /// <summary>The whole problem, one row or column per line.</summary>
        public string Format() =>
            string.Join(Environment.NewLine, [
                $"{(problem.Sense == ObjectiveSense.Minimise ? "minimise" : "maximise")} {problem.Objective.Format()}",
                "subject to",
                .. problem.Rows.Select(row => $"  {row.Format()}"),
                "where",
                .. problem.Columns.Select(column => $"  {Number(column.LowerBound)} <= {column.Variable.Name} <= {Number(column.UpperBound)}, {Kind(column.Variable)}"),
            ]);
    }

    extension(MilpProblem problem) {
        /// <summary>The whole programme, one row or column per line.</summary>
        public string Format() =>
            string.Join(Environment.NewLine, [
                $"{(problem.Sense == ObjectiveSense.Minimise ? "minimise" : "maximise")} {problem.Objective.Format()}",
                "subject to",
                .. problem.Rows.Select(row => $"  {row.Format()}"),
                "where",
                .. problem.Columns.Select(column => $"  {Number(column.LowerBound)} <= {column.Variable.Name} <= {Number(column.UpperBound)}, {Kind(column.Variable)}"),
            ]);
    }

    private sealed record LinearStep(
        ILinearExpression Expression,
        ImmutableList<string> Tokens
    );

    private static ImmutableList<string> WriteLinear(LinearStep step) => DeepRecursion.Guard(WriteLinearUnguarded, step);

    private static ImmutableList<string> WriteLinearUnguarded(LinearStep step) =>
        step.Expression switch {
            Constant constant => step.Tokens.Add(Number(constant.Value)),
            IVariable variable => step.Tokens.Add(variable.Name),
            NamedTerm named => step.Tokens.Add(named.Name),
            Product { Coefficient: -1 } product => WriteOperand(product.Expression, step.Tokens.Add("-")),
            Product product => WriteOperand(product.Expression, step.Tokens.Add(Number(product.Coefficient)).Add("*")),
            Sum { Right: Product { Coefficient: -1 } subtracted } sum => WriteOperand(subtracted.Expression, WriteLinear(step with { Expression = sum.Left }).Add(" - ")),
            Sum { Right: Product { Coefficient: < 0 } subtracted } sum => WriteLinear(new LinearStep(new Product(-subtracted.Coefficient, subtracted.Expression), WriteLinear(step with { Expression = sum.Left }).Add(" - "))),
            Sum { Right: Constant { Value: < 0 } subtracted } sum => WriteLinear(step with { Expression = sum.Left }).Add(" - ").Add(Number(-subtracted.Value)),
            Sum sum => WriteLinear(new LinearStep(sum.Right, WriteLinear(step with { Expression = sum.Left }).Add(" + "))),
            Maximum maximum => WriteCall(step.Tokens.Add("max("), maximum.Left, maximum.Right),
            Minimum minimum => WriteCall(step.Tokens.Add("min("), minimum.Left, minimum.Right),
            AbsoluteValue absolute => WriteLinear(new LinearStep(absolute.Operand, step.Tokens.Add("abs("))).Add(")"),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {step.Expression.GetType().Name}."),
        };

    private static ImmutableList<string> WriteCall(ImmutableList<string> tokens, ILinearExpression left, ILinearExpression right) =>
        WriteLinear(new LinearStep(right, WriteLinear(new LinearStep(left, tokens)).Add(", "))).Add(")");

    /// <summary>Writes the operand of a product or subtraction, in brackets if it would otherwise be misread.</summary>
    private static ImmutableList<string> WriteOperand(ILinearExpression operand, ImmutableList<string> tokens) =>
        operand is Sum or Constant { Value: < 0 } or Product { Coefficient: < 0 }
            ? WriteLinear(new LinearStep(operand, tokens.Add("("))).Add(")")
            : WriteLinear(new LinearStep(operand, tokens));

    private sealed record BooleanStep(
        IBooleanExpression Expression,
        int Context,
        ImmutableList<string> Tokens
    );

    private static ImmutableList<string> WriteBoolean(BooleanStep step) => DeepRecursion.Guard(WriteBooleanUnguarded, step);

    /// <summary>Brackets go around anything that binds more loosely than its context; comparisons nested in logic always get them.</summary>
    private static ImmutableList<string> WriteBooleanUnguarded(BooleanStep step) =>
        Precedence(step.Expression) < step.Context
            ? WriteBare(step with { Tokens = step.Tokens.Add("(") }).Add(")")
            : WriteBare(step);

    private static ImmutableList<string> WriteBare(BooleanStep step) =>
        step.Expression switch {
            BooleanConstant constant => step.Tokens.Add(constant.Value ? "true" : "false"),
            BinaryVariable variable => step.Tokens.Add(variable.Name),
            NamedConstraint named => step.Tokens.Add(named.Name),
            Comparison comparison => WriteLinear(new LinearStep(comparison.Right, WriteLinear(new LinearStep(comparison.Left, step.Tokens)).Add($" {Symbol(comparison.Relation)} "))),
            Negation negation => WriteBoolean(new BooleanStep(negation.Operand, 5, step.Tokens.Add("!"))),
            Conjunction conjunction => WriteBinary(step, conjunction.Left, 4, " & ", conjunction.Right, 4),
            Disjunction disjunction => WriteBinary(step, disjunction.Left, 3, " | ", disjunction.Right, 3),
            Implication implication => WriteBinary(step, implication.Antecedent, 3, " => ", implication.Consequent, 2),
            Equivalence equivalence => WriteBinary(step, equivalence.Left, 2, " <=> ", equivalence.Right, 2),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {step.Expression.GetType().Name}."),
        };

    private static ImmutableList<string> WriteBinary(BooleanStep step, IBooleanExpression left, int leftContext, string symbol, IBooleanExpression right, int rightContext) =>
        WriteBoolean(new BooleanStep(right, rightContext, WriteBoolean(new BooleanStep(left, leftContext, step.Tokens)).Add(symbol)));

    private static int Precedence(IBooleanExpression expression) =>
        expression switch {
            Comparison => 0,
            Equivalence => 1,
            Implication => 2,
            Disjunction => 3,
            Conjunction => 4,
            Negation => 5,
            _ => 6,
        };

    private static string Symbol(Relation relation) =>
        relation switch {
            Relation.LessThan => "<",
            Relation.LessThanOrEqual => "<=",
            Relation.Equal => "==",
            Relation.NotEqual => "!=",
            Relation.GreaterThanOrEqual => ">=",
            Relation.GreaterThan => ">",
            _ => throw new NotSupportedException($"Unknown relation: {relation}."),
        };

    private static string Term(double coefficient, string name, bool isFirst) =>
        Signed(coefficient, Math.Abs(coefficient) == 1 ? name : $"{Number(Math.Abs(coefficient))}*{name}", isFirst);

    private static string Signed(double sign, string magnitude, bool isFirst) =>
        (isFirst, sign < 0) switch {
            (true, true) => $"-{magnitude}",
            (true, false) => magnitude,
            (false, true) => $" - {magnitude}",
            (false, false) => $" + {magnitude}",
        };

    private static string Kind(IVariable variable) =>
        variable switch {
            BinaryVariable => "binary",
            IntegerVariable => "integer",
            _ => "continuous",
        };

    /// <summary>Twelve significant digits: enough to tell numbers apart, few enough to hide floating-point dust.</summary>
    private static string Number(double value) =>
        double.IsPositiveInfinity(value) ? "inf"
        : double.IsNegativeInfinity(value) ? "-inf"
        : value == 0 ? "0"
        : value.ToString("G12", CultureInfo.InvariantCulture);
}
