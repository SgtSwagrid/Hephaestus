using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>Finds the variables that expressions and problems mention.</summary>
public static class Occurrences {
    extension(ILinearExpression expression) {
        /// <summary>Every variable mentioned in this expression, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => CollectLinear(expression, Empty);
    }

    extension(IBooleanExpression expression) {
        /// <summary>Every variable mentioned in this expression, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => CollectBoolean(expression, Empty);
    }

    extension(BinaryVariable variable) {
        /// <summary>The variable itself. (A binary variable is both kinds of expression; this settles which traversal applies.)</summary>
        public ImmutableSortedSet<IVariable> Variables => Empty.Add(variable);
    }

    extension(ISingleObjectiveProblem problem) {
        /// <summary>Every variable mentioned in the problem, in the standard order.</summary>
        /// <exception cref="ModellingException">Two variables of different kinds share a name.</exception>
        public ImmutableSortedSet<IVariable> Variables => DistinctlyNamed(problem.Constraint.Variables.Union(problem.Objective.Expression.Variables));
    }

    private static ImmutableSortedSet<IVariable> Empty { get; } = ImmutableSortedSet.Create(VariableOrder.Comparer);

    private static ImmutableSortedSet<IVariable> CollectLinear(ILinearExpression expression, ImmutableSortedSet<IVariable> found) =>
        DeepRecursion.Guard(CollectLinearUnguarded, expression, found);

    private static ImmutableSortedSet<IVariable> CollectLinearUnguarded(ILinearExpression expression, ImmutableSortedSet<IVariable> found) =>
        expression switch {
            Constant => found,
            IVariable variable => found.Add(variable),
            Product product => CollectLinear(product.Expression, found),
            Sum sum => CollectLinear(sum.Right, CollectLinear(sum.Left, found)),
            Maximum maximum => CollectLinear(maximum.Right, CollectLinear(maximum.Left, found)),
            Minimum minimum => CollectLinear(minimum.Right, CollectLinear(minimum.Left, found)),
            AbsoluteValue absolute => CollectLinear(absolute.Operand, found),
            NamedTerm named => CollectLinear(named.Expression, found),
            Conditional conditional => CollectLinear(conditional.Otherwise, CollectLinear(conditional.Then, CollectBoolean(conditional.Condition, found))),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {expression.GetType().Name}."),
        };

    private static ImmutableSortedSet<IVariable> CollectBoolean(IBooleanExpression expression, ImmutableSortedSet<IVariable> found) =>
        DeepRecursion.Guard(CollectBooleanUnguarded, expression, found);

    private static ImmutableSortedSet<IVariable> CollectBooleanUnguarded(IBooleanExpression expression, ImmutableSortedSet<IVariable> found) =>
        expression switch {
            BooleanConstant => found,
            BinaryVariable variable => found.Add(variable),
            Comparison comparison => CollectLinear(comparison.Right, CollectLinear(comparison.Left, found)),
            Negation negation => CollectBoolean(negation.Operand, found),
            NamedConstraint named => CollectBoolean(named.Expression, found),
            Conjunction conjunction => CollectBoth(conjunction.Left, conjunction.Right, found),
            Disjunction disjunction => CollectBoth(disjunction.Left, disjunction.Right, found),
            Implication implication => CollectBoth(implication.Antecedent, implication.Consequent, found),
            Equivalence equivalence => CollectBoth(equivalence.Left, equivalence.Right, found),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };

    private static ImmutableSortedSet<IVariable> CollectBoth(IBooleanExpression left, IBooleanExpression right, ImmutableSortedSet<IVariable> found) =>
        CollectBoolean(right, CollectBoolean(left, found));

    private static ImmutableSortedSet<IVariable> DistinctlyNamed(ImmutableSortedSet<IVariable> variables) =>
        variables.GroupBy(variable => variable.Name).FirstOrDefault(group => group.Count() > 1) is { } clash
            ? throw new ModellingException($"The name '{clash.Key}' is used by variables of different kinds: {string.Join(", ", clash.Select(variable => variable.GetType().Name))}.")
            : variables;
}
