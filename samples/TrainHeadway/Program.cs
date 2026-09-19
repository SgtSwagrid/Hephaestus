// Three trains want to leave over one shared section of track as early as they can. Any two that
// both use the section must be a headway apart:
//
//     occ_A ∧ occ_B  ⟹  (dep_A + h ≤ dep_B) ∨ (dep_B + h ≤ dep_A)
//
// The model below is that sentence, in NodaTime types, solved twice: by SCIP through a big-M
// encoding nobody had to write, and by Z3, which needs no encoding at all.

using Hephaestus;
using Hephaestus.NodaTime;
using Hephaestus.OrTools;
using Hephaestus.Z3;
using NodaTime;

var start = new LocalDateTime(2026, 9, 19, 8, 0);
var headway = Duration.FromMinutes(2);

var trains = new[] { "A", "B", "C" }
    .Select(name => new Train(
        name,
        Departure: Variable.LocalDateTime($"departure{name}", origin: start),
        OccupiesSection: Variable.Binary($"occupies{name}")))
    .ToList();

var earliest = new Dictionary<string, LocalDateTime> {
    ["A"] = start.PlusMinutes(1),
    ["B"] = start,
    ["C"] = start.PlusSeconds(30),
};

var withinTheHour = trains.AllOf(train => train.Departure.Between(earliest[train.Name], start.PlusHours(1)));
var everyoneRuns = trains.AllOf(train => train.OccupiesSection);
var noConflicts = Pairs(trains).AllOf(pair => IsConflictFree(pair.First, pair.Second));

var totalDelay = trains.Select(train => train.Departure - earliest[train.Name]).Sum();
var problem = Problem.Minimise(totalDelay, subjectTo: withinTheHour & everyoneRuns & noConflicts);

Console.WriteLine("The constraint, as the library sees it:");
Console.WriteLine($"  {IsConflictFree(trains[0], trains[1]).Format()}");
Console.WriteLine();
Console.WriteLine("The programme a MILP solver is given:");
Console.WriteLine(problem.Encode().Format());

foreach (var (name, solver) in new (string, ISolver)[] { ("SCIP", OrToolsSolver.Create()), ("Z3", new Z3Solver()) }) {
    Console.WriteLine();
    Console.WriteLine(solver.Solve(problem).Match(
        optimal: solution => $"{name}: {Timetable(solution)} (total delay {solution.Value(totalDelay)})",
        feasible: solution => $"{name}: {Timetable(solution)} (not proven optimal)",
        infeasible: () => $"{name}: no timetable exists",
        unbounded: () => $"{name}: unbounded",
        unknown: reason => $"{name}: gave up ({reason})"));
}

IBooleanExpression IsConflictFree(Train first, Train second) =>
    !(first.OccupiesSection & second.OccupiesSection) | IsSeparated(first, second);

IBooleanExpression IsSeparated(Train first, Train second) =>
    (first.Departure + headway <= second.Departure) | (second.Departure + headway <= first.Departure);

string Timetable(Solution solution) =>
    string.Join(", ", trains.OrderBy(train => solution.Value(train.Departure)).Select(train => $"{train.Name} at {solution.Value(train.Departure):HH:mm:ss}"));

static IEnumerable<(T First, T Second)> Pairs<T>(IReadOnlyList<T> items) =>
    from i in Enumerable.Range(0, items.Count)
    from j in Enumerable.Range(i + 1, items.Count - i - 1)
    select (items[i], items[j]);

internal sealed record Train(
    string Name,
    Point<LocalDateTime, Duration> Departure,
    BinaryVariable OccupiesSection
);
