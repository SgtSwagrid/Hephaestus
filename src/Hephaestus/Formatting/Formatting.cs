using System.Collections.Immutable;
using System.Globalization;

namespace Hephaestus;

/// <summary>
/// Renders expressions the way they were written, for logs, error messages and code review:
/// <c>(startA + 120 &lt;= startB) | (startB + 120 &lt;= startA)</c>.
/// </summary>
public static class Formatting {
    extension(ILinearExpression expression) {
        /// <summary>The expression in mathematical notation.</summary>
        public string Format() => string.Concat(WriteLinear(expression, []));
    }

    extension(IBooleanExpression expression) {
        /// <summary>The expression in the same notation it is written in: <c>&amp;</c>, <c>|</c>, <c>!</c>, <c>=&gt;</c>, <c>&lt;=&gt;</c>.</summary>
        public string Format() => string.Concat(WriteBoolean(expression, 0, []));
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
        public string Format() => Write(problem, problem.Rows.Select(row => row.Format()));
    }

    extension(MilpProblem problem) {
        /// <summary>The whole programme, one row or column per line.</summary>
        public string Format() => Write(problem, problem.Rows.Select(row => row.Format()));
    }

    /// <summary>A problem in either of the forms a backend takes; only the way a row is written tells them apart.</summary>
    private static string Write(ILoweredProblem problem, IEnumerable<string> rows) =>
        string.Join(Environment.NewLine, [
            $"{(problem.Sense == ObjectiveSense.Minimise ? "minimise" : "maximise")} {problem.Objective.Format()}",
            "subject to",
            .. rows.Select(row => $"  {row}"),
            "where",
            .. problem.Columns.Select(column => $"  {Number(column.LowerBound)} <= {column.Variable.Name} <= {Number(column.UpperBound)}, {Kind(column.Variable)}"),
        ]);

    private static ImmutableList<string> WriteLinear(ILinearExpression expression, ImmutableList<string> tokens) =>
        DeepRecursion.Guard(WriteLinearUnguarded, expression, tokens);

    private static ImmutableList<string> WriteLinearUnguarded(ILinearExpression expression, ImmutableList<string> tokens) =>
        expression switch {
            Constant constant => tokens.Add(Number(constant.Value)),
            IVariable variable => tokens.Add(variable.Name),
            NamedTerm named => tokens.Add(named.Name),
            Product { Coefficient: -1 } product => WriteOperand(product.Expression, tokens.Add("-")),
            Product product => WriteOperand(product.Expression, tokens.Add(Number(product.Coefficient)).Add("*")),
            Sum { Right: Product { Coefficient: -1 } subtracted } sum => WriteOperand(subtracted.Expression, WriteLinear(sum.Left, tokens).Add(" - ")),
            Sum { Right: Product { Coefficient: < 0 } subtracted } sum => WriteLinear(new Product(-subtracted.Coefficient, subtracted.Expression), WriteLinear(sum.Left, tokens).Add(" - ")),
            Sum { Right: Constant { Value: < 0 } subtracted } sum => WriteLinear(sum.Left, tokens).Add(" - ").Add(Number(-subtracted.Value)),
            Sum sum => WriteLinear(sum.Right, WriteLinear(sum.Left, tokens).Add(" + ")),
            Maximum maximum => WriteCall(tokens.Add("max("), maximum.Left, maximum.Right),
            Minimum minimum => WriteCall(tokens.Add("min("), minimum.Left, minimum.Right),
            AbsoluteValue absolute => WriteLinear(absolute.Operand, tokens.Add("abs(")).Add(")"),
            Conditional conditional => WriteCall(WriteBoolean(conditional.Condition, 0, tokens.Add("if(")).Add(", "), conditional.Then, conditional.Otherwise),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {expression.GetType().Name}."),
        };

    private static ImmutableList<string> WriteCall(ImmutableList<string> tokens, ILinearExpression left, ILinearExpression right) =>
        WriteLinear(right, WriteLinear(left, tokens).Add(", ")).Add(")");

    /// <summary>Writes the operand of a product or subtraction, in brackets if it would otherwise be misread.</summary>
    private static ImmutableList<string> WriteOperand(ILinearExpression operand, ImmutableList<string> tokens) =>
        operand is Sum or Constant { Value: < 0 } or Product { Coefficient: < 0 }
            ? WriteLinear(operand, tokens.Add("(")).Add(")")
            : WriteLinear(operand, tokens);

    private static ImmutableList<string> WriteBoolean(IBooleanExpression expression, int context, ImmutableList<string> tokens) =>
        DeepRecursion.Guard(WriteBooleanUnguarded, expression, context, tokens);

    /// <summary>Brackets go around anything that binds more loosely than its context; comparisons nested in logic always get them.</summary>
    private static ImmutableList<string> WriteBooleanUnguarded(IBooleanExpression expression, int context, ImmutableList<string> tokens) =>
        Precedence(expression) < context
            ? WriteBare(expression, tokens.Add("(")).Add(")")
            : WriteBare(expression, tokens);

    private static ImmutableList<string> WriteBare(IBooleanExpression expression, ImmutableList<string> tokens) =>
        expression switch {
            BooleanConstant constant => tokens.Add(constant.Value ? "true" : "false"),
            BinaryVariable variable => tokens.Add(variable.Name),
            NamedConstraint named => tokens.Add(named.Name),
            Comparison comparison => WriteLinear(comparison.Right, WriteLinear(comparison.Left, tokens).Add($" {Symbol(comparison.Relation)} ")),
            Negation negation => WriteBoolean(negation.Operand, 5, tokens.Add("!")),
            Conjunction conjunction => WriteBinary(conjunction.Left, 4, " & ", conjunction.Right, 4, tokens),
            Disjunction disjunction => WriteBinary(disjunction.Left, 3, " | ", disjunction.Right, 3, tokens),
            Implication implication => WriteBinary(implication.Antecedent, 3, " => ", implication.Consequent, 2, tokens),
            Equivalence equivalence => WriteBinary(equivalence.Left, 2, " <=> ", equivalence.Right, 2, tokens),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };

    private static ImmutableList<string> WriteBinary(IBooleanExpression left, int leftContext, string symbol, IBooleanExpression right, int rightContext, ImmutableList<string> tokens) =>
        WriteBoolean(right, rightContext, WriteBoolean(left, leftContext, tokens).Add(symbol));

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
