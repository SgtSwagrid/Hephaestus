# Changelog

All notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

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
- `ISolveResult.Statistics` (encoding and solving times, best bound, nodes, iterations) on every outcome, with `result.AbsoluteGap` and `result.RelativeGap`.
- `SolverOptions.AbsoluteGap`, `Seed`, `Log` and `Parameters` (the solver's own parameters, by its own names).
- Warm starts: `solver.Solve(problem, startingFrom: solution)`, with `Solution.Empty.With(variable, value)` to build a start by hand.
- Several objectives in order of priority: `Problem.Minimise(a, subjectTo: c).Then(Objective.Maximise(b))`, solved with any solver as a sequence of ordinary solves.
