<div align="center">

  <h1>⚒️ Hephaestus</h1>
  <p>A purely functional modelling layer for MILP and SMT solvers, for C# 14 / .NET 10.</p>

  <span>
    <a href="https://github.com/SgtSwagrid/Hephaestus/actions/workflows/build-integrity.yml"><img src="https://github.com/SgtSwagrid/Hephaestus/actions/workflows/build-integrity.yml/badge.svg" alt="Build status" /></a>
    <a href="https://www.nuget.org/packages/Hephaestus.Optimisation"><img src="https://img.shields.io/nuget/vpre/Hephaestus.Optimisation.svg" alt="NuGet" /></a>
    <a href="https://alecdorrington.com/Hephaestus"><img src="https://img.shields.io/badge/docs-latest-blue.svg" alt="Documentation" /></a>
  </span>

</div>

## 💡 Overview

<br/>

> "Nothing at all takes place in the universe in which some rule of maximum or minimum does not appear." — Leonhard Euler.

<br/>

Optimisation models are specified as mathematics. Hephaestus lets the code *be* that mathematics, so that reviewing a constraint is a direct comparison with its specification:

```
occ_A ∧ occ_B  ⟹  (dep_A + h ≤ dep_B) ∨ (dep_B + h ≤ dep_A)
```

```csharp
var isSeparated    = (departureA + headway <= departureB) | (departureB + headway <= departureA);
var isConflictFree = !(occupiesA & occupiesB) | isSeparated;
```

There are no auxiliary booleans to declare, no gadget factories, and no big-M to work out by hand: the boolean structure is lowered to linear constraints automatically, and every big-M is derived from the bounds your constraints already state. The solver underneath is swappable.

## ⬇️ Installation

| Package | What it is |
| --- | --- |
| `Hephaestus.Optimisation` | The core: expressions, problems, the MILP encoding, the solver interfaces. No native dependencies. |
| `Hephaestus.Optimisation.Gurobi` | Native Gurobi backend. Conditional constraints become Gurobi indicator constraints, so no big-M is involved; the classic big-M formulation is available too. Needs a Gurobi licence. |
| `Hephaestus.Optimisation.Highs` | Standalone HiGHS backend: the leading permissively licensed MILP solver, in a few megabytes. |
| `Hephaestus.Optimisation.OrTools` | Google OR-Tools: CP-SAT through its own interface (whole-number problems, no big-M, often the fastest choice for either-or scheduling), plus SCIP, CBC and HiGHS as MILP solvers. Pure LP solvers (GLOP, CLP, PDLP) are accepted only for problems that need no whole-number variables. |
| `Hephaestus.Optimisation.Z3` | SMT backend on Microsoft Z3: native boolean structure, exact arithmetic, no encoding at all. |
| `Hephaestus.Optimisation.NodaTime` | Typed variables and expressions for the NodaTime types. |

```bash
dotnet add package Hephaestus.Optimisation --prerelease
dotnet add package Hephaestus.Optimisation.OrTools --prerelease
```

Hephaestus is in beta, so `--prerelease` is needed for now, and the API may change between versions.

(The bare name `Hephaestus` was already taken on nuget.org, hence `Hephaestus.Optimisation`. The namespace is simply `Hephaestus`.) Operators are C# 14 extension members, so consumers need the .NET 10 SDK.

## 📖 A complete example

```csharp
using Hephaestus;
using Hephaestus.OrTools;

var departureA = Variable.Continuous("departureA");
var departureB = Variable.Continuous("departureB");
var occupiesA  = Variable.Binary("occupiesA");
var occupiesB  = Variable.Binary("occupiesB");
const double headway = 120;

var withinTheHour  = departureA.Between(0, 3600) & departureB.Between(0, 3600);
var isSeparated    = (departureA + headway <= departureB) | (departureB + headway <= departureA);
var isConflictFree = !(occupiesA & occupiesB) | isSeparated;

var problem = Problem.Minimise(
    departureA + departureB,
    subjectTo: withinTheHour & isConflictFree & occupiesA & occupiesB);

var summary = OrToolsSolver.Create().Solve(problem).Match(
    optimal:    solution => $"A leaves at {solution.Value(departureA)}, B at {solution.Value(departureB)}",
    feasible:   solution => $"best found: {solution.ObjectiveValue}",
    infeasible: () => "no timetable exists",
    unbounded:  () => "unbounded",
    unknown:    reason => $"the solver gave up: {reason}");
```

Swap `OrToolsSolver.Create()` for `OrToolsSolver.Create(OrToolsSolverId.Highs)` or `new Z3Solver()` and nothing else changes. `samples/TrainHeadway` is a slightly larger version in NodaTime types.

## 🧠 The ideas

### Expressions are data; operators only build it

There are two algebraic data types, each an interface with a handful of sealed records:

- `ILinearExpression`: `Constant`, `Sum`, `Product`, and the variables.
- `IBooleanExpression`: `BooleanConstant`, `Comparison`, `Negation`, `Conjunction`, `Disjunction`, `Implication`, `Equivalence`, and `BinaryVariable`.

Keeping them apart makes illegal compositions unrepresentable: `x * y`, `(x <= 1) + 1` and `if (x <= y)` do not compile. A `BinaryVariable` belongs to both types, so `occupiesA + occupiesB <= 1` and `occupiesA & occupiesB` are both fine.

Operators (`+ - * /`, `<= >= < >`, `& | ! ^`, plus `EqualTo`, `NotEqualTo`, `Between`, `Implies`, `Iff`) are extension members that do nothing but construct records: `a + b` *is* `new Sum(a, b)`. Plain values mix in on either side: `x + 5`, `5 + x`, `0 <= x`, and, for constraints that depend on known data, `train.IsFreight.Implies(departure >= curfew)` or `isPeak & (headway >= 180)`. Nothing is flattened or simplified at construction time. All interpretation happens later, in separate passes over the data:

| Pass | Function |
| --- | --- |
| Normalise a linear expression to its canonical affine form | `expression.Normalise()` |
| Render as written, for logs and review | `expression.Format()` |
| Collect variables | `expression.Variables` |
| Lower a problem to a plain MILP | `problem.Encode()` |
| Read an expression off a solution | `solution.Value(expression)` |

Because expressions are immutable values with structural equality, a constraint can be composed from smaller named pieces, logged, compared and unit-tested without a solver or a "problem" object in sight. (`==` is reserved by C# for that structural equality, which is why the equality *constraint* is spelt `EqualTo`.)

### A problem is one constraint

```csharp
Problem.Satisfy(constraint)
Problem.Minimise(objective, subjectTo: constraint)
Problem.Maximise(objective, subjectTo: constraint)
```

Conjoin a collection with `constraints.AllOf()` (and `AnyOf()`, and `terms.Sum()`); these build balanced trees, so a hundred thousand constraints are no deeper than seventeen levels. Problems are records too: "rollback" is keeping the old value, and extending is `problem with { Constraint = problem.Constraint & extra }`.

The constraints of a model are still there to be had: `constraint.Conjuncts` takes the tree of `&` apart again, and those are the units in which an infeasibility is explained (below).

### Names, and explaining infeasibility

```csharp
var isSeparated    = ((departureA + headway <= departureB) | (departureB + headway <= departureA)).WithName("separated");
var isConflictFree = (!(occupiesA & occupiesB) | isSeparated).WithName("headway A/B");

isConflictFree.Format()                     // "headway A/B"
((NamedConstraint)isConflictFree).Expression.Format()    // "!(occupiesA & occupiesB) | separated"
```

Any expression, boolean, linear or typed, can be given a name with `WithName`. A name changes nothing about what an expression means; it is what the expression is called wherever it is written out, including inside a larger one. An expression without one goes by the way it is written, so every constraint has a usable `Name` from the start.

```csharp
if (solver.Solve(problem) is Infeasible) {
    var conflict = solver.FindConflict(problem);        // "headway A/B", "A leaves early", "B leaves early", "0 <= departureA", ...
}
```

`FindConflict` returns conjuncts that cannot all hold and none of which can be spared: drop any one and the rest can. It asks nothing of a solver but to tell feasible from infeasible, so it works with every backend, and takes about `k · log(n / k)` solves to find `k` constraints among `n`. Bounds take part like any other constraint, since that is all they are.

### Shadow prices

```csharp
var platformFree = (departure >= release).WithName("platform free");
var problem      = Problem.Minimise(totalDelay, subjectTo: platformFree & ...);

var solution = solver.Solve(problem).SolutionOrNull!;
var prices   = problem.ShadowPrices(solution, new GurobiBackend());

prices.Of(platformFree)      // seconds of total delay per second by which `release` is raised
prices.ByName                // every constraint, by name
```

The price of a constraint `lhs <= rhs` (or `>=`, or `==`) is the change in the optimal objective for each unit by which its right-hand side, as written, is raised; a constraint that is not binding has a price of zero. Prices belong to linear programmes, and a problem with logic or whole numbers is not one, but at a solution it comes down to one: every disjunction has a side that holds, and every whole-number variable has its value. Keep those, and what is left is the linear programme of the continuous variables; its dual values are the prices. They say what each constraint costs given the discrete choices that were made, not what it would cost if those could be made again. The backend that supplies the dual values need not be the solver that found the solution: Gurobi, HiGHS and OR-Tools' GLOP (`new OrToolsBackend(OrToolsSolverId.Glop)`) report them, and a solution found by Z3 or CP-SAT can be priced by any of them.

### Variables have no bounds; bounds are constraints

A variable is a name and a kind (`Continuous`, `Integer`, `Binary`). `0 <= x & x <= 3600` is a constraint like any other. The encoder recognises unconditional single-variable constraints and hands them to the solver as native column bounds, so this costs nothing.

### Big-M is derived, never supplied

The MILP encoding runs in four pure steps:

1. **Normalise** to negation normal form, with comparisons as `affine form <= 0` or `== 0`.
2. **Encode the logic** as *guarded rows*, "if these literals all hold then `e <= 0`", introducing as few auxiliary binaries as possible. In a disjunction the existing literals become guards, and only the compound disjuncts *other than the last* need an auxiliary. `(a + h <= b) | (b + h <= a)` comes out as the textbook either-or with one binary; `flag.Iff(x >= 5)` needs none. Equal subformulas share one auxiliary.
3. **Propagate bounds** (feasibility-based bound tightening) over the unconditional rows. `x <= 3600` bounds `x` directly; `arrival == departure + run` bounds `arrival` once `departure` and `run` are bounded; and so on down the line until nothing changes.
4. **Relax each guard** with `e <= M · (number of guards that are off)`, where `M` is the largest value `e` can take inside the propagated bounds. That is the tightest valid `M`, computed separately for every row. Rows that can never bind are dropped; rows that can never hold forbid their guards outright.

If some `M` is infinite, encoding fails with an error naming the variables that lack bounds. That is deliberate: a guessed big-M that is too small silently cuts off solutions, and the whole point is that wrong constraints should not fail silently. If you really want a guess, opt in with `new EncodingOptions(FallbackBigM: 1e6)`.

The first two steps are `problem.EncodeLogic()`, giving an `IndicatorProblem`; the last two are `.RelaxGuards()`, giving a `MilpProblem`; `problem.Encode()` is both. Either result can be printed with `.Format()`. Solvers with indicator or half-reified constraints (Gurobi, CP-SAT) stop after the first half and never see a big-M, and the Z3 backend skips all of it, because an SMT solver takes the boolean structure as it stands.

### Strictness

Over whole-valued expressions, `n < 5` is exactly `n <= 4`. Over the reals a MILP cannot express strictness, so `x < 5` becomes `x + ε <= 5` with `EncodingOptions.StrictnessEpsilon` (default `1e-4`, comfortably above solver tolerances). Negation is where this usually bites: `!(x <= 5)` is `x > 5`. Z3 has true strict inequalities and no epsilon.

### Reified truth values

To use the truth of a constraint as a number (say, to count violated soft constraints), tie it to a binary variable yourself: `isLate.Iff(arrival >= deadline)`, then use `isLate` in the objective. `Iff` binds in both directions.

### Min, max and absolute value

```csharp
using static Hephaestus.Piecewise;

var isPunctual = Abs(arrival - booked) <= tolerance;
var makespan   = Max(finishes);
var problem    = Problem.Minimise(makespan + 10 * Abs(arrival - booked), subjectTo: ...);
```

`Max`, `Min` and `Abs` are records like everything else, and work on plain and typed expressions alike. When a problem is encoded, each becomes an auxiliary variable tied to its operands (all three are maxima: `min(a, b) = -max(-a, -b)` and `|e| = max(e, -e)`), and equal ones share a variable. How it is tied depends on how the problem leans on it. Minimising a maximum, or bounding an absolute value from above, only tempts the solver to make the variable too small, so `m >= a & m >= b` is enough and no binary variable is spent; that is the usual linear-programming idiom, found for you. Only a use that rewards a larger value (`Abs(x - y) >= 5`, or maximising a maximum) adds `m <= a | m <= b`, which costs one binary. The variable is bounded by the bounds of its operands, so its big-M is derived like any other.

### Typed expressions

A `Quantity<T>` is a linear expression read as an *amount* of type `T` (a duration); a `Point<T, TDelta>` is one read as a *position* (a date-time) whose differences are amounts of `TDelta`. Each pairs an ordinary `ILinearExpression` with an `IProjection<T>`, an affine map between `T` and the solver's number line ("seconds since 08:00").

The two types carry the right algebra, once, generically:

```csharp
Quantity<Duration>               runTime  = arrival - departure;          // point - point
Point<LocalDateTime, Duration>   earliest = departure + dwell + minimum;  // point + quantity
IBooleanExpression               onTime   = arrival <= deadline;          // compare with plain values
Point<LocalDateTime, Duration>   release  = start + dwell;                // plain value + quantity
//                                          arrival + departure           // does not compile
//                                          2 * arrival                   // does not compile
//                                          arrival <= dwell              // does not compile
```

Plain values of the right type are welcome wherever an expression is, on either side (`Duration.FromMinutes(2) + departure`, `start <= departure`, `dwell.Between(minimum, arrival - departure)`), and `delay.In(Duration.FromMinutes(1))` turns a quantity back into a plain linear expression ("delay in minutes") for a cost function.

Solutions are read back in the same types: `solution.Value(arrival)` is a `LocalDateTime`, and so is `solution.Value(departure + dwell)`; any expression can be read, not just variables. Expressions under different projections (minutes against seconds, different origins) are reconciled automatically.

The core ships projections for `TimeSpan`, `DateTime` and `DateTimeOffset`; `Hephaestus.Optimisation.NodaTime` adds `Duration`, `Instant`, `LocalDateTime`, `LocalDate` (in `Period`s of whole days), `LocalTime`, `OffsetDateTime` and `ZonedDateTime`:

```csharp
var departure = Variable.LocalDateTime("departure", origin: start);
var dwell     = Variable.Duration("dwell", unit: Duration.FromSeconds(30), inWholeUnits: true);  // quantised
```

Supporting another type means writing one small record that implements `IProjection<T>` (or `IPointProjection<T, TDelta>`), plus, for a point type, the three one-line `T ± Quantity<TDelta>` operators that C# will not let the core declare generically (they delegate to `quantity.Beyond(origin, projection)`). Typed reads round the underlying number to five decimal places of the unit by default, so that solver noise does not turn 08:04:00 into 08:03:59.99999999.

### Swapping the solver

`ISolver` is the seam: `ISolveResult Solve(IProblem problem, CancellationToken cancellationToken = default)`. A backend joins at whichever level suits the solver:

| A solver that takes... | implements | and sees | Examples |
| --- | --- | --- | --- |
| logic as it stands | `ISolver` | the `IProblem` itself | `Z3Solver` |
| conditional linear constraints | `IIndicatorBackend` | an `IndicatorProblem`: linear rows guarded by literals, no big-M | `GurobiBackend`, `CpSatBackend` |
| linear constraints only | `IMilpBackend` | a `MilpProblem`: bounded columns, linear rows, a linear objective | `HighsBackend`, `OrToolsBackend` |

`IndicatorSolver` and `MilpSolver` wrap the latter two into an `ISolver`: they encode, solve, hide the auxiliaries and snap whole-number variables. A backend is about a hundred lines.

```csharp
GurobiSolver.Create()                       // indicator constraints, no big-M
GurobiSolver.Create(useIndicators: false)   // classic big-M, derived per row
CpSatSolver.Create()                        // whole-number problems only
HighsSolver.Create()
OrToolsSolver.Create(OrToolsSolverId.Scip)
new Z3Solver()
```

The test suite runs one contract against SCIP, CBC, HiGHS (standalone and through OR-Tools), Gurobi (both formulations) and Z3, and a second, whole-number contract against those and CP-SAT. The Gurobi tests are skipped where no licence is found.

### Tuning a solve, and what it reports

```csharp
var options = new SolverOptions(TimeLimit: TimeSpan.FromMinutes(5), RelativeGap: 0.01, Seed: 7, Log: Console.Write)
    .With("MIPFocus", "1");                       // the solver's own parameters, by its own names

var result = GurobiSolver.Create(options: options).Solve(problem);

result.Statistics.SolvingTime                     // and EncodingTime, BestBound, Nodes, Iterations
result.RelativeGap                                // how far a Feasible result might be from optimal
```

`TimeLimit`, `RelativeGap`, `AbsoluteGap`, `Threads` and `Seed` mean the same to every solver that has them. `Parameters` go to the solver verbatim, and one it does not recognise is an error rather than a silent no-op. Every result carries `Statistics`, whatever its outcome; a figure that a solver does not report is null (Z3 proves optimality without bounds, so it has no gap to give). Gurobi and CP-SAT deliver their log to `Log`; the others can only write to standard output, and do so when it is set.

### Several objectives

```csharp
var problem = Problem.Minimise(totalDelay, subjectTo: constraint)
    .Then(Objective.Minimise(platformChanges))
    .Then(Objective.Maximise(slack));

var result = solver.Solve(problem);
```

Objectives that are to be traded off against each other need nothing special: weigh them into one expression. Objectives in order of priority are a `LexicographicProblem`: each is optimised in turn, among the solutions that are best for those before it. That is done with a sequence of ordinary solves, so it works with every solver: each stage is held to the values already found and starts from the solution before. An objective can give ground to those after it (`Objective.Minimise(totalDelay, relativeTolerance: 0.01)`, or a typed `tolerance: Duration.FromMinutes(2)`); `Problem.Lexicographic([...], subjectTo: ...)` takes the whole list at once. The result is `Optimal` only if every stage was, its objective value is that of the first objective (read the others with `solution.Value(...)`), and the solver's limits apply to each stage separately.

### Warm starts

```csharp
var yesterday = solver.Solve(timetable).SolutionOrNull;
var today     = solver.Solve(timetableWithOneMoreTrain, startingFrom: yesterday);

var byHand    = Solution.Empty.With(departureA, start.PlusMinutes(5)).With(occupiesA, true);
```

A starting solution is a hint and nothing more: it may cover only some of the variables, mention ones the problem lacks, or be infeasible, and the answer is the same, only perhaps sooner. Only the modeller's own variables are passed on; the auxiliaries of the encoding are left for the solver to fill in, which keeps a start valid across problems whose encodings differ. Gurobi, CP-SAT, SCIP, CBC and standalone HiGHS take the hint; Z3 has no use for one.

## ⚠️ Things worth knowing

- Sum and conjoin collections with `Sum()`, `AllOf()` and `AnyOf()` rather than folding `+` or `&` yourself. (Folding still works: deep trees are handled without overflowing the stack. It is merely slower, and the records' built-in `ToString`/`Equals` do recurse.)
- An equivalence duplicates its operands when negations are pushed inwards, so *deeply nested* `Iff`s grow exponentially. Name the inner ones with binary variables.
- Variables are identified by name and kind. Two variables of different kinds sharing a name is an error.
- Do not reference `Hephaestus.Optimisation.Highs` and `Hephaestus.Optimisation.OrTools` from the same application. Both upstream packages ship a native `highs.dll`, of different versions, and whichever is copied last breaks the other.
- CP-SAT works in whole numbers over finite domains. It refuses continuous variables by name, needs every variable bounded (directly or by implication), and scales fractional coefficients by a power of ten, refusing those that none makes whole (a third, say).
- HiGHS, as driven by OR-Tools, prints a one-line banner to standard output on every solve. That is upstream behaviour; the standalone HiGHS backend and the other solvers are silent.

## 🛠️ Building

```bash
dotnet build
dotnet test
dotnet run --project samples/TrainHeadway
dotnet pack --configuration Release --output artefacts
```

The test projects use xunit.v3 on Microsoft.Testing.Platform (see `global.json`). `tests/Hephaestus.Tests/EncodingEquivalenceTests.cs` is the one to read first: it checks, exhaustively over a grid and for hundreds of random formulas, that an assignment satisfies a formula exactly when it extends to a solution of the encoded rows.

Publishing a GitHub release tagged `v1.2.3` tests, packs and publishes version `1.2.3` of every package to nuget.org, and publishes the API documentation. See [CONTRIBUTING.md](CONTRIBUTING.md#-publishing-workflow).

## 👮‍♂️ Licence

MIT; see [LICENSE.md](LICENSE.md).

## 👁️ See also

- This library was made using [C# Library Template](https://github.com/SgtSwagrid/cs-library-template).
