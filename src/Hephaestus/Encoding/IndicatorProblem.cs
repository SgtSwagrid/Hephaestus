using System.Collections.Immutable;

namespace Hephaestus;

/// <summary>
/// <c>Expression &lt;= 0</c> (or <c>== 0</c>) whenever every guard holds. With no guards the row
/// holds unconditionally.
/// </summary>
public sealed record GuardedRow(
    ImmutableList<Literal> Guards,
    AffineForm Expression,
    bool IsEquality
) {
    /// <summary>
    /// The constraint of the problem (a conjunct of its one constraint) that this row was encoded
    /// from, if it is known. A row is named after it when a model is written to a file. It is kept by
    /// reference and costs nothing until it is asked for its name.
    /// </summary>
    public IBooleanExpression? Origin { get; init; }
}

/// <summary>
/// A problem with its logic encoded but no big-M in sight: bounded columns, a linear objective, and
/// rows that are linear except for being guarded by literals. This is the natural input for solvers
/// with indicator constraints (Gurobi) or half-reified constraints (CP-SAT), and the half-way point
/// on the road to a <see cref="MilpProblem"/>.
/// </summary>
public sealed record IndicatorProblem(
    ImmutableArray<Column> Columns,
    ImmutableArray<GuardedRow> Rows,
    ObjectiveSense Sense,
    AffineForm Objective
) {
    /// <summary>
    /// The constraints that the bounds of the columns were taken from, for the columns that have any.
    /// Bounds are constraints like any other, so an explanation of infeasibility has to be able to name them.
    /// </summary>
    public ImmutableDictionary<IVariable, BoundOrigin> BoundOrigins { get; init; } = ImmutableDictionary<IVariable, BoundOrigin>.Empty;
}

/// <summary>The constraints that state a column's lower and upper bounds. A side that nothing states, or that was derived rather than stated, has none.</summary>
public sealed record BoundOrigin(
    IBooleanExpression? Lower,
    IBooleanExpression? Upper
);

/// <summary>Functions over <see cref="IndicatorProblem"/>.</summary>
public static class IndicatorProblems {
    extension(IndicatorProblem problem) {
        /// <summary>
        /// The plain mixed-integer linear programme in which every guard has been relaxed with a
        /// big-M derived from propagated bounds.
        /// </summary>
        /// <exception cref="ModellingException">A big-M cannot be derived and no fallback was configured.</exception>
        public MilpProblem RelaxGuards(EncodingOptions? options = null) => Relax(problem, options ?? EncodingOptions.Default);

        /// <summary>
        /// The same problem with every column's bounds tightened to what the unconditional rows imply
        /// (feasibility-based bound tightening). For solvers that need finite domains.
        /// </summary>
        public IndicatorProblem WithPropagatedBounds(int rounds = 10) =>
            problem.WithColumnBounds(problem.DerivedBounds(rounds), [.. problem.Columns.Select(column => column.Variable)]);

        /// <summary>The same problem with the columns of the given variables bounded as stated.</summary>
        internal IndicatorProblem WithColumnBounds(ImmutableDictionary<IVariable, Interval> bounds, ImmutableHashSet<IVariable> variables) =>
            problem with { Columns = [.. problem.Columns.Select(column => variables.Contains(column.Variable) ? column with { LowerBound = bounds.Of(column.Variable).Lower, UpperBound = bounds.Of(column.Variable).Upper } : column)] };

        /// <summary>The bounds of every variable that follow from the column bounds and the unconditional rows.</summary>
        internal ImmutableDictionary<IVariable, Interval> DerivedBounds(int rounds) =>
            BoundPropagation.Propagate(
                [.. problem.Rows.Where(row => row.Guards.IsEmpty)],
                problem.Columns.ToImmutableDictionary(column => column.Variable, column => new Interval(column.LowerBound, column.UpperBound)),
                rounds);

        /// <summary>
        /// The same problem with at most one guard per row, for solvers whose indicator constraints
        /// take a single binary. A conjunction of guards is replaced by a fresh binary that must be
        /// set whenever they all hold; equal conjunctions share one.
        /// </summary>
        public IndicatorProblem WithSingleGuards(string auxiliaryPrefix = "_all") =>
            problem.Rows.Aggregate(new Conjoining(problem with { Rows = [] }, ImmutableDictionary<Conjunction, Literal>.Empty, auxiliaryPrefix, 0), Conjoin).Problem;

        /// <summary>Whether infeasibility is evident without solving: contradictory stated bounds, or an unconditional constant row that fails.</summary>
        public bool IsTriviallyInfeasible =>
            problem.Columns.Any(column => column.LowerBound > column.UpperBound)
            || problem.Rows.Any(row => row.Guards.IsEmpty && row.Expression.IsConstant && !IsSatisfied(row));
    }

    extension(MilpProblem problem) {
        /// <summary>The same programme as an indicator problem without guards, so that one backend routine can serve both forms.</summary>
        public IndicatorProblem AsIndicatorProblem() =>
            new(problem.Columns, [.. problem.Rows.SelectMany(Unguarded)], problem.Sense, problem.Objective);
    }

    /// <summary>Whether a row without variables holds.</summary>
    internal static bool IsSatisfied(GuardedRow row) =>
        row.IsEquality ? row.Expression.Constant == 0 : row.Expression.Constant <= 0;


    private static MilpProblem Relax(IndicatorProblem problem, EncodingOptions options) {
        var bounds = problem.DerivedBounds(options.BoundPropagationRounds);
        return new MilpProblem(
                problem.Columns,
                [
                    .. problem.Rows
                        .SelectMany(row => BigM.Relax(row, bounds, options).Select(relaxed => relaxed with { Origin = row.Origin }))
                        .Where(row => !(row.Coefficients.IsEmpty && row.LowerBound <= 0 && 0 <= row.UpperBound)),
                ],
                problem.Sense,
                problem.Objective);
    }

    /// <summary>
    /// The guards of a row, in a fixed order, as the key under which rows guarded alike share a
    /// binary. Literals rather than their names, so that a variable called <c>b &amp; c</c> cannot
    /// pass itself off as two guards.
    /// </summary>
    private sealed record Conjunction(ImmutableArray<Literal> Guards) {
        public bool Equals(Conjunction? other) => other is not null && Guards.SequenceEqual(other.Guards);

        public override int GetHashCode() => Guards.Aggregate(0, HashCode.Combine);
    }

    private sealed record Conjoining(
        IndicatorProblem Problem,
        ImmutableDictionary<Conjunction, Literal> Conjunctions,
        string Prefix,
        int NextIndex
    );

    private static Conjoining Conjoin(Conjoining state, GuardedRow row) =>
        row.Guards.Count <= 1 ? WithRows(state, [row])
        : state.Conjunctions.TryGetValue(Key(row.Guards), out var known) ? WithRows(state, [row with { Guards = [known] }])
        : ConjoinAfresh(state, row);

    private static Conjoining ConjoinAfresh(Conjoining state, GuardedRow row) {
        var fresh = FreshNames.After(state.NextIndex, state.Prefix, name => state.Problem.Columns.Any(column => column.Variable.Name == name));
        var all = new Literal(new BinaryVariable(fresh.Name), IsPositive: true);
        return WithRows(
            state with {
                Problem = state.Problem with { Columns = state.Problem.Columns.Add(new Column(all.Variable, 0, 1, IsAuxiliary: true)) },
                Conjunctions = state.Conjunctions.Add(Key(row.Guards), all),
                NextIndex = fresh.Index + 1,
            },
            [IndicatorEncoding.AtLeastOne([all], row.Guards) with { Origin = row.Origin }, row with { Guards = [all] }]);
    }

    private static Conjoining WithRows(Conjoining state, ImmutableArray<GuardedRow> rows) =>
        state with { Problem = state.Problem with { Rows = state.Problem.Rows.AddRange(rows) } };

    private static Conjunction Key(ImmutableList<Literal> guards) =>
        new([.. guards.OrderBy(guard => guard.Variable.Name, StringComparer.Ordinal).ThenBy(guard => guard.IsPositive)]);

    private static IEnumerable<GuardedRow> Unguarded(LinearRow row) =>
        new AffineForm(row.Coefficients, 0) is var body && row.LowerBound == row.UpperBound
            ? [new GuardedRow([], body.Plus(-row.UpperBound), IsEquality: true) { Origin = row.Origin }]
            : [
                .. double.IsPositiveInfinity(row.UpperBound) ? (GuardedRow[])[] : [new GuardedRow([], body.Plus(-row.UpperBound), IsEquality: false) { Origin = row.Origin }],
                .. double.IsNegativeInfinity(row.LowerBound) ? (GuardedRow[])[] : [new GuardedRow([], body.Negated.Plus(row.LowerBound), IsEquality: false) { Origin = row.Origin }],
            ];
}
