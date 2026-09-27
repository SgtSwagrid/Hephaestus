namespace Hephaestus;

/// <summary>
/// A decision variable: a name and a kind, nothing more. Variables are values, so two variables of
/// the same kind and name are the same variable. Bounds are not part of a variable; they are
/// ordinary constraints (<c>0 &lt;= x &amp; x &lt;= 10</c>), which the encoder recognises and passes
/// to the solver as native bounds.
/// </summary>
public interface IVariable {
    /// <summary>The name that identifies the variable within a problem.</summary>
    string Name { get; }
}

/// <summary>A variable that is a number, and so a linear expression: a <see cref="ContinuousVariable"/> or an <see cref="IntegerVariable"/>.</summary>
public interface INumericVariable : IVariable, ILinearExpression;

/// <summary>A variable ranging over the real numbers.</summary>
public sealed record ContinuousVariable(string Name) : INumericVariable;

/// <summary>A variable ranging over the whole numbers.</summary>
public sealed record IntegerVariable(string Name) : INumericVariable;

/// <summary>
/// A variable that is true or false: a boolean expression, combined with <c>&amp;</c>, <c>|</c> and
/// <c>!</c>. It is not a number; where one is wanted, its <c>Indicator</c> is one exactly when it is
/// true, and zero when it is not: <c>needsSetup.Indicator * setupTime</c>.
/// </summary>
public sealed record BinaryVariable(string Name) : IVariable, IBooleanExpression<ILogic>;
