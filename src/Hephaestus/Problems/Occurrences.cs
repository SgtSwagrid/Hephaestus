using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>Finds the variables that expressions and problems mention.</summary>
public static class Occurrences {
    extension(ILinearExpression expression) {
        /// <summary>Every variable mentioned in this expression, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => CollectLinear(expression, Empty);
    }

    extension<TTheory>(IBooleanExpression<TTheory> expression) {
        /// <summary>Every variable mentioned in this expression, in the standard order.</summary>
        public ImmutableSortedSet<IVariable> Variables => CollectBoolean(expression, Empty);
    }

    extension(IOneShotProblem problem) {
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
            INumericVariable variable => found.Add(variable),
            Indicator indicator => CollectBoolean(indicator.Condition, found),
            Product product => CollectLinear(product.Expression, found),
            Sum sum => CollectLinear(sum.Right, CollectLinear(sum.Left, found)),
            Maximum maximum => CollectLinear(maximum.Right, CollectLinear(maximum.Left, found)),
            Minimum minimum => CollectLinear(minimum.Right, CollectLinear(minimum.Left, found)),
            AbsoluteValue absolute => CollectLinear(absolute.Operand, found),
            NamedTerm named => CollectLinear(named.Expression, found),
            Conditional conditional => CollectLinear(conditional.Otherwise, CollectLinear(conditional.Then, CollectBoolean(conditional.Condition, found))),
            _ => throw new NotSupportedException($"Unknown kind of linear expression: {expression.GetType().Name}."),
        };

    private static ImmutableSortedSet<IVariable> CollectBoolean<TTheory>(IBooleanExpression<TTheory> expression, ImmutableSortedSet<IVariable> found) =>
        DeepRecursion.Guard(CollectBooleanUnguarded, expression, found);

    private static ImmutableSortedSet<IVariable> CollectBooleanUnguarded<TTheory>(IBooleanExpression<TTheory> expression, ImmutableSortedSet<IVariable> found) =>
        expression switch {
            BooleanConstant => found,
            BinaryVariable variable => found.Add(variable),
            LinearRelation relation => CollectLinear(relation.Right, CollectLinear(relation.Left, found)),
            INegation<TTheory> negation => CollectBoolean(negation.Operand, found),
            INamedConstraint<TTheory> named => CollectBoolean(named.Expression, found),
            IConjunction<TTheory> conjunction => CollectBoth(conjunction.Left, conjunction.Right, found),
            IDisjunction<TTheory> disjunction => CollectBoth(disjunction.Left, disjunction.Right, found),
            IImplication<TTheory> implication => CollectBoth(implication.Antecedent, implication.Consequent, found),
            IEquivalence<TTheory> equivalence => CollectBoth(equivalence.Left, equivalence.Right, found),
            _ => throw new NotSupportedException($"Unknown kind of boolean expression: {expression.GetType().Name}."),
        };

    private static ImmutableSortedSet<IVariable> CollectBoth<TTheory>(IBooleanExpression<TTheory> left, IBooleanExpression<TTheory> right, ImmutableSortedSet<IVariable> found) =>
        CollectBoolean(right, CollectBoolean(left, found));

    private static ImmutableSortedSet<IVariable> DistinctlyNamed(ImmutableSortedSet<IVariable> variables) =>
        variables.GroupBy(variable => variable.Name).FirstOrDefault(group => group.Count() > 1) is { } clash
            ? throw new ModellingException($"The name '{clash.Key}' is used by variables of different kinds: {string.Join(", ", clash.Select(variable => variable.GetType().Name))}.")
            : variables;
}
