// Three jobs want to run on one shared machine as early as they can. Any two that both use it must
// be a changeover apart, to let the machine be reset between them:
//
//     use_A ∧ use_B  ⟹  (start_A + c ≤ start_B) ∨ (start_B + c ≤ start_A)
//
// The model below is that sentence, in NodaTime types, solved twice: by SCIP through a big-M
// encoding nobody had to write, and by Z3, which needs no encoding at all.

using Hephaestus;
using Hephaestus.NodaTime;
using Hephaestus.OrTools;
using Hephaestus.Z3;
using NodaTime;

var shiftStart = new LocalDateTime(2026, 9, 19, 8, 0);
var changeover = Duration.FromMinutes(2);

var jobs = new[] { "A", "B", "C" }
    .Select(name => new Job(
        name,
        Start: Variable.LocalDateTime($"start{name}", origin: shiftStart),
        UsesMachine: Variable.Binary($"uses{name}")))
    .ToList();

var readyAt = new Dictionary<string, LocalDateTime> {
    ["A"] = shiftStart.PlusMinutes(1),
    ["B"] = shiftStart,
    ["C"] = shiftStart.PlusSeconds(30),
};

var withinTheShift = jobs.AllOf(job => job.Start.Between(readyAt[job.Name], shiftStart.PlusHours(1)));
var everyoneRuns = jobs.AllOf(job => job.UsesMachine);
var noClashes = Pairs(jobs).AllOf(pair => IsClashFree(pair.First, pair.Second));

var totalDelay = jobs.Select(job => job.Start - readyAt[job.Name]).Sum();
var problem = Problem.Minimise(totalDelay).SubjectTo(withinTheShift & everyoneRuns & noClashes);

Console.WriteLine("The constraint, as the library sees it:");
Console.WriteLine($"  {IsClashFree(jobs[0], jobs[1]).Format()}");
Console.WriteLine();
Console.WriteLine("The programme a MILP solver is given:");
Console.WriteLine(problem.Encode().Format());

foreach (var (name, solver) in new (string, ISolver)[] { ("SCIP", OrToolsSolver.Create()), ("Z3", new Z3Solver()) }) {
    Console.WriteLine();
    Console.WriteLine(solver.Solve(problem) switch {
        Optimal(var solution) => $"{name}: {Schedule(solution)} (total delay {solution.Value(totalDelay)})",
        Feasible(var solution) => $"{name}: {Schedule(solution)} (not proven optimal)",
        Infeasible => $"{name}: no schedule exists",
        Unbounded => $"{name}: unbounded",
        Unknown(var reason) => $"{name}: gave up ({reason})",
        _ => throw new NotSupportedException($"Unknown kind of solve result: {name}."),
    });
}

IBooleanExpression IsClashFree(Job first, Job second) =>
    !(first.UsesMachine & second.UsesMachine) | IsSeparated(first, second);

IBooleanExpression IsSeparated(Job first, Job second) =>
    (first.Start + changeover <= second.Start) | (second.Start + changeover <= first.Start);

string Schedule(Solution solution) =>
    string.Join(", ", jobs.OrderBy(job => solution.Value(job.Start)).Select(job => $"{job.Name} at {solution.Value(job.Start):HH:mm:ss}"));

static IEnumerable<(T First, T Second)> Pairs<T>(IReadOnlyList<T> items) =>
    from i in Enumerable.Range(0, items.Count)
    from j in Enumerable.Range(i + 1, items.Count - i - 1)
    select (items[i], items[j]);

internal sealed record Job(
    string Name,
    Point<LocalDateTime, Duration> Start,
    BinaryVariable UsesMachine
);
