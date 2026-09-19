using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>Finds the variables that expressions and problems mention.</summary>
public static class Occurrences {
    extension(ILinearExpression expression) {
        /// <summary>Every variable mentioned in this expression, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => CollectLinear(new LinearStep(expression, Empty));
    }

    extension(IBooleanExpression expression) {
        /// <summary>Every variable mentioned in this expression, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => CollectBoolean(new BooleanStep(expression, Empty));
    }

    extension(BinaryVariable variable) {
        /// <summary>The variable itself. (A binary variable is both kinds of expression; this settles which traversal applies.)</summary>
        public ImmutableSortedSet<IVariable> Variables => Empty.Add(variable);
    }

    extension(IProblem problem) {
        /// <summary>Every variable mentioned in the problem, in the standard order.</summary>
        /// <exception cref="ModellingException">Two variables of different kinds share a name.</exception>
        public ImmutableSortedSet<IVariable> Variables => DistinctlyNamed(problem.Constraint.Variables.Union(problem.Objective.Variables));
    }

    private static ImmutableSortedSet<IVariable> Empty { get; } = ImmutableSortedSet.Create(VariableOrder.Comparer);

    private sealed record LinearStep(
        ILinearExpression Expression,
        ImmutableSortedSet<IVariable> Found
    );

    private sealed record BooleanStep(
        IBooleanExpression Expression,
        ImmutableSortedSet<IVariable> Found
    );

    private static ImmutableSortedSet<IVariable> CollectLinear(LinearStep step) => DeepRecursion.Guard(CollectLinearUnguarded, step);

    private static ImmutableSortedSet<IVariable> CollectLinearUnguarded(LinearStep step) =>
        step.Expression switch {
            Constant => step.Found,
            IVariable variable => step.Found.Add(variable),
            Product product => CollectLinear(step with { Expression = product.Expression }),
            Sum sum => CollectLinear(new LinearStep(sum.Right, CollectLinear(step with { Expression = sum.Left }))),
            Maximum maximum => CollectLinear(new LinearStep(maximum.Right, CollectLinear(step with { Expression = maximum.Left }))),
            Minimum minimum => CollectLinear(new LinearStep(minimum.Right, CollectLinear(step with { Expression = minimum.Left }))),
            AbsoluteValue absolute => CollectLinear(step with { Expression = absolute.Operand }),
            NamedTerm named => CollectLinear(step with { Expression = named.Expression }),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {step.Expression.GetType().Name}."),
        };

    private static ImmutableSortedSet<IVariable> CollectBoolean(BooleanStep step) => DeepRecursion.Guard(CollectBooleanUnguarded, step);

    private static ImmutableSortedSet<IVariable> CollectBooleanUnguarded(BooleanStep step) =>
        step.Expression switch {
            BooleanConstant => step.Found,
            BinaryVariable variable => step.Found.Add(variable),
            Comparison comparison => CollectLinear(new LinearStep(comparison.Right, CollectLinear(new LinearStep(comparison.Left, step.Found)))),
            Negation negation => CollectBoolean(step with { Expression = negation.Operand }),
            NamedConstraint named => CollectBoolean(step with { Expression = named.Expression }),
            Conjunction conjunction => CollectBoth(step, conjunction.Left, conjunction.Right),
            Disjunction disjunction => CollectBoth(step, disjunction.Left, disjunction.Right),
            Implication implication => CollectBoth(step, implication.Antecedent, implication.Consequent),
            Equivalence equivalence => CollectBoth(step, equivalence.Left, equivalence.Right),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {step.Expression.GetType().Name}."),
        };

    private static ImmutableSortedSet<IVariable> CollectBoth(BooleanStep step, IBooleanExpression left, IBooleanExpression right) =>
        CollectBoolean(new BooleanStep(right, CollectBoolean(step with { Expression = left })));

    private static ImmutableSortedSet<IVariable> DistinctlyNamed(ImmutableSortedSet<IVariable> variables) =>
        variables.GroupBy(variable => variable.Name).FirstOrDefault(group => group.Count() > 1) is { } clash
            ? throw new ModellingException($"The name '{clash.Key}' is used by variables of different kinds: {string.Join(", ", clash.Select(variable => variable.GetType().Name))}.")
            : variables;
}
