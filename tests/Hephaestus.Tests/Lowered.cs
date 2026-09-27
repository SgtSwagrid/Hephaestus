namespace Hephaestus.Tests;

/// <summary>
/// Formulas as written, lowered and then normalised, as the encoder does: only a lowered formula
/// has a normal form, since the relations of one as written may still hold piecewise-linear functions.
/// </summary>
internal static class Lowered {
    extension(IBooleanExpression<IAtom> constraint) {
        /// <summary>The negation normal form of the formula, once lowered.</summary>
        public INormalForm Normalised(double epsilon = 1e-4) => Problem.Satisfy(constraint).Linearise().Constraint.Normalise(epsilon);
    }
}
