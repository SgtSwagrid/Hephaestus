using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using Microsoft.Z3;

namespace Hephaestus.Z3;

/// <summary>
/// Solves problems with the Z3 SMT solver. Boolean structure is handed over as it stands, so there
/// are no auxiliary variables, no big-M values and no need for bounds; arithmetic is exact and
/// rational, so strict inequalities mean exactly what they say.
/// </summary>
public sealed record Z3Solver(SolverOptions? Options = null) : ISolver {
    /// <inheritdoc/>
    public ISolveResult Solve(IProblem problem, CancellationToken cancellationToken = default) {
        using var context = new Context();
        using var interruption = cancellationToken.Register(context.Interrupt);

        var symbols = new Symbols(context, problem.Variables.ToImmutableDictionary(variable => variable, variable => Declare(context, variable)));
        var objective = Linear(symbols, problem.Objective.Normalise());
        var optimiser = context.MkOptimize();
        Configure(context, optimiser, Options ?? SolverOptions.Default);
        optimiser.Assert([.. symbols.Constants.Keys.OfType<BinaryVariable>().Select(variable => Domain(symbols, variable))]);
        optimiser.Assert(Boolean(new Step(symbols, problem.Constraint)));
        var handle = problem switch {
            Minimisation => optimiser.MkMinimize(objective),
            Maximisation => optimiser.MkMaximize(objective),
            _ => null,
        };

        return optimiser.Check() switch {
            Status.SATISFIABLE => AsResult(handle?.Value.ToString() ?? "", ReadSolution(symbols, optimiser.Model, problem)),
            Status.UNSATISFIABLE => new Infeasible(),
            _ => new Unknown($"Z3 gave up: {optimiser.ReasonUnknown}."),
        };
    }

    private sealed record Symbols(
        Context Context,
        ImmutableDictionary<IVariable, ArithExpr> Constants
    );

    private sealed record Step(
        Symbols Symbols,
        IBooleanExpression Expression
    );

    private static ArithExpr Declare(Context context, IVariable variable) =>
        variable is ContinuousVariable ? context.MkRealConst(variable.Name) : context.MkIntConst(variable.Name);

    private static BoolExpr Domain(Symbols symbols, BinaryVariable variable) =>
        symbols.Context.MkAnd(
            symbols.Context.MkGe(symbols.Constants[variable], symbols.Context.MkInt(0)),
            symbols.Context.MkLe(symbols.Constants[variable], symbols.Context.MkInt(1)));

    private static void Configure(Context context, Optimize optimiser, SolverOptions options) {
        if (options.TimeLimit is { } timeLimit) {
            var parameters = context.MkParams();
            parameters.Add("timeout", (uint)timeLimit.TotalMilliseconds);
            optimiser.Parameters = parameters;
        }
    }

    private static BoolExpr Boolean(Step step) => DeepRecursion.Guard(BooleanUnguarded, step);

    private static BoolExpr BooleanUnguarded(Step step) =>
        step.Expression switch {
            BooleanConstant constant => step.Symbols.Context.MkBool(constant.Value),
            BinaryVariable variable => step.Symbols.Context.MkEq(step.Symbols.Constants[variable], step.Symbols.Context.MkInt(1)),
            Comparison comparison => Compare(step.Symbols.Context, comparison.Relation, Linear(step.Symbols, (comparison.Left - comparison.Right).Normalise())),
            Negation negation => step.Symbols.Context.MkNot(Boolean(step with { Expression = negation.Operand })),
            Conjunction conjunction => step.Symbols.Context.MkAnd(Boolean(step with { Expression = conjunction.Left }), Boolean(step with { Expression = conjunction.Right })),
            Disjunction disjunction => step.Symbols.Context.MkOr(Boolean(step with { Expression = disjunction.Left }), Boolean(step with { Expression = disjunction.Right })),
            Implication implication => step.Symbols.Context.MkImplies(Boolean(step with { Expression = implication.Antecedent }), Boolean(step with { Expression = implication.Consequent })),
            Equivalence equivalence => step.Symbols.Context.MkIff(Boolean(step with { Expression = equivalence.Left }), Boolean(step with { Expression = equivalence.Right })),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {step.Expression.GetType().Name}."),
        };

    /// <summary><c>difference ~ 0</c>.</summary>
    private static BoolExpr Compare(Context context, Relation relation, ArithExpr difference) =>
        relation switch {
            Relation.LessThan => context.MkLt(difference, context.MkReal(0)),
            Relation.LessThanOrEqual => context.MkLe(difference, context.MkReal(0)),
            Relation.Equal => context.MkEq(difference, context.MkReal(0)),
            Relation.NotEqual => context.MkNot(context.MkEq(difference, context.MkReal(0))),
            Relation.GreaterThanOrEqual => context.MkGe(difference, context.MkReal(0)),
            Relation.GreaterThan => context.MkGt(difference, context.MkReal(0)),
            _ => throw new NotSupportedException($"Unknown relation: {relation}."),
        };

    private static ArithExpr Linear(Symbols symbols, AffineForm form) =>
        symbols.Context.MkAdd([
            symbols.Context.MkReal(Rational(form.Constant)),
            .. form.Coefficients.Select(term => symbols.Context.MkMul(symbols.Context.MkReal(Rational(term.Value)), AsReal(symbols, term.Key))),
        ]);

    private static ArithExpr AsReal(Symbols symbols, IVariable variable) =>
        symbols.Constants[variable] is IntExpr whole ? symbols.Context.MkInt2Real(whole) : symbols.Constants[variable];

    /// <summary>
    /// A double as a rational literal. The shortest decimal that round-trips is what the modeller
    /// meant (0.1 is one tenth, not 3602879701896397/36028797018963968); numbers beyond the reach of
    /// <see cref="decimal"/> are converted bit for bit instead.
    /// </summary>
    private static string Rational(double value) =>
        value == 0 || Math.Abs(value) is > 1e-12 and < 1e27
            ? ((decimal)value).ToString(CultureInfo.InvariantCulture)
            : BitForBit(BitConverter.DoubleToInt64Bits(value));

    private static string BitForBit(long bits) =>
        Fraction(
            mantissa: (bits < 0 ? -1 : 1) * (((bits >> 52) & 0x7FF) == 0 ? (bits & 0xFFFFFFFFFFFFF) << 1 : (bits & 0xFFFFFFFFFFFFF) | 0x10000000000000),
            exponent: (int)((bits >> 52) & 0x7FF) - 1075);

    private static string Fraction(BigInteger mantissa, int exponent) =>
        exponent >= 0
            ? (mantissa * BigInteger.Pow(2, exponent)).ToString(CultureInfo.InvariantCulture)
            : $"{mantissa.ToString(CultureInfo.InvariantCulture)}/{BigInteger.Pow(2, -exponent).ToString(CultureInfo.InvariantCulture)}";

    private static Solution ReadSolution(Symbols symbols, Model model, IProblem problem) {
        var values = symbols.Constants.ToImmutableSortedDictionary(
            entry => entry.Key,
            entry => AsDouble(model.Eval(entry.Value, completion: true)),
            VariableOrder.Comparer);
        return new Solution(values, problem.Objective.Normalise().Evaluate(variable => values[variable]));
    }

    private static double AsDouble(Expr value) =>
        value switch {
            IntNum whole => (double)whole.BigInteger,
            RatNum ratio => (double)ratio.BigIntNumerator / (double)ratio.BigIntDenominator,
            _ => throw new NotSupportedException($"Z3 produced a value that is not a rational number: {value}."),
        };

    /// <summary>
    /// Z3 reports an objective that escapes to infinity as <c>oo</c>, and one that approaches but
    /// never attains its limit (<c>minimise x</c> subject to <c>x &gt; 5</c>) with an <c>epsilon</c>.
    /// In the latter case the model is feasible but no optimum exists.
    /// </summary>
    private static ISolveResult AsResult(string objectiveValue, Solution solution) =>
        objectiveValue.Contains("oo", StringComparison.Ordinal) ? new Unbounded()
        : objectiveValue.Contains("epsilon", StringComparison.Ordinal) ? new Feasible(solution)
        : new Optimal(solution);
}
