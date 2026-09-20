using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// A formula lowered as far as it can go without knowing any bounds: rows that are linear except
/// for their guards, plus the auxiliary binaries that were introduced to get there.
/// </summary>
internal sealed record IndicatorProgram(
    ImmutableList<GuardedRow> Rows,
    ImmutableDictionary<INormalForm, Literal> Definitions,
    ImmutableList<BinaryVariable> Auxiliaries,
    int NextAuxiliaryIndex
) {
    /// <summary>The constraint being encoded, which every row added meanwhile is put down to.</summary>
    public IBooleanExpression? Origin { get; init; }

    public static IndicatorProgram Empty { get; } = new([], ImmutableDictionary<INormalForm, Literal>.Empty, [], 0);
}

/// <summary>How auxiliary variables are named: a prefix and a counter, skipping names already taken.</summary>
internal sealed record AuxiliaryNaming(
    ImmutableHashSet<string> Reserved,
    string Prefix
);

/// <summary>
/// The logical half of the MILP encoding. A disjunction <c>l&#8321; | &#8230; | l&#8342; | F&#8321; | &#8230; | F&#8345;</c>
/// of literals and compound operands says: if every literal is false, some <c>F</c> holds. So the
/// literals' negations become guards; each <c>F</c> but the last gets an auxiliary binary <c>z</c>
/// with <c>z = 1 &#8658; F</c>, whose negation also becomes a guard; and the last <c>F</c> is
/// enforced directly under all those guards. For <c>(a + h &lt;= b) | (b + h &lt;= a)</c> that is
/// the textbook either-or with one binary. One direction of implication suffices because negation
/// normal form leaves every subformula in a positive position, and equal subformulas share a binary.
/// </summary>
internal static class IndicatorEncoding {
    public static IndicatorProgram Encode(INormalForm formula, AuxiliaryNaming naming) =>
        Enforce(IndicatorProgram.Empty, formula, [], naming);

    /// <summary>
    /// Encodes a conjunction one conjunct at a time, so that every row knows which it came from. The
    /// result is the same as for the conjunction as a whole: the auxiliaries are shared and numbered
    /// alike, since the conjuncts are met in the same order.
    /// </summary>
    public static IndicatorProgram Encode(IEnumerable<(IBooleanExpression Origin, INormalForm Formula)> conjuncts, AuxiliaryNaming naming) =>
        conjuncts.Aggregate(IndicatorProgram.Empty, (program, conjunct) => Enforce(program with { Origin = conjunct.Origin }, conjunct.Formula, [], naming));

    /// <summary>The 0/1-valued affine form of a literal.</summary>
    public static AffineForm AsAffine(Literal literal) =>
        literal.IsPositive
            ? AffineForm.Zero.PlusTerm(literal.Variable, 1)
            : AffineForm.Zero.PlusTerm(literal.Variable, -1).Plus(1);

    /// <summary>One exactly when the guard is off, and nothing when it holds.</summary>
    public static AffineForm Slack(Literal guard) => AsAffine(Negated(guard));

    /// <summary>The number of guards that are off: zero exactly when the guarded row must hold.</summary>
    public static AffineForm Slack(ImmutableList<Literal> guards) =>
        guards.Aggregate(AffineForm.Zero, (slack, guard) => slack.Plus(Slack(guard)));

    private static IndicatorProgram Enforce(IndicatorProgram program, INormalForm formula, ImmutableList<Literal> guards, AuxiliaryNaming naming) =>
        formula switch {
            All all => all.Operands.Aggregate(program, (enforced, operand) => Enforce(enforced, operand, guards, naming)),
            Atom atom => WithRow(program, new GuardedRow(guards, atom.Expression, atom.IsEquality)),
            Literal literal => WithRow(program, AtLeastOne([literal], guards)),
            Any any => EnforceDisjunction(program, any, guards, naming),
            _ => throw new NotSupportedException($"Unknown kind of normal form: {formula.GetType().Name}."),
        };

    private static IndicatorProgram EnforceDisjunction(IndicatorProgram program, Any any, ImmutableList<Literal> guards, AuxiliaryNaming naming) {
        var operands = any.Operands.Select(operand => (Operand: operand, Literal: KnownLiteral(program, operand))).ToImmutableList();
        var known = operands.Select(operand => operand.Literal).OfType<Literal>().ToImmutableList();
        var compound = operands.Where(operand => operand.Literal is null).Select(operand => operand.Operand).ToImmutableList();
        return compound.IsEmpty
            ? WithRow(program, AtLeastOne(known, guards))
            : EnforceLast(compound.SkipLast(1).Aggregate(new Defined(program, known), (defined, operand) => Define(defined, operand, naming)), compound[^1], guards, naming);
    }

    /// <summary>With every other operand reduced to a literal, the last holds whenever all of those literals are false.</summary>
    private static IndicatorProgram EnforceLast(Defined defined, INormalForm last, ImmutableList<Literal> guards, AuxiliaryNaming naming) =>
        Enforce(defined.Program, last, guards.AddRange(defined.Literals.Select(Negated)), naming);

    private sealed record Defined(
        IndicatorProgram Program,
        ImmutableList<Literal> Literals
    );

    private static Literal? KnownLiteral(IndicatorProgram program, INormalForm formula) =>
        formula as Literal ?? program.Definitions.GetValueOrDefault(formula);

    /// <summary>Introduces a fresh binary <c>z</c> for <paramref name="formula"/> and enforces <c>z = 1 &#8658; formula</c>.</summary>
    private static Defined Define(Defined defined, INormalForm formula, AuxiliaryNaming naming) =>
        KnownLiteral(defined.Program, formula) is { } known
            ? defined with { Literals = defined.Literals.Add(known) }
            : DefineAfresh(defined, formula, naming);

    private static Defined DefineAfresh(Defined defined, INormalForm formula, AuxiliaryNaming naming) {
        var fresh = FreshNames.After(defined.Program.NextAuxiliaryIndex, naming.Prefix, naming.Reserved.Contains);
        var literal = new Literal(new BinaryVariable(fresh.Name), IsPositive: true);
        var declared = defined.Program with {
            Definitions = defined.Program.Definitions.Add(formula, literal),
            Auxiliaries = defined.Program.Auxiliaries.Add(literal.Variable),
            NextAuxiliaryIndex = fresh.Index + 1,
        };
        return new Defined(Enforce(declared, formula, [literal], naming), defined.Literals.Add(literal));
    }

    /// <summary><c>&#931; literals + slack &gt;= 1</c>: whenever the guards all hold, so does some literal. Purely linear.</summary>
    public static GuardedRow AtLeastOne(ImmutableList<Literal> literals, ImmutableList<Literal> guards) =>
        new([], literals.Aggregate(Slack(guards), (sum, literal) => sum.Plus(AsAffine(literal))).Negated.Plus(1), IsEquality: false);

    private static Literal Negated(Literal literal) => literal with { IsPositive = !literal.IsPositive };

    private static IndicatorProgram WithRow(IndicatorProgram program, GuardedRow row) => program with { Rows = program.Rows.Add(row with { Origin = program.Origin }) };
}
