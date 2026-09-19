namespace Hephaestus;

/// <summary>
/// A decision variable: a name and a kind, nothing more. Variables are values, so two variables of
/// the same kind and name are the same variable. Bounds are not part of a variable; they are
/// ordinary constraints (<c>0 &lt;= x &amp; x &lt;= 10</c>), which the encoder recognises and passes
/// to the solver as native bounds.
/// </summary>
public interface IVariable : ILinearExpression {
    /// <summary>The name that identifies the variable within a problem.</summary>
    string Name { get; }
}

/// <summary>A variable ranging over the real numbers.</summary>
public sealed record ContinuousVariable(string Name) : IVariable;

/// <summary>A variable ranging over the whole numbers.</summary>
public sealed record IntegerVariable(string Name) : IVariable;

/// <summary>
/// A variable that is either 0 or 1. It is both a linear expression (its 0/1 value) and a boolean
/// expression (true exactly when it is 1), so it can be summed and scaled as well as combined with
/// <c>&amp;</c>, <c>|</c> and <c>!</c>.
/// </summary>
public sealed record BinaryVariable(string Name) : IVariable, IBooleanExpression;
