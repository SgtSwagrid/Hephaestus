namespace Hephaestus;

/// <summary>
/// Thrown when a model cannot be given a meaning: clashing variable names, non-finite coefficients,
/// or a big-M value that cannot be derived because a variable lacks bounds.
/// </summary>
public sealed class ModellingException(string message) : Exception(message);
