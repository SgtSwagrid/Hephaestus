# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- `Zip` puts any two typed expressions side by side as one of a pair, whatever each is made of: `start.Zip(runtime)`, or a number and a truth with `runtime.Zip(flag.AsEncodable())`. The pair is compared entry by entry, read with `solution.Value`, given a starting value with `solution.With`, and zipped again. `Select` and `Biselect` take a pair's halves (`x.Zip(y).Biselect((a, b) => new Location(a, b), place => (place.X, place.Y))`), `Sequence()` makes many into one array-valued expression, and `AsEncodable()` lets plain linear and boolean expressions join in.
- `Vector<T>`, a fixed number of expressions combined entry by entry through `Zip` and `Select`: `u.Zip(v).Select((a, b) => a + b)`. Vectors of quantities add, subtract, scale, `Sum`, take a `Dot` product with plain weights, and compare with vectors, plain arrays and plain values in every entry (`u + v < [4, 5, 6]`); they are read back as vectors of values.

### Changed

- Typed expressions are made of components, each a `LinearComponent` (a number) or a `LogicalComponent` (a truth), rather than of one linear expression: `IProjectedExpression.Expression` is now `Components`, and the decoders and encoders of typed expressions read and write an `ImmutableArray<double>` with one entry for each component. `IEncodable<TValue>` is the common type of everything typed; `ILinearlyEncodable<TValue>` and `ILogicallyEncodable<TValue>` are its cases of one number and of one truth, and keep their `Expression`.
- Comparison, `EqualTo`, `NotEqualTo`, `Between` and `solution.With` are written against `IEncodable<TValue>` and work entry by entry, truths being ordered by implication. `ILogicallyEncodable<TValue>` gains them, and `solution.Value`, as a result.

## [0.1.0-beta.2] - 2026-09-20

### Changed

- Problems are built fluently: `Problem.Minimise(objective).SubjectTo(constraint)` takes the place of `Problem.Minimise(objective, subjectTo: constraint)`, and likewise for `Maximise` and `Lexicographic`. `SubjectTo` can be repeated or given several constraints at once, `Minimise(a, b, c)` takes objectives in order of priority, and `ThenMinimise` / `ThenMaximise` add more.
- Objectives are values with a hierarchy of their own: `IObjective` is an `ISingleObjective` (`NoObjective`, `Optimisation`) or an `ILexicographicObjective` (a list of `Prioritised` objectives, which is where tolerances live). A problem is an objective and a constraint (`SingleObjectiveProblem`, `MultipleObjectiveProblem`); the records `Satisfaction`, `Minimisation`, `Maximisation`, `LexicographicProblem` and `Objective` are gone, and `Objective` is now the factory (`Objective.Minimise(x).Then(...)`, `Problem.Optimise(objective)`).
- `solution.Value` is one method for every kind of expression, returning a number for a linear one, a truth for a constraint, and its own type for anything read through a projection. `IReadableExpression<out TValue>` is what they all are; `IDecodedExpression<TValue>` adds a decoder, `IWritableExpression<in TValue>` an encoder, and `ILinearlyEncodable<TValue>` is both by way of its projection.
- The `decimalPlaces` parameter is gone from `Value`, and `tolerance` is no longer optional: reading a constraint without naming one forgives `Evaluation.Tolerance`, and naming one selects the overload that takes it. Both defaults are now public constants (`Evaluation.Tolerance`, `Evaluation.DecimalPlaces`).
- `IProjection<TValue>` is now `IProjection<TValue, double>`, the common case of a projection onto a raw form; `IProjection<TValue, bool>` projects onto a truth, so a two-state type can be carried by a single binary (`ILogicallyEncodable<TValue>`). Existing projections are unaffected.
- Projections and projected expressions can be re-viewed, each giving back what it still is: `Biselect` gives one of another type, `Select` gives a reading no constraint can mention, and `Preselect` gives something no solution can be asked for.
- `Quantity<T>` and `Point<T, TDelta>` are both `ILinearlyEncodable<TValue>`, which carries the expression and the projection. Comparison, `EqualTo`, `NotEqualTo`, `Between`, `In(projection)`, `Value`, `With`, `Minimise` and `Maximise` are written once against it rather than once per kind, and a type of your own that implements it is treated alike. Only the arithmetic stays with each: amounts add and scale, positions do not.
- `Quantity<T>` and `Point<T, TDelta>` are both `ILinearlyEncodable<TValue>`, which carries the expression and the projection. Comparison, `EqualTo`, `NotEqualTo`, `Between`, `In(projection)`, `Value`, `With`, `Minimise` and `Maximise` are written once against it rather than once per kind, and a type of your own that implements it is treated alike. Only the arithmetic stays with each: amounts add and scale, positions do not.
- A problem with no objective is a `SatisfactionProblem` rather than a `SingleObjectiveProblem` holding an `Objective.None`. The two are the cases of `IOneShotProblem`, which takes the place of `ISingleObjectiveProblem` as what a backend solves in one go; `SingleObjectiveProblem.Objective` is now an `Optimisation`, so a problem with nothing to optimise cannot be built as one.
- `IProblem` is now the common type of `ISingleObjectiveProblem` (what it used to be called: `Satisfaction`, `Minimisation`, `Maximisation`) and `IMultipleObjectiveProblem` (`LexicographicProblem`). `ISolver` implementations solve the former; `solver.Solve(IProblem)` dispatches.
- `ISolver.Solve` and the two backend interfaces take a starting solution, so a cancellation token passed positionally must now be named.
- The objective value of a solution is now the objective as written, read off the solution, rather than the solver's own figure.

### Removed

- `result.Match(optimal: ..., feasible: ...)`. A `switch` expression over `Optimal`, `Feasible`, `Infeasible`, `Unbounded` and `Unknown` says the same thing in the language's own syntax, with positional patterns for the solution and the reason.

### Added

- Core (`Hephaestus.Optimisation`): immutable linear and boolean expression records with C# 14 extension operators; problems as a single constraint with an optional objective; normalisation to affine and negation normal forms; MILP encoding with guarded rows, bound propagation and per-row derived big-M values; typed `Quantity<T>` and `Point<T, TDelta>` expressions with projections for `TimeSpan`, `DateTime` and `DateTimeOffset`; the `ISolver` and `IMilpBackend` seams.
- `Hephaestus.Optimisation.OrTools`: MILP backend on Google OR-Tools.
- `Hephaestus.Optimisation.Z3`: SMT backend on Microsoft Z3.
- `Hephaestus.Optimisation.Gurobi`: native Gurobi backend, with conditional constraints as indicator constraints (no big-M) or, optionally, the classic big-M formulation.
- `Hephaestus.Optimisation.Highs`: standalone HiGHS backend.
- CP-SAT through its own interface (`CpSatSolver`), in `Hephaestus.Optimisation.OrTools`, for whole-number problems without any big-M.
- A third backend seam, `IIndicatorBackend`, over the new `IndicatorProblem`; `Encode()` is now `EncodeLogic()` followed by `RelaxGuards()`.
- `Hephaestus.Optimisation.NodaTime`: projections and typed variables for the NodaTime types.
- `Piecewise.Max`, `Piecewise.Min` and `Piecewise.Abs`, over plain and typed expressions. They are lowered to linear form when a problem is encoded (`problem.Linearise()`), spending a binary variable only where the problem rewards a larger maximum.
- `Piecewise.If(condition, then, otherwise)`, and the product of a binary variable and an expression (`needsSetup * setupTime`), lowered to two conditional rows.
- `ISolveResult.Statistics` (encoding and solving times, best bound, nodes, iterations) on every outcome, with `result.AbsoluteGap` and `result.RelativeGap`.
- `SolverOptions.AbsoluteGap`, `Seed`, `Log` and `Parameters` (the solver's own parameters, by its own names).
- Warm starts: `solver.Solve(problem, startingFrom: solution)`, with `Solution.Empty.With(variable, value)` to build a start by hand.
- Several objectives in order of priority: `Problem.Minimise(a, subjectTo: c).Then(Objective.Maximise(b))`, solved with any solver as a sequence of ordinary solves.
- Names for expressions (`WithName`, `Name`), `constraint.Conjuncts`, and `solver.FindConflict(problem)`, which explains an infeasible problem with any solver.
- Shadow prices: `problem.ShadowPrices(solution, backend)`, quoted per named constraint, for problems with logic and whole numbers too (as the prices of the linear programme in force at the solution).
- LP and MPS export: `problem.Encode().ToLp()`, `.ToMps()` and `problem.EncodeLogic().ToLp()` (indicator constraints). Rows carry the constraint they were encoded from (`row.Origin`) and are named after it.
- Typed numbers: `Variable.Integer<int>("jobs")` and `Variable.Continuous<decimal>("cost")`, quantities that read back in their own numeric type.
- `Objective.None`, and `ThenMinimise` / `ThenMaximise` on objectives themselves; the ones on problems are defined by them.
- `FindConflict` uses Gurobi's native IIS to narrow the search down (`IConflictBackend`, `IConflictSolver`); column bounds now know the constraint that states them (`IndicatorProblem.BoundOrigins`).
