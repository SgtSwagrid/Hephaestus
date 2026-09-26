# CLAUDE.md

This file provides guidance to [Claude Code](https://claude.com/product/claude-code) when working with code in this repository.
It is not intended for human eyes.

### Maintenance

You (robot or human) have standing permission to update this file without asking.
Add important patterns, gotchas, or context that would help future sessions.
Keep it concise and actionable.

## Project overview

This is a C# library providing a purely functional modelling layer for MILP and SMT solvers.
Constraints are immutable expressions that read like the mathematics they represent,
and are lowered to whichever solver is plugged in. The [README](README.md) records the design in detail.

### Structure

- [src/Hephaestus](src/Hephaestus) is the core (package `Hephaestus.Optimisation`). It has no native dependencies and must stay that way.
- Every other project in [src](src) is a solver backend or an integration, published as a separate package `Hephaestus.Optimisation.*`.
- Namespaces are `Hephaestus.*`; the package IDs are longer only because `Hephaestus` is taken on nuget.org.
- The solver contracts live in [tests/Shared](tests/Shared) and are compiled into each solver test project. [tests/Hephaestus.Solvers.Tests](tests/Hephaestus.Solvers.Tests) runs them against every backend but one; add new backends to it.
- The exception is standalone HiGHS, in [tests/Hephaestus.Highs.Tests](tests/Hephaestus.Highs.Tests): `Highs.Native` and `Google.OrTools` both ship a native `highs.dll`, so the two can never share an output folder.
- `Piecewise.Max/Min/Abs` are expression records that `problem.Linearise()` lowers before anything else sees them (the encoder and Z3 both call it). A backend that implements `ISolver` directly must call it too, or `Normalise()` will throw on them.
- Backends join at one of three seams: `ISolver` (takes logic as it stands, e.g. Z3), `IIndicatorBackend` (takes rows guarded by literals, no big-M, e.g. Gurobi and CP-SAT) or `IMilpBackend` (linear rows only, e.g. HiGHS). `problem.Encode()` is `EncodeLogic()` then `RelaxGuards()`.
- A backend that can explain infeasibility natively implements `IConflictBackend`; `FindConflict` treats its answer as a head start and still runs the generic search over it, because irreducible rows need not mean irreducible constraints. Derived bounds on auxiliary columns are lifted before asking (`Conflicts.Narrow`), since they have no constraint to be put down to.
- Model files (`ModelFiles.cs`) are tested by round trip: HiGHS (in CI) and Gurobi read the LP/MPS text back and must reach the same optimum. That is how Gurobi dropping a bare constant in an LP objective was found; prefer such tests to golden text alone.
- `tests/Shared/SensitivityContract.cs` checks shadow prices against finite differences, which is what pins down each backend's dual sign convention; a backend that gains `RowDuals` should be added to it.
- Do not call `Solver` (or anything else that may skip) inside `Assert.All`: a skip is an exception, and `Assert.All` reports it as a failure. CI has no Gurobi licence, so that only shows up there.
- The Gurobi tests skip themselves when no valid licence is found. The Gurobi NuGet package bundles no fallback licence, so they do not run in CI; run them on a machine with a licence after touching [src/Hephaestus.Gurobi](src/Hephaestus.Gurobi). They were last verified against a real solver on 2026-09-19, with a size-limited demo licence; [GurobiTests.cs](tests/Hephaestus.Solvers.Tests/GurobiTests.cs) covers what the shared contracts do not.

### Design invariants

These were deliberate decisions. Don't reintroduce what they rule out:

- Expressions are as-written data. Operators only construct records; flattening, normalisation and simplification happen in later passes, never at construction time. The one exception is `IReadableExpression<TValue>.Read`, which every kind of expression supplies as an internal default interface member: it is what lets a single `solution.Value` serve them all, it is denotation rather than a pass (it delegates straight to `Evaluation`), and being internal it never reaches the public surface of a record. Do not add other members this way.
- A problem is _one_ constraint plus its objectives, not a list of constraints. Problems are built fluently (`Problem.Minimise(x).SubjectTo(c)`), each step giving a new value. `IProblem` is either an `IOneShotProblem` (all a backend or an `ISolver` ever has to solve) or an `IMultipleObjectiveProblem` (solved as a sequence of those by an extension method). An `IOneShotProblem` is a `SatisfactionProblem` (nothing to optimise) or a `SingleObjectiveProblem` (whose `Objective` is an `Optimisation`, not an `ISingleObjective`, so a problem with no objective cannot be built as one). Which one is decided by its objective: `IObjective` is an `ISingleObjective` (`NoObjective` | `Optimisation`) or an `ILexicographicObjective`; tolerances exist only on `Prioritised` entries of the latter. Don't reintroduce per-sense problem records. "The constraints" of a model are the `Conjuncts` of that one constraint; diagnostics (names, conflicts, shadow prices) are quoted in those, and work on the as-written expressions rather than on encoded rows.
- Adding a case to `ILinearExpression` or `IBooleanExpression` means teaching every pass about it: `Occurrences`, `Formatting`, both normalisations, `Evaluation`, `PiecewiseLowering`, and the Z3 translation. Each ends in a `NotSupportedException` for unknown cases, so the tests find what was missed.
- `Quantity<T>` and `Point<T, TDelta>` are the same data — a linear expression and a projection, which is `ILinearlyEncodable<TValue>` — and differ only in their arithmetic: a quantity is a vector (adds, scales), a point is a position in the affine space over it (no `point + point`, no scaling). They cannot be merged: the distinction has to sit in the type because C# declares operators per type, and it cannot sit in `T` because constraining `TimeSpan` or NodaTime's `Duration` to an interface of ours is impossible (C# 14 extensions add members, not implementations). Everything that is not arithmetic belongs on an interface, not on either record: comparison, `Value` and `With` on `IEncodable<TValue>` (entry by entry), and `In` and `Minimise`, which need one number, on `ILinearlyEncodable<TValue>`.
- A typed expression is `IEncodable<TValue>`: components, each a `LinearComponent` (a number) or a `LogicalComponent` (a truth, `1` or `0` in the raw form), and a projection onto `ImmutableArray<double>`. The kind is tagged because a `BinaryVariable` is both kinds of expression. Only `Componentwise` (relations, plain constants, conversion by probing the origin and unit vectors) and `Evaluation` (reading, `With`) switch on the kind; a new kind of component must be taught to both. `Zip` is defined once, on `IEncodable`, for every kind. There is deliberately no `pure`/`Constant` for encodables: encoding a constant is not injective, so `EqualTo` on one would be wrong. Never add `SelectMany`: a model's shape must not depend on its solution.
- `Vector<T>` is a different functor: it maps expressions while a model is built, where `IDecodedExpression.Select` maps values once it is solved. Keep it a plain container; were it an `IEncodable`, the two `Select`s would collide on an implicitly typed lambda. Its operators are its `Zip` and `Select` applied to `Quantity`'s, one extension block per element type, because `Quantity`'s own operators are extensions that no generic constraint can ask for.
- Beware removing an overload: optional and `params` siblings silently absorb its call sites. Deleting `Objective.Minimise(ILinearExpression)` rebinds `Minimise(x)` to the tolerance overload and changes the return type from `Optimisation` to `Prioritised`; `Problem.Minimise(x)` likewise falls into the `params` overload and becomes multi-objective. Both compile. Check what a call binds to after any such deletion.
- Variables carry no bounds. Bounds are ordinary constraints, recovered as column bounds by the encoder.
- Big-M values are never supplied by the user, but derived per row from propagated bounds.

### Configuration

Many configuration files (`.editorconfig`, the root `Directory.Build.props` and `Directory.Build.targets`, `global.json`, `.github/workflows/*`, `CONTRIBUTING.md`, `docs/STYLE_GUIDE.md`, etc.)
are synchronised from [C# Library Config](https://github.com/SgtSwagrid/cs-library-config) and will be overwritten.
Don't change them here; change `src/Directory.Build.props`, `tests/Directory.Build.props` or `Directory.Packages.props` instead,
or else propose the change upstream.

## Instructions

### Workflow

- Open a pull request for finished, verified work by default, without asking first. Merging and releasing still need the say-so of a human.

### Compilation and Diagnostics

- When the user asks for help with a compilation or type error, start by running `dotnet build` to see the error for yourself.
  If there are many errors, making it unclear which one the user is referring to, ask them to clarify, and then focus only on that issue.
- JetBrains IDE MCP integration may be active. When a request seems to implicitly refer to something the user is looking at, check
  `mcp__ide__getDiagnostics` first to see which file(s) are open and get associated diagnostics (errors, warnings, and info hints with line numbers).

#### Testing

- After making code changes, always run `dotnet build` and `dotnet test` to verify that issues are fixed and no new ones are introduced.
- Warnings are errors. Don't suppress a warning without a comment explaining why.
- Run `dotnet format` before committing, or the CI pipeline will reject the change.
- Tests and samples build for the current platform's runtime identifier only, because OR-Tools ships native binaries for five platforms (about 380 MB). Keep it that way.
- Repeatedly retry upon failure until the build succeeds. If you are unsure how to fix an issue, ask for help or refer to existing code for examples.
- Before trying to fix an error, make sure you first understand it fully.
- You should never report that a feature is complete without testing it first.

### Code Style

- You must read the [Code Style Guidelines](docs/STYLE_GUIDE.md).

### Pull Requests

When asked to publish the code changes, your task is to open one or more pull requests (PRs) to merge the changes into `main` on GitHub:

- Use `git` to check what has changed as compared to the `main` branch on `origin`.
- If the changes are thematically linked, they can be published as a single PR.
- Otherwise, you'll need to divide the changes into multiple PRs using your own judgement.
- Each PR should have a singular focus, shouldn't break anything, and should be able to be merged independently.
- Ensure that all code is staged, committed and pushed. Ensure no new files are left uncommitted, and no debug code is left in the codebase.
- When creating a PR, ensure that the title and description are clear, informative, and comprehensive.
- All feature/bugfix/etc branch names should be formatted as "feature_<short description>" or "fix_<short description>" or similar.
- All PR titles should be formatted as "[<scope>] <Short summary>", e.g. "[renderer] Fixed colour inversion bug."
- You have GitHub MCP integration that can be used to do the above.
