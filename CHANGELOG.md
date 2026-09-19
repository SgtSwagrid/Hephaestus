# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- Problems are built fluently: `Problem.Minimise(objective).SubjectTo(constraint)` takes the place of `Problem.Minimise(objective, subjectTo: constraint)`, and likewise for `Maximise` and `Lexicographic`. `SubjectTo` can be repeated or given several constraints at once, `Minimise(a, b, c)` takes objectives in order of priority, and `ThenMinimise` / `ThenMaximise` add more.
- `IProblem` is now the common type of `ISingleObjectiveProblem` (what it used to be called: `Satisfaction`, `Minimisation`, `Maximisation`) and `IMultipleObjectiveProblem` (`LexicographicProblem`). `ISolver` implementations solve the former; `solver.Solve(IProblem)` dispatches.
- `ISolver.Solve` and the two backend interfaces take a starting solution, so a cancellation token passed positionally must now be named.
- The objective value of a solution is now the objective as written, read off the solution, rather than the solver's own figure.

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
- `Piecewise.If(condition, then, otherwise)`, and the product of a binary variable and an expression (`stops * dwell`), lowered to two conditional rows.
- `ISolveResult.Statistics` (encoding and solving times, best bound, nodes, iterations) on every outcome, with `result.AbsoluteGap` and `result.RelativeGap`.
- `SolverOptions.AbsoluteGap`, `Seed`, `Log` and `Parameters` (the solver's own parameters, by its own names).
- Warm starts: `solver.Solve(problem, startingFrom: solution)`, with `Solution.Empty.With(variable, value)` to build a start by hand.
- Several objectives in order of priority: `Problem.Minimise(a, subjectTo: c).Then(Objective.Maximise(b))`, solved with any solver as a sequence of ordinary solves.
- Names for expressions (`WithName`, `Name`), `constraint.Conjuncts`, and `solver.FindConflict(problem)`, which explains an infeasible problem with any solver.
- Shadow prices: `problem.ShadowPrices(solution, backend)`, quoted per named constraint, for problems with logic and whole numbers too (as the prices of the linear programme in force at the solution).
- LP and MPS export: `problem.Encode().ToLp()`, `.ToMps()` and `problem.EncodeLogic().ToLp()` (indicator constraints). Rows carry the constraint they were encoded from (`row.Origin`) and are named after it.
- Typed numbers: `Variable.Integer<int>("trains")` and `Variable.Continuous<decimal>("cost")`, quantities that read back in their own numeric type.
- `FindConflict` uses Gurobi's native IIS to narrow the search down (`IConflictBackend`, `IConflictSolver`); column bounds now know the constraint that states them (`IndicatorProblem.BoundOrigins`).
