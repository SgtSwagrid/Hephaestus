namespace Hephaestus;

/// <summary>
/// The atoms of linear arithmetic once a problem has been lowered: an affine form related to zero,
/// with every piecewise-linear function gone. They are linear relations of a narrower kind, so a
/// lowered formula is still a formula over <see cref="ILinearRelation"/>; but a formula as written
/// is not one over these, since its relations may still hold piecewise-linear functions. A formula
/// over them is what <see cref="PiecewiseLowering"/> gives, and all that normalisation and the
/// solvers that take logic as it stands are ever handed. The one case is <see cref="AffineRelation"/>.
/// </summary>
public interface IAffineRelation : ILinearRelation;

/// <summary><c>Difference</c> related to zero: <c>Difference &lt;= 0</c>, say. The relation is kept as written, strictness and all.</summary>
public sealed record AffineRelation(
    AffineForm Difference,
    Relation Relation
) : IAffineRelation, IBooleanExpression<IAffineRelation>;
