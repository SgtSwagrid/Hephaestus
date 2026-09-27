namespace Hephaestus;

/// <summary>
/// Factory functions for decision variables. Integration packages extend this type with typed
/// variables (for example <c>Variable.Duration</c> from the NodaTime package).
/// </summary>
public static class Variable {
    /// <summary>A real-valued variable.</summary>
    public static ContinuousVariable Continuous(string name) => new(name);

    /// <summary>A whole-number variable.</summary>
    public static IntegerVariable Integer(string name) => new(name);

    /// <summary>A variable that is true or false. Where a number is wanted, its <c>Indicator</c> is one when it is true and zero when not.</summary>
    public static BinaryVariable Binary(string name) => new(name);
}
