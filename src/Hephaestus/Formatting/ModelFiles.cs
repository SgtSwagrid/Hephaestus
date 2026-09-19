using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Hephaestus;

/// <summary>
/// Writes encoded problems in the two file formats that every solver reads, so that a model can be
/// opened in a solver's own tools, tuned, archived as a benchmark, or sent to a solver's support
/// desk. LP is algebra that a person can read; MPS is older, column by column, and universal.
/// Rows are named after the constraints they were encoded from, and since neither format allows
/// much in a name, each name is reduced to letters, digits and underscores, with the constraint as
/// written in a comment above it (in LP, which has comments).
/// </summary>
public static partial class ModelFiles {
    extension(MilpProblem problem) {
        /// <summary>The programme in LP format.</summary>
        public string ToLp() => problem.AsIndicatorProblem().ToLp();

        /// <summary>The programme in free-format MPS.</summary>
        public string ToMps(string name = "hephaestus") => string.Join('\n', Mps(problem, name, new Naming(problem.Columns, RowOrigins(problem.Rows.Select(row => row.Origin))))) + "\n";
    }

    extension(IndicatorProblem problem) {
        /// <summary>
        /// The problem in LP format, with each guarded row as an indicator constraint
        /// (<c>b = 1 -&gt; x + y &lt;= 5</c>), which Gurobi, CPLEX, SCIP and HiGHS read. An indicator
        /// takes a single binary, so a conjunction of guards is given one of its own first.
        /// </summary>
        public string ToLp() => problem.WithSingleGuards() is var single ? string.Join('\n', Lp(single, new Naming(single.Columns, RowOrigins(single.Rows.Select(row => row.Origin))))) + "\n" : "";
    }

    /// <summary>The names that columns and rows go by in a file: unique, and free of anything a format might choke on.</summary>
    private sealed record Naming(
        ImmutableDictionary<IVariable, string> Columns,
        ImmutableArray<string> Rows
    ) {
        public Naming(ImmutableArray<Column> columns, IEnumerable<string> rows) : this(
            columns.Select(column => column.Variable).Zip(Unique(columns.Select(column => Plain(column.Variable.Name, "x")).Prepend(One)).Skip(1), KeyValuePair.Create).ToImmutableDictionary(),
            [.. Unique(rows.Select(row => Plain(row, "c")))]) { }
    }

    /// <summary>
    /// LP readers disagree about a bare number in the objective (Gurobi drops it), so the constant
    /// is written as a multiple of a column fixed at one, which is how Gurobi writes it too. No
    /// other column gets this name.
    /// </summary>
    private const string One = "constant_one";

    private static IEnumerable<string> RowOrigins(IEnumerable<IBooleanExpression?> origins) =>
        origins.Select((origin, index) => origin is NamedConstraint named ? named.Name : $"c{index}");

    /// <summary>Only a name that was given is used for a row; one that is merely the constraint as written would be all punctuation.</summary>
    private static string Plain(string name, string prefix) =>
        NotPlain().Replace(name, "_") is var plain && plain.Length > 0 && char.IsAsciiLetter(plain[0]) && plain[0] is not ('e' or 'E')
            ? Shortened(plain)
            : Shortened(prefix + "_" + plain);

    private static string Shortened(string name) => name.Length <= 200 ? name : name[..200];

    [GeneratedRegex("[^A-Za-z0-9_]")]
    private static partial Regex NotPlain();

    /// <summary>Names that collide once simplified are told apart by a number.</summary>
    private static IEnumerable<string> Unique(IEnumerable<string> names) =>
        names.Aggregate(
            (Taken: ImmutableHashSet<string>.Empty, Names: ImmutableList<string>.Empty),
            (seen, name) => Enumerable.Range(1, int.MaxValue).Select(attempt => attempt == 1 ? name : $"{name}_{attempt}").First(candidate => !seen.Taken.Contains(candidate)) is var unique
                ? (seen.Taken.Add(unique), seen.Names.Add(unique))
                : seen).Names;

    private static IEnumerable<string> Lp(IndicatorProblem problem, Naming naming) => [
        "\\ Written by Hephaestus.",
        problem.Sense == ObjectiveSense.Minimise ? "Minimize" : "Maximize",
        $" obj: {Terms(problem.Objective.Coefficients, naming)}{(problem.Objective.Constant == 0 ? "" : $" {Signed(problem.Objective.Constant)} {One}")}",
        "Subject To",
        .. problem.Rows.SelectMany((row, index) => LpRow(row, naming.Rows[index], naming)),
        "Bounds",
        .. problem.Objective.Constant == 0 ? (string[])[] : [$" {One} = 1"],
        .. problem.Columns.Where(column => !IsPlainBinary(column)).Select(column => $" {LpBound(column, naming.Columns[column.Variable])}"),
        .. Section("Binaries", problem.Columns.Where(IsPlainBinary), naming),
        .. Section("Generals", problem.Columns.Where(column => column.Variable.IsIntegral && !IsPlainBinary(column)), naming),
        "End",
    ];

    private static IEnumerable<string> LpRow(GuardedRow row, string name, Naming naming) => [
        .. row.Origin is null ? (string[])[] : [$"\\ {row.Origin.Name.ReplaceLineEndings(" ")}"],
        $" {name}: {(row.Guards is [var guard] ? $"{naming.Columns[guard.Variable]} = {(guard.IsPositive ? 1 : 0)} -> " : "")}"
            + $"{Terms(row.Expression.Coefficients, naming)} {(row.IsEquality ? "=" : "<=")} {Number(0 - row.Expression.Constant)}",
    ];

    private static IEnumerable<string> Section(string title, IEnumerable<Column> columns, Naming naming) =>
        columns.Select(column => $" {naming.Columns[column.Variable]}").ToList() is { Count: > 0 } names ? [title, .. names] : [];

    /// <summary>A binary column left to range over both its values; one that has been fixed is written as a general integer with its bounds.</summary>
    private static bool IsPlainBinary(Column column) => column.Variable is BinaryVariable && column is { LowerBound: 0, UpperBound: 1 };

    /// <summary>Every bound is written out, because a column that is not mentioned is taken to be non-negative.</summary>
    private static string LpBound(Column column, string name) =>
        double.IsNegativeInfinity(column.LowerBound) && double.IsPositiveInfinity(column.UpperBound) ? $"{name} free"
        : column.LowerBound == column.UpperBound ? $"{name} = {Number(column.LowerBound)}"
        : $"{Number(column.LowerBound)} <= {name} <= {Number(column.UpperBound)}";

    /// <summary>A sum of terms; a row without any is written as nothing times the first column, since a format needs something to parse.</summary>
    private static string Terms(ImmutableSortedDictionary<IVariable, double> coefficients, Naming naming) =>
        coefficients.IsEmpty
            ? $"0 {naming.Columns.Values.Order(StringComparer.Ordinal).FirstOrDefault() ?? "x"}"
            : string.Join(' ', coefficients.Select((term, index) => $"{(index == 0 ? Number(term.Value) : Signed(term.Value))} {naming.Columns[term.Key]}"));

    private static string Signed(double value) => value < 0 ? $"- {Number(-value)}" : $"+ {Number(value)}";

    private static string Number(double value) =>
        double.IsPositiveInfinity(value) ? "+inf"
        : double.IsNegativeInfinity(value) ? "-inf"
        // Rounding a bound inwards can leave a negative zero, which is no way to write nothing.
        : value == 0 ? "0"
        : value.ToString("R", CultureInfo.InvariantCulture);

    private static IEnumerable<string> Mps(MilpProblem problem, string name, Naming naming) =>
        Mps(problem, name, naming, problem.Rows.SelectMany((row, index) => row.Coefficients.Select(term => (Variable: term.Key, Row: naming.Rows[index], Coefficient: term.Value))).ToLookup(entry => entry.Variable));

    /// <summary>The lines of the file, given the matrix by column, gathered once: MPS is written column by column, and the programme is held row by row.</summary>
    private static IEnumerable<string> Mps(MilpProblem problem, string name, Naming naming, ILookup<IVariable, (IVariable Variable, string Row, double Coefficient)> entries) => [
        $"NAME {Plain(name, "m")}",
        "OBJSENSE",
        problem.Sense == ObjectiveSense.Minimise ? "    MIN" : "    MAX",
        "ROWS",
        " N  obj",
        .. problem.Rows.Select((row, index) => $" {Kind(row)}  {naming.Rows[index]}"),
        "COLUMNS",
        .. problem.Columns.SelectMany((column, index) => MpsColumn(problem, column, index, naming, entries[column.Variable])),
        "RHS",
        // The constant of the objective goes in as the negative of a right-hand side for its row.
        .. problem.Objective.Constant == 0 ? (string[])[] : [$"    rhs obj {Number(0 - problem.Objective.Constant)}"],
        .. problem.Rows.Select((row, index) => $"    rhs {naming.Rows[index]} {Number(double.IsPositiveInfinity(row.UpperBound) ? row.LowerBound : row.UpperBound)}"),
        .. Ranges(problem, naming),
        "BOUNDS",
        .. problem.Columns.SelectMany(column => MpsBounds(column, naming.Columns[column.Variable])),
        "ENDATA",
    ];

    private static string Kind(LinearRow row) =>
        row.LowerBound == row.UpperBound ? "E"
        : double.IsPositiveInfinity(row.UpperBound) ? "G"
        : "L";

    /// <summary>A row with two finite sides is an L row at its upper side, with a range reaching down to its lower.</summary>
    private static IEnumerable<string> Ranges(MilpProblem problem, Naming naming) =>
        problem.Rows
            .Select((row, index) => (Row: row, Name: naming.Rows[index]))
            .Where(entry => Kind(entry.Row) == "L" && !double.IsNegativeInfinity(entry.Row.LowerBound))
            .Select(entry => $"    rng {entry.Name} {Number(entry.Row.UpperBound - entry.Row.LowerBound)}")
            .ToList() is { Count: > 0 } ranges
                ? ["RANGES", .. ranges]
                : [];

    /// <summary>A column's entries, between markers if it is a whole number. Every column is mentioned, if only with nothing in the objective.</summary>
    private static IEnumerable<string> MpsColumn(MilpProblem problem, Column column, int index, Naming naming, IEnumerable<(IVariable Variable, string Row, double Coefficient)> entries) => [
        .. column.Variable.IsIntegral ? [$"    MARKER{index} 'MARKER' 'INTORG'"] : (string[])[],
        $"    {naming.Columns[column.Variable]} obj {Number(problem.Objective.Coefficients.GetValueOrDefault(column.Variable))}",
        .. entries.Select(entry => $"    {naming.Columns[column.Variable]} {entry.Row} {Number(entry.Coefficient)}"),
        .. column.Variable.IsIntegral ? [$"    MARKER{index} 'MARKER' 'INTEND'"] : (string[])[],
    ];

    /// <summary>Every bound is written out, because readers disagree about what a column that is not mentioned may be.</summary>
    private static IEnumerable<string> MpsBounds(Column column, string name) =>
        double.IsNegativeInfinity(column.LowerBound) && double.IsPositiveInfinity(column.UpperBound) ? [$" FR bnd {name}"]
        : column.LowerBound == column.UpperBound ? [$" FX bnd {name} {Number(column.LowerBound)}"]
        : [
            double.IsNegativeInfinity(column.LowerBound) ? $" MI bnd {name}" : $" LO bnd {name} {Number(column.LowerBound)}",
            double.IsPositiveInfinity(column.UpperBound) ? $" PL bnd {name}" : $" UP bnd {name} {Number(column.UpperBound)}",
        ];
}
